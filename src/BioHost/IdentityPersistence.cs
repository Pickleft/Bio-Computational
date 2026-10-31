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
        string PersonalityDescription,
        float AttachmentWarmth = 0, float AttachmentResponsiveness = 0,
        int AttachmentSeparations = 0, int AttachmentContacts = 0,
        int AttachmentStyle = 0, string BrainName = "", string VoiceJson = "", string UserName = "",
        string CognitionJson = "");

    public static void Save(EmergentPersonality personality, LonelinessTracker loneliness,
        Dictionary<string, int> socialBonds, AttachmentSystem? attachment = null, string? brainName = null, string? voiceJson = null, string? userName = null,
        string? cognitionJson = null)
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
                personality.Describe(),
                attachment?.Warmth ?? 0, attachment?.Responsiveness ?? 0,
                attachment?.Separations ?? 0, attachment?.Contacts ?? 0,
                (int)(attachment?.Style ?? 0), brainName ?? "", voiceJson ?? "", userName ?? "",
                cognitionJson ?? "");
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
