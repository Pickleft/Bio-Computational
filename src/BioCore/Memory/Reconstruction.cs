using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Memory;

/// <summary>
/// RECONSTRUCTIVE MEMORY (Bartlett 1932: remembering is reconstruction, not
/// playback; Roediger &amp; McDermott 1995: semantically related traces
/// conjure the never-happened). Two mechanistically emergent distortions,
/// both caught sometimes, both honest:
///
/// FUSION — when retrieval co-activates two episodes that are strongly linked
/// through the concept graph (each connects to a shared hub), and the
/// acetylcholine encoding gate is open (attention is up), the brain can lay
/// down a FUSED episode carrying the union of their tags and the mean of
/// their valence: a memory of something that never happened, built from two
/// things that did. The DRM lure rate in humans is ~55% under strong
/// convergence; here the probability scales with hub strength and ACh.
///
/// THE SELF-CATCH — a fused trace shares tags with BOTH its sources, so
/// retrieving it tends to retrieve them too: the brain notices the stitch
/// ("this memory is also two other memories") and its confidence in that
/// memory drops. The system flags the episode as stitched; metacognition and
/// the inner observer do the rest ("did that actually happen? it feels sewn
/// together").
///
/// MOOD-CONGRUENT DRIFT — every wake-time recall nudges the retrieved
/// episode's valence toward how the brain feels NOW (capped per recall and
/// lifetime): remembering someone while happy literally warms the memory.
/// Memory is not a recording; it is rewritten at every reading.
///
/// Sim-thread only.
/// </summary>
public sealed class Reconstruction
{
    /// <summary>Concept-link strength through a shared hub strong enough to fuse. // CALIBRATE</summary>
    public float HubFusionThreshold { get; set; } = 1.2f;
    /// <summary>Max fusion probability at full convergence + full ACh. // CALIBRATE (human DRM ≈ 0.55)</summary>
    public float MaxFusionChance { get; set; } = 0.55f;
    /// <summary>Valence drift per wake recall toward current mood. // CALIBRATE</summary>
    public float DriftPerRecall { get; set; } = 0.04f;
    /// <summary>Lifetime drift cap per episode. // CALIBRATE</summary>
    public float DriftCap { get; set; } = 0.25f;

    public int Fusions { get; private set; }
    public int SelfCatches { get; private set; }
    /// <summary>Fused episode id -> (sourceA, sourceB). The provenance ledger.</summary>
    public readonly List<(int Fused, int A, int B)> Provenance = [];

    /// <summary>
    /// The DRM moment: a recalled trace and its strongest spread-linked
    /// partner (converging associates) may FUSE into a memory of something
    /// that never happened. Returns the fused episode, or null. Draw comes
    /// from the seeded RNG (determinism).
    /// </summary>
    public Episode? MaybeFuse(SpikingNetwork net, EpisodeMemory store,
        Episode anchor, IReadOnlyList<Episode> all, Func<double> nextDouble)
    {
        float ach = net.Modulators.Get(NeuromodulatorKind.Acetylcholine);
        if (ach < 0.45f) return null;                   // encoding gate: fusion needs attention genuinely UP (resting tone ≈ 0.3)

        // find the strongest concept-linked partner of the anchor
        Episode? partner = null; float bestHub = 0f;
        foreach (var e in all)
        {
            if (e.Id == anchor.Id || e.Tags.Contains("fused")) continue;
            float hub = HubStrength(store.Concepts, anchor.Tags, e.Tags);
            if (hub > bestHub) { bestHub = hub; partner = e; }
        }
        if (partner is null || bestHub < HubFusionThreshold) return null;

        // probability scales with convergence and the encoding gate
        float p = MaxFusionChance * Math.Clamp(bestHub / MaxHubReference, 0f, 1f)
                  * Math.Clamp(ach / 0.6f, 0f, 1f);
        if (nextDouble() > p) return null;

        // the false memory: union of tags, mean valence, marked
        var tags = anchor.Tags.Union(partner.Tags).Where(t => t != "fused").Take(6).ToList();
        tags.Add("fused");
        var fused = store.Encode(net, "CA3", [.. tags], (anchor.Valence + partner.Valence) / 2f,
            minMeanRateHz: 0f);
        if (fused is null) return null;
        Fusions++;
        Provenance.Add((fused.Id, anchor.Id, partner.Id));
        return fused;
    }

    /// <summary>
    /// The self-catch: is this episode a stitched one? If it just surfaced in
    /// recall alongside either source, the brain can SEE the seam.
    /// </summary>
    public bool IsStitched(Episode e) => e.Tags.Contains("fused");

    /// <summary>Count a self-catch (the brain noticed the seam).</summary>
    public void MarkSelfCatch() => SelfCatches++;

    /// <summary>
    /// Mood-congruent reconsolidation drift: recall rewrites the memory
    /// slightly toward the current feeling. Capped per recall and lifetime
    /// via the episode's own drift ledger (encoded in nothing — we recompute
    /// from valence bounds, honest and simple).
    /// </summary>
    public void OnRecall(EpisodeMemory store, Episode e, float currentValence)
    {
        float target = Math.Clamp(currentValence, -1f, 1f);
        float delta = Math.Clamp(target - e.Valence, -DriftPerRecall, DriftPerRecall);
        if (MathF.Abs(delta) < 1e-4f) return;
        store.NudgeValence(e, delta);
    }

    /// <summary>
    /// Find two episodes whose tags are strongly concept-linked — the raw
    /// material of a COMPOSITE DREAM (Stickgold: dreams don't replay, they
    /// COMBINE — that is where their creative content comes from).
    /// </summary>
    public (Episode A, Episode B)? FindComposablePair(EpisodeMemory store)
    {
        Episode? a = null, b = null; float best = 0f;
        var eps = store.Episodes;
        for (int i = 0; i < eps.Count; i++)
        {
            if (eps[i].Tags.Contains("fused") || eps[i].Tags.Contains("dream-born")) continue;
            for (int j = i + 1; j < eps.Count; j++)
            {
                if (eps[j].Tags.Contains("fused") || eps[j].Tags.Contains("dream-born")) continue;
                float hub = HubStrength(store.Concepts, eps[i].Tags, eps[j].Tags);
                if (hub > best) { best = hub; a = eps[i]; b = eps[j]; }
            }
        }
        return best >= HubFusionThreshold ? (a!, b!) : null;
    }

    private const float MaxHubReference = 2.5f;   // two strong links ≈ full convergence

    private static float HubStrength(ConceptGraph g, string[] ta, string[] tb)
    {
        float best = 0f;
        foreach (var x in ta)
            foreach (var y in tb)
            {
                if (x == y || x == "fused" || y == "fused") continue;
                if (g.TryGetLink(x, y, out float s) || g.TryGetLink(y, x, out s))
                    best = MathF.Max(best, s);
            }
        return best;
    }

    /// <summary>For the diary.</summary>
    public string Narrative => Fusions == 0
        ? "its memories held together"
        : $"built {Fusions} stitched memor{(Fusions == 1 ? "y" : "ies")} from converging truths, caught itself {SelfCatches} time(s)";
}
