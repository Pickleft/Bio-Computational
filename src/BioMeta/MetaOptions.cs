namespace BioComp.Meta;

/// <summary>
/// Meta Model API configuration. The key is loaded from (in order) an explicit value,
/// the MODEL_API_KEY environment variable, then the gitignored .secrets/key.txt file.
/// Nothing here ever enters the repository.
/// </summary>
public sealed class MetaOptions
{
    public string BaseUrl { get; init; } = "https://api.meta.ai/v1";
    public string SparkModel { get; set; } = "muse-spark-1.3";
    public string SamModel { get; set; } = "sam-3.1";
    public string VoiceModel { get; set; } = "muse-voice-transcribe-1.0";
    public string ImageModel { get; set; } = "muse-image-1.0";

    /// <summary>Baseten partner inference for the open-weight interpreter (Spark Open).</summary>
    public string? SparkOpenBaseUrl { get; set; }
    public string SparkOpenModel { get; set; } = "meta-models/muse-spark-open-1026";

    public string? ApiKey { get; private set; }
    public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);

    public static MetaOptions Load(string? explicitKey = null, string? secretsPath = null)
    {
        var opts = new MetaOptions();
        // resolve the secrets path relative to the solution root, not the CWD
        // (VS and dotnet-run-from-subfolder would otherwise look in the wrong place)
        string path = secretsPath ?? FindKeyFile();
        opts.ApiKey = explicitKey
            ?? Environment.GetEnvironmentVariable("MODEL_API_KEY")
            ?? (File.Exists(path) ? File.ReadAllText(path).Trim() : null);
        return opts;
    }

    /// <summary>Search upward from CWD for .secrets/key.txt (handles VS, dotnet run,
    /// and direct execution from any subdirectory).</summary>
    private static string FindKeyFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir is not null; i++)
        {
            string candidate = Path.Combine(dir.FullName, ".secrets", "key.txt");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        // also try CWD as fallback
        string cwd = Path.Combine(Directory.GetCurrentDirectory(), ".secrets", "key.txt");
        return File.Exists(cwd) ? cwd : Path.Combine(Directory.GetCurrentDirectory(), ".secrets", "key.txt");
    }
}
