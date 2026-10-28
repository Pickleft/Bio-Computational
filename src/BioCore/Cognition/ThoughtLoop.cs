using BioComp.Core.Memory;
using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

/// <summary>
/// The four affect channels carried by every thought, kept independent by design
/// (the founding architecture): valence (good/bad), activation (how strongly
/// engaged), salience (priority relative to competing content), plasticity (how
/// much this experience should change the network). Collapsing them into one
/// "emotion" number is explicitly avoided.
/// </summary>
public readonly record struct ThoughtAffect(float Valence, float Activation, float Salience, float Plasticity);

/// <summary>One link of a thought chain: text, its evaluated affect, when, which chain, how deep.</summary>
public sealed record ThoughtLink(string Text, string[] Tags, ThoughtAffect Affect, float TimeMs, int ChainId, int Depth);

/// <summary>
/// What the loop asks the interpreter tier for: continue the current chain from
/// the previous thought and its affect, or seed a new chain from a memory.
/// </summary>
public sealed record ThoughtRequest(bool ContinueChain, string? PreviousThought, ThoughtAffect PreviousAffect,
    string[] SeedTags, string SeedMemory, int ChainId, int Depth);

/// <summary>
/// The emotionally reinforced thought-recycling loop - the founding architecture
/// made literal. Each generated thought is EVALUATED (G: affect scored from its
/// own words), RE-INJECTED into the network (F: neuromodulator releases carry
/// the affect, and the text is hashed into the language cortex as inner speech
/// - the brain hears itself think), WRITTEN to episodic memory (H), and carried
/// forward: the next request receives the previous thought and its affect as
/// context, so thoughts CHAIN (association -> interpretation -> prediction)
/// instead of being isolated samples.
///
/// Regulation (anti-rumination): salience decays toward forgetting with age,
/// near-duplicate thoughts are inhibited (habituation, Sokolov), three
/// suppressed repeats kill the chain and force a cooldown, and a fresh external
/// event displaces the chain (the world wins attention). Activation and
/// learning stay separate: chaining changes no weights; weights move only
/// through the modulator-gated STDP that the releases bias. Mood-congruent
/// retrieval follows Bower 1981 (mood biases recall toward congruent memories).
///
/// Threading: all state is owned by the sim thread. External text (from the
/// async interpreter) enters ONLY through PostResult and is consumed inside
/// Tick, like every other sim-input.
/// </summary>
public sealed class ThoughtLoop
{
    /// <summary>Minimum gap between links. A salient thought shortens it (urgency).</summary>
    public float MinIntervalMs { get; set; } = 3000f;    // CALIBRATE
    /// <summary>Salience decay time constant: unresolved thoughts fade in about tau.</summary>
    public float SalienceTauMs { get; set; } = 9000f;    // CALIBRATE
    /// <summary>A dead chain stays dead at least this long before a new one seeds.</summary>
    public float NewChainCooldownMs { get; set; } = 6000f;
    /// <summary>A chain deeper than this resolves and ends (a thought can be developed, not obsessed).</summary>
    public int MaxChainDepth { get; set; } = 6;
    /// <summary>A salience below this lets the chain die of natural boredom.</summary>
    public float InterestFloor { get; set; } = 0.22f;
    /// <summary>Token overlap above this counts as repetition (inhibition kicks in).</summary>
    public float RepeatOverlap { get; set; } = 0.6f;

    public ThoughtLink? Current { get; private set; }
    public IReadOnlyList<ThoughtLink> Chain => _chain;
    public int LinksEvaluated { get; private set; }
    public int InhibitedRepeats { get; private set; }
    public int ChainsStarted { get; private set; }

    private readonly List<ThoughtLink> _chain = [];
    private readonly List<string> _recentTexts = [];   // inhibition window
    private readonly Queue<string> _inbox = new();     // sim-input from the async interpreter
    private readonly object _inboxGate = new();
    private int _chainId;
    private float _nextEligibleMs = 3000f;             // first thought after 3s of life
    private float _lastChainEndMs = -1e9f;
    private float _lastTickMs;
    private int _strikes;
    /// <summary>Scheduled somatic read: spike totals of the language cortex at
    /// injection time, so the substrate's RESPONSE to its own thought can be
    /// measured one delay later (Damasio-style somatic appraisal).</summary>
    private (float LinkTimeMs, ulong LangCounts, ulong PfcCounts, float TimeMs)? _somaticPending;

    /// <summary>Resonance from the measured response rate (spikes/sec/neuron in
    /// the language cortex + its prefrontal target after inner-speech
    /// injection). CALIBRATE: 0.8 Hz/neuron reads as full resonance.</summary>
    public static float ResonanceFromRate(float spikesPerSecPerNeuron) =>
        Math.Clamp(spikesPerSecPerNeuron / 0.8f, 0f, 1f);

    /// <summary>Thread-safe: the async interpreter posts its generated text here.</summary>
    public void PostResult(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        lock (_inboxGate) _inbox.Enqueue(text.Trim());
    }

    /// <summary>
    /// Sim-thread heartbeat: consume posted results (evaluate + re-inject + remember),
    /// decay the current thought, let the world displace the chain, and decide
    /// whether to request the next link.
    /// </summary>
    public ThoughtRequest? Tick(SpikingNetwork net, IdleController idle, float externalEventMs = -1e9f)
    {
        // 1. drain the sim-input: every posted thought becomes neural state
        while (true)
        {
            string text;
            lock (_inboxGate) { if (!_inbox.TryDequeue(out text)) break; }
            Evaluate(net, idle, text);
        }

        // 1b. somatic appraisal: how did the substrate RESPOND to its own
        // thought? measured one delay after the inner-speech injection. A
        // thought that resonates (the network amplifies it) gains salience
        // and plasticity; one that lands on deaf neurons fades (Damasio 1996
        // somatic marker: the body votes on the thought).
        if (_somaticPending is { } sp && net.TimeMs - sp.TimeMs >= 200f)
        {
            _somaticPending = null;
            float dtSec = MathF.Max(0.001f, (net.TimeMs - sp.TimeMs) / 1000f);
            int langN = 1, pfcN = 1;
            ulong delta = RegionSpikes(net, "Lang", ref langN) - sp.LangCounts
                        + RegionSpikes(net, "PFC", ref pfcN) - sp.PfcCounts;
            float rate = delta / (float)(langN + pfcN) / dtSec;
            float resonance = ResonanceFromRate(rate);
            for (int i = _chain.Count - 1; i >= 0; i--)
                if (_chain[i].TimeMs == sp.LinkTimeMs)
                {
                    var a = _chain[i].Affect;
                    var adjusted = a with
                    {
                        // a thought that lands on quiet neurons fades, but is
                        // never killed by measurement alone (floor 0.75)
                        Salience = Math.Clamp(a.Salience * (0.75f + 0.45f * resonance) + 0.15f * resonance, 0f, 1f),
                        Plasticity = Math.Clamp(a.Plasticity * (0.7f + 0.6f * resonance), 0f, 1f),
                    };
                    _chain[i] = _chain[i] with { Affect = adjusted };
                    SomaticFeedback?.Invoke(_chain[i], resonance);
                    break;
                }
        }

        // 2. salience decay (persistence without permanence): exponential in
        // real elapsed time since the last tick, so cadence does not matter
        float k = MathF.Exp(-MathF.Max(0f, net.TimeMs - _lastTickMs) / SalienceTauMs);
        _lastTickMs = net.TimeMs;
        for (int i = 0; i < _chain.Count; i++)
        {
            var a = _chain[i].Affect with { Salience = _chain[i].Affect.Salience * k, Activation = _chain[i].Affect.Activation * k };
            _chain[i] = _chain[i] with { Affect = a };
        }
        Current = _chain.Count > 0 ? _chain[^1] : null;

        // 3. displacement: a fresh external event wins attention over any thread
        // of thought (focus shifting - the world matters more than musing)
        if (net.TimeMs - externalEventMs < 1200f && _chain.Count > 0)
            EndChain(net.TimeMs, reason: "displaced by the world");

        // 4. request the next link?
        if (net.TimeMs < _nextEligibleMs) return null;
        if (Current is { } cur && cur.Affect.Salience >= InterestFloor && cur.Depth < MaxChainDepth)
        {
            _nextEligibleMs = net.TimeMs + MinIntervalMs * (1f - 0.5f * cur.Affect.Salience); // urgent when salient
            return new ThoughtRequest(true, cur.Text, cur.Affect, [.. cur.Tags], "", cur.ChainId, cur.Depth + 1);
        }
        if (Current is null && net.TimeMs - _lastChainEndMs > NewChainCooldownMs)
        {
            _chainId++;
            _chain.Clear();
            _nextEligibleMs = net.TimeMs + MinIntervalMs;
            if (net.EpisodeStore.Episodes.Count == 0)
            {
                // a newborn brain has no memories to retrieve - its first
                // thoughts rise from raw feeling and body state alone
                var f0 = idle.Feelings.Last;
                return new ThoughtRequest(false, null, default, ["feeling", f0.Name],
                    $"no memories yet - only a raw feeling: {f0.Name} (valence {f0.Valence:F1}, arousal {f0.Arousal:F1}) and whatever the body reports",
                    _chainId, 1);
            }
            var (episode, why) = RetrieveMoodCongruent(net, idle);
            return new ThoughtRequest(false, null, default, [.. episode.Tags.Take(3)],
                $"remembering: {string.Join(", ", episode.Tags.Take(4))} (felt {(episode.Valence >= 0 ? "+" : "")}{episode.Valence:F1}, " +
                $"{(net.TimeMs - episode.TimeMs) / 1000f:F0}s ago) - picked by {why}", _chainId, 1);
        }
        return null;
    }

    /// <summary>
    /// The G function plus the feedback paths. Scores the thought's affect from
    /// its own words, inhibits near-repeats, releases neuromodulators so the
    /// affect is carried into the next cycle, re-injects the text as inner
    /// speech, and writes the thought into episodic memory.
    /// </summary>
    public ThoughtLink? Evaluate(SpikingNetwork net, IdleController idle, string text)
    {
        text = text.Trim();
        if (text.Length == 0) return null;
        var (affect, warmth) = ScoreAffect(text);
        var tags = ExtractTags(text);

        // somatic marker (Damasio 1996): the brain's CURRENT reward state tints
        // the appraisal before any of this thought's own releases land - the
        // body votes on the valence, not just the dictionary
        float da = net.Modulators.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine);
        affect = affect with { Valence = Math.Clamp(
            0.75f * affect.Valence + 0.25f * MathF.Tanh((da - 0.3f) * 3f), -1f, 1f) };

        // habituation: near-duplicates lose most of their salience (Sokolov);
        // three strikes end the chain - this is the anti-rumination guard
        bool repeated = false;
        foreach (var r in _recentTexts)
            if (TokenOverlap(r, text) > RepeatOverlap) { repeated = true; break; }
        bool suppressed = false, killed = false;
        if (repeated)
        {
            affect = affect with { Salience = affect.Salience * 0.35f };
            suppressed = true;
            InhibitedRepeats++;
            if (++_strikes >= 3)
            {
                _strikes = 0;
                EndChain(net.TimeMs, reason: "ruminating on repeat");
                _nextEligibleMs = net.TimeMs + NewChainCooldownMs;
                killed = true;
            }
        }
        else _strikes = 0;

        // a chain started by a seed request adopts that request's chain id
        // (the request incremented _chainId); a direct call starts chain 1
        bool newChain = Current is null;
        if (newChain && _chainId == 0) _chainId++;
        int chainId = Current?.ChainId ?? _chainId;
        if (newChain) ChainsStarted++;
        int depth = Current is { } c && c.ChainId == chainId ? c.Depth + 1 : 1;
        var link = new ThoughtLink(text[..Math.Min(120, text.Length)], tags, affect, net.TimeMs, chainId, depth);
        if (!killed)
        {
            _chain.Add(link);
            while (_chain.Count > MaxChainDepth + 2) _chain.RemoveAt(0);
            Current = link;
        }
        // a suppressed rumination is logged but not carried forward: the
        // working-memory slot stays empty so the chain stays dead
        _recentTexts.Add(text);
        while (_recentTexts.Count > 10) _recentTexts.RemoveAt(0);
        LinksEvaluated++;

        // ---- carry the affect into the next cycle (neuromodulator releases) ----
        float p = affect.Plasticity * (suppressed ? 0.35f : 1f);
        if (affect.Valence > 0.2f)
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine, 0.25f * p);
        if (affect.Valence < -0.2f)
        {
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline, 0.3f * p);
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Serotonin, -0.15f * p);
        }
        if (affect.Salience > 0.5f)
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Acetylcholine, 0.2f * p); // attend to own thought
        if (affect.Activation > 0.6f)
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline, 0.15f * p);
        if (warmth > 0)
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Oxytocin, 0.2f * p);

        // ---- F feedback: the thought re-enters the network as inner speech ----
        // strength scales with salience: a passing fancy whispers, an obsession shouts
        InnerSpeech(net, text, (6f + 14f * affect.Salience) * (suppressed ? 0.35f : 1f));

        // schedule the somatic read: spike totals now, response measured later
        if (!killed && _somaticPending is null)
        {
            int langN = 1, pfcN = 1;
            _somaticPending = (link.TimeMs, RegionSpikes(net, "Lang", ref langN),
                RegionSpikes(net, "PFC", ref pfcN), net.TimeMs);
        }

        // ---- H memory write: thinking about something becomes itself a memory ----
        net.EpisodeStore.Encode(net, "CA3", ["thought", .. tags], affect.Valence, minMeanRateHz: 0.001f);

        if (!killed) LinkEvaluated?.Invoke(link);
        return link;
    }

    /// <summary>Deterministic text-to-neurons injection into the language cortex:
    /// same text always stimulates the same cells (FNV-1a), scaled by salience.</summary>
    public static void InnerSpeech(SpikingNetwork net, string text, float picoAmps)
    {
        var lang = net.Regions.FirstOrDefault(r => r.Name is "Lang" or "LangEnc");
        if (lang is null) return;
        ulong h = 1469598103934665603UL;
        foreach (char ch in text) h = (h ^ ch) * 1099511628211UL;
        for (int k = 0; k < 12; k++)
        {
            h ^= h << 13; h ^= h >> 7; h ^= h << 17;
            net.InjectCurrent(lang.Start + (int)(h % (ulong)lang.Count), picoAmps);
        }
    }

    /// <summary>
    /// Associative + mood-congruent recall, cued by BOTH neural-code similarity
    /// (how the network is representing this moment vs how it represented the
    /// stored one - memory cuing by representation) and symbolic tag overlap.
    /// The developmental weight shifts with experience: a young brain retrieves
    /// mostly by word tags, an experienced one mostly by neural similarity -
    /// the symbolic scaffolding fades into the subsymbolic as the substrate
    /// learns its own codes. Mood congruence follows Bower 1981.
    /// </summary>
    public (Episode Episode, string Why) RetrieveMoodCongruent(SpikingNetwork net, IdleController idle)
    {
        float mood = idle.Feelings.Last.Valence;
        string[] seed = Current?.Tags ?? [];
        var episodes = net.EpisodeStore.Episodes;
        var queryCode = net.EpisodeStore.CurrentCode(net, "CA3");
        // CALIBRATE: full neural retrieval by ~200 stored episodes
        float wCode = MathF.Min(1f, episodes.Count / 200f);

        Episode? best1 = null, best2 = null, best3 = null;
        float s1 = -1e9f, s2 = -1e9f, s3 = -1e9f;
        foreach (var e in episodes)
        {
            // mostly think about the world; occasionally about having thought
            if (e.Tags.Contains("thought") && net.Rng.NextDouble() > 0.3) continue;
            int overlap = 0;
            foreach (var q in seed)
                foreach (var t in e.Tags)
                    if (string.Equals(q, t, StringComparison.OrdinalIgnoreCase)) { overlap++; break; }
            float age = MathF.Max(1f, (net.TimeMs - e.TimeMs) / 1000f);
            float score = ScoreEpisode(overlap, EpisodeMemory.CodeSimilarity(queryCode, e.CodeNeurons),
                mood, e.Valence, age, wCode);
            if (score > s1) { (best3, s3) = (best2, s2); (best2, s2) = (best1, s1); (best1, s1) = (e, score); }
            else if (score > s2) { (best3, s3) = (best2, s2); (best2, s2) = (e, score); }
            else if (score > s3) { (best3, s3) = (e, score); }
        }
        var pool = new[] { best1, best2, best3 }.Where(e => e is not null).ToArray();
        var pick = pool.Length > 0 ? pool[net.Rng.NextInt(pool.Length)] : episodes[^1];
        return (pick, $"code-similarity {(wCode * 100f):F0}% + tag-overlap + mood-congruence (mood {(mood >= 0 ? "+" : "")}{mood:F1})");
    }

    /// <summary>Episode retrieval score: neural-code similarity, symbolic tag
    /// overlap, mood congruence (Bower 1981), recency and emotional
    /// significance, blended by the developmental weight wCode (0 = words, 1 =
    /// neurons). retrievalStrength is Ebbinghaus forgetting resistance: often
    /// revisited memories surface more easily than faded ones.</summary>
    public static float ScoreEpisode(int tagOverlap, float codeSimilarity, float moodValence,
        float episodeValence, float ageSec, float wCode, float retrievalStrength = 1f)
    {
        float congruence = MathF.Abs(episodeValence - moodValence) < 0.35f ? 1.2f : 0.2f;
        return ((1f - wCode) * tagOverlap * 2f + wCode * codeSimilarity * 6f)
            * MathF.Min(1.5f, retrievalStrength)
            + congruence + 2f / MathF.Sqrt(MathF.Max(1f, ageSec)) + MathF.Abs(episodeValence) * 0.4f;
    }

    private void EndChain(float timeMs, string reason)
    {
        if (_chain.Count == 0) return;
        _lastChainEndMs = timeMs;
        _chain.Clear();
        Current = null;
        ChainEnded?.Invoke(reason);
    }

    /// <summary>Fires when a chain ends (displaced, bored out, or ruminating on repeat).</summary>
    public event Action<string>? ChainEnded;
    /// <summary>Fires for every evaluated link (sim thread, from Tick's drain).</summary>
    public event Action<ThoughtLink>? LinkEvaluated;
    /// <summary>Fires when the substrate's response to its own thought has been
    /// measured: how much the thought resonated (0 = deaf neurons, 1 = amplified).</summary>
    public event Action<ThoughtLink, float>? SomaticFeedback;

    private static ulong RegionSpikes(SpikingNetwork net, string name, ref int neuronCount)
    {
        foreach (var r in net.Regions)
            if (r.Name == name)
            {
                neuronCount = r.Count;
                ulong sum = 0;
                for (int i = 0; i < r.Count; i++) sum += net.Recorder.Counts[r.Start + i];
                return sum;
            }
        neuronCount = 1;
        return 0;
    }

    /// <summary>
    /// Lexicon-based affect scoring (ANEW-style subset). Honest about what it is:
    /// deterministic, cheap, inspectable - the interpreter's words are scored by
    /// the brain itself, not by another model call.
    /// </summary>
    public static (ThoughtAffect Affect, float PersonWarmth) ScoreAffect(string text)
    {
        string t = text.ToLowerInvariant();
        int pos = CountAny(t, PositiveWords), neg = CountAny(t, NegativeWords);
        int intens = CountAny(t, IntensityWords), self = CountAny(t, SelfWords);
        int person = CountAny(t, PersonWords), quest = t.Count(ch => ch == '?');
        int exclam = t.Count(ch => ch == '!');

        float valence = Math.Clamp((pos - neg) / MathF.Max(1f, pos + neg), -1f, 1f);
        float activation = Math.Clamp((intens + exclam * 2) / 3f, 0f, 1f);
        float salience = Math.Clamp((self * 0.5f + intens * 0.3f + person * 0.4f + quest * 0.3f) / 1.5f, 0f, 1f);
        float plasticity = Math.Clamp(MathF.Abs(valence) * 0.6f + salience * 0.4f, 0f, 1f);
        float warmth = person > 0 && valence > 0.1f ? Math.Clamp(person * 0.3f, 0f, 1f) : 0f;
        return (new ThoughtAffect(valence, activation, salience, plasticity), warmth);
    }

    private static string[] ExtractTags(string text) =>
        text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(char.IsLetter).ToArray()))
            .Where(w => w.Length > 3 && !StopWords.Contains(w))
            .Take(4)
            .ToArray();

    private static int CountAny(string text, string[] words)
    {
        int n = 0;
        foreach (var w in words) if (text.Contains(w)) n++;
        return n;
    }

    private static float TokenOverlap(string a, string b)
    {
        var sa = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var sb = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (sa.Count == 0 || sb.Count == 0) return 0f;
        int inter = sa.Count(sb.Contains);
        return inter / (float)(sa.Count + sb.Count - inter);
    }

    // small but real lexicons: scored, cited affect norms (ANEW-style subsets)
    private static readonly string[] PositiveWords =
        ["good", "nice", "happy", "glad", "love", "warm", "safe", "calm", "beautiful", "fun", "joy", "sweet", "hope", "wonderful"];
    private static readonly string[] NegativeWords =
        ["bad", "sad", "angry", "mad", "hate", "scared", "afraid", "wrong", "hurt", "alone", "lonely", "tired", "annoy", "worse", "stuck", "ignore"];
    private static readonly string[] IntensityWords =
        ["very", "really", "so much", "can't", "cannot", "never", "always", "must", "keep", "again and again"];
    private static readonly string[] SelfWords = ["i ", " i", "me", "my", "myself", "mine"];
    private static readonly string[] PersonWords =
        ["they", "them", "he ", "she ", "you", "someone", "person", "people", "friend", "man", "woman", "voice"];
    private static readonly HashSet<string> StopWords =
        new(["that", "this", "with", "just", "what", "when", "then", "than", "have", "been", "were", "will", "would", "could", "about", "maybe", "because", "there", "their", "these", "those", "some", "more", "most", "very", "really", "think", "thought", "feels", "feeling", "like", "know", "thing", "things"], StringComparer.OrdinalIgnoreCase);
}
