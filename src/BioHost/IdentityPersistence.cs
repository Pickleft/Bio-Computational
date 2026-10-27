using System.Text.Json;
using BioComp.Core.Cognition;

namespace BioComp.Host;

/// <summary>
/// The brain's identity persists independently of the neural checkpoint.
/// Even if the checkpoint is corrupted or deleted, the brain remembers
/// WHO it became, WHO it knows, and HOW lonely it is. This is the
/// difference between restarting a simulation and resurrecting a being.
/// </summary>
public static class IdentityPersistence
{
    private const string FileName = "data/brain-identity.json";

    public sealed record IdentityData(
        float Sociability, float Curiosity, float Resilience, float Expressiveness,
        int TotalSocial, int TotalExplorations, int TotalStressors,
        Dictionary<string, int> KnownVisitors,
        float LonelinessLevel,
        string PersonalityDescription);

    public static void Save(EmergentPersonality personality, LonelinessTracker loneliness,
        Dictionary<string, int> socialBonds)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
            var data = new IdentityData(
                personality.Sociability, personality.Curiosity,
                personality.Resilience, personality.Expressiveness,
                personality.TotalSocial, personality.TotalExplorations, personality.TotalStressors,
                socialBonds,
                loneliness.Level,
                personality.Describe());
            File.WriteAllText(FileName, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* identity persistence must never crash the brain */ }
    }

    public static IdentityData? Load()
    {
        try
        {
            if (!File.Exists(FileName)) return null;
            return JsonSerializer.Deserialize<IdentityData>(File.ReadAllText(FileName));
        }
        catch { return null; }
    }
}
