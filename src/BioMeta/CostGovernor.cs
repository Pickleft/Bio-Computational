using System.Text.Json;

namespace BioComp.Meta;

public enum MetaTier { Chat, Segment, Voice, Image, Wonder }

/// <summary>
/// One budget for every external call. Books estimated dollars before the request,
/// records actual cost, latency and success to data/api_cost.jsonl, and refuses calls
/// once a tier's hourly cap or the daily total is exhausted (the brain degrades to
/// mocks instead of overspending - spending its budget literally makes it quieter).
/// </summary>
public sealed class CostGovernor
{
    public float DailyBudgetUsd { get; set; } = 25f;
    public float ChatHourlyCapUsd { get; set; } = 1.0f;
    public float ImageHourlyCapUsd { get; set; } = 2.0f;

    public float SpentTodayUsd { get; private set; }
    public int CallsToday { get; private set; }
    public int RefusedCalls { get; private set; }
    public string? LastError { get; private set; }

    public string LogPath { get; set; } = Path.Combine("data", "api_cost.jsonl");

    private readonly Dictionary<MetaTier, (float Spent, int Calls, long WindowStart)> _hourly = new();
    private readonly object _gate = new();
    private float _reservedToday; // booked but not yet completed

    public static float EstimateUsd(MetaTier tier) => tier switch
    {
        MetaTier.Chat => 0.002f,
        MetaTier.Segment => 0.005f,
        MetaTier.Voice => 0.002f,   // $0.18/h, ~30 s clips
        MetaTier.Image => 0.01f,    // flat per image (documented)
        MetaTier.Wonder => 0.02f,
        _ => 0.01f,
    };

    private static float HourlyCap(CostGovernor g, MetaTier tier) => tier switch
    {
        MetaTier.Chat => g.ChatHourlyCapUsd,
        MetaTier.Image => g.ImageHourlyCapUsd,
        _ => 2.0f,
    };

    public bool TryBook(MetaTier tier)
    {
        lock (_gate)
        {
            float est = EstimateUsd(tier);
            if (SpentTodayUsd + _reservedToday + est > DailyBudgetUsd) { RefusedCalls++; return false; }

            long hour = DateTimeOffset.UtcNow.UtcTicks / TimeSpan.TicksPerHour;
            if (!_hourly.TryGetValue(tier, out var w) || w.WindowStart != hour)
            {
                w = (0, 0, hour);
                _hourly[tier] = w;
            }
            if (w.Spent + est > HourlyCap(this, tier)) { RefusedCalls++; return false; }
            _hourly[tier] = (w.Spent + est, w.Calls + 1, hour);
            _reservedToday += est;
            return true;
        }
    }

    public void Record(MetaTier tier, float actualUsd, double latencyMs, bool ok, string note = "")
    {
        lock (_gate)
        {
            _reservedToday = MathF.Max(0f, _reservedToday - EstimateUsd(tier)); // release the reservation
            SpentTodayUsd += ok ? actualUsd : 0f;
            CallsToday++;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, JsonSerializer.Serialize(new
                {
                    t = DateTimeOffset.UtcNow.ToString("o"),
                    tier = tier.ToString(),
                    usd = actualUsd,
                    ms = Math.Round(latencyMs, 1),
                    ok,
                    note,
                }) + Environment.NewLine);
            }
            catch { /* cost logging must never break the brain */ }
        }
    }

    /// <summary>Convenience wrapper: book, run, record.</summary>
    public async Task<T?> RunAsync<T>(MetaTier tier, Func<Task<T>> call, Func<T, bool> okTest)
    {
        if (!TryBook(tier)) return default;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            T result = await call();
            sw.Stop();
            bool ok = okTest(result);
            Record(tier, EstimateUsd(tier), sw.Elapsed.TotalMilliseconds, ok);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            LastError = ex.Message;
            Record(tier, 0f, sw.Elapsed.TotalMilliseconds, ok: false, note: "exception");
            return default;
        }
    }
}
