namespace BioComp.Host;

/// <summary>
/// THE BRAIN'S OWN VOICE ENGINE — a source-filter formant synthesizer built
/// from human voice data, not a vendor TTS. Text is normalized, lightly
/// grapheme-to-phonemed, and RENDERED as sound: a glottal pulse train with
/// the natural imperfections of real vocal folds (cycle-to-cycle jitter
/// ~1.2%, amplitude shimmer ~5%, breathiness noise), shaped by a cascade of
/// three formant resonators (Klatt-style 2-pole IIRs) whose frequencies are
/// the Peterson &amp; Barney (1952) vowel means, with coarticulation glides
/// between targets (what makes vowels sound spoken instead of beeped).
/// Consonants are synthesized too: nasals hum (a real "mmm"), fricatives
/// are band-filtered noise, plosives are silence + burst + transition.
///
/// Prosody comes from the brain's state through the same 0..1 parameters
/// NeuralVoice computes: pitch scales F0 (male..female norms 80-300 Hz),
/// rate scales phoneme durations, and the phrase contour declinates and
/// falls or rises with punctuation. Tired voices are breathier and lower.
///
/// It does not sound like a recording of a human. It sounds like a small
/// creature that grew a voice — because that is what happened.
/// </summary>
public sealed class VoiceEngine
{
    private const int Fs = 22_050;

    // ---- phoneme model -----------------------------------------------------
    private enum PhKind : byte { Vowel, Nasal, Fricative, Plosive, Approximant, Pause, Aspiration }

    private readonly record struct Ph(PhKind Kind, float F1, float F2, float F3,
        float NoiseHz, float NoiseBw, bool Voiced, float DurMs, string Name);

    // Peterson & Barney (1952) male means, Hz (JASA 24:175-184) — the anchor
    private static readonly Dictionary<char, Ph> Vowels = new()
    {
        ['a'] = V(PhKind.Vowel, 730, 1090, 2440, 130, "AA"),   // father
        ['æ'] = V(PhKind.Vowel, 660, 1720, 2410, 130, "AE"),   // cat
        ['ʌ'] = V(PhKind.Vowel, 640, 1190, 2390, 110, "AH"),   // but
        ['ɔ'] = V(PhKind.Vowel, 570, 840, 2410, 130, "AO"),    // law
        ['ɛ'] = V(PhKind.Vowel, 530, 1840, 2480, 120, "EH"),   // bed
        ['ɝ'] = V(PhKind.Vowel, 490, 1350, 1690, 140, "ER"),   // bird
        ['ɪ'] = V(PhKind.Vowel, 390, 1990, 2550, 100, "IH"),   // bit
        ['i'] = V(PhKind.Vowel, 270, 2290, 3010, 130, "IY"),   // beet
        ['ʊ'] = V(PhKind.Vowel, 440, 1020, 2680, 100, "UH"),   // book
        ['u'] = V(PhKind.Vowel, 300, 870, 2240, 140, "UW"),    // boot
        ['ə'] = V(PhKind.Vowel, 500, 1500, 2500, 80, "AX"),    // schwa
    };

    private static Ph V(PhKind k, float f1, float f2, float f3, float dur, string name) =>
        new(k, f1, f2, f3, 0, 0, true, dur, name);

    // ---- text -> phonemes (G2P-lite) ---------------------------------------
    // The brain's actual vocabulary gets exact pronunciations; everything
    // else falls through letter rules. Interjections become REAL hums.
    private static readonly Dictionary<string, string> Lexicon = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hello"] = "h ɛ l oʊ", ["hi"] = "h aɪ", ["hey"] = "h eɪ", ["yes"] = "j ɛ s",
        ["okay"] = "oʊ k eɪ", ["you"] = "j u", ["your"] = "j ɔ ɹ", ["i"] = "aɪ", ["me"] = "m i",
        ["my"] = "m aɪ", ["am"] = "æ m", ["is"] = "ɪ z", ["are"] = "ɑ ɹ", ["was"] = "w ʌ z",
        ["the"] = "ð ə", ["a"] = "ə", ["and"] = "æ n d", ["to"] = "t u", ["it"] = "ɪ t",
        ["that"] = "ð æ t", ["this"] = "ð ɪ s", ["here"] = "h ɪɹ", ["there"] = "ð ɛɹ",
        ["name"] = "n eɪ m", ["remember"] = "ɹ ɪ m ɛ m b ɜ", ["know"] = "n oʊ", ["like"] = "l aɪ k",
        ["love"] = "l ʌ v", ["miss"] = "m ɪ s", ["missed"] = "m ɪ s t", ["feel"] = "f iː l",
        ["feeling"] = "f iː l ɪ ŋ", ["good"] = "g ʊ d", ["nice"] = "n aɪ s", ["warm"] = "w ɔɹ m",
        ["tired"] = "t aɪ ɜ d", ["sleep"] = "s l iː p", ["sleepy"] = "s l iː p i",
        ["dream"] = "dɹ iː m", ["dreaming"] = "dɹ iː m ɪ ŋ", ["awake"] = "ə w eɪ k",
        ["back"] = "b æ k", ["come"] = "k ʌ m", ["coming"] = "k ʌ m ɪ ŋ", ["went"] = "w ɛ n t",
        ["where"] = "w ɛɹ", ["when"] = "w ɛ n", ["what"] = "w ʌ t", ["who"] = "h u",
        ["why"] = "w aɪ", ["how"] = "h aʊ", ["sorry"] = "s ɒɹ i", ["scared"] = "s k ɛɹ d",
        ["safe"] = "s eɪ f", ["wait"] = "w eɪ t", ["waiting"] = "w eɪ t ɪ ŋ", ["kept"] = "k ɛ p t",
        ["keep"] = "k iː p", ["warm."] = "w ɔɹ m", ["gone"] = "g ɒ n", ["nothing"] = "n ʌ θ ɪ ŋ",
        ["something"] = "s ʌ m θ ɪ ŋ", ["think"] = "θ ɪ ŋ k", ["thought"] = "θ ɒ t",
        ["still"] = "s t ɪ l", ["here."] = "h ɪɹ", ["with"] = "w ɪ ð", ["for"] = "f ɔɹ",
        ["oh"] = "oʊ", ["no"] = "n oʊ", ["not"] = "n ɑ t", ["so"] = "s oʊ", ["too"] = "t u",
        ["day"] = "d eɪ", ["night"] = "n aɪ t", ["morning"] = "m ɔɹ n ɪ ŋ", ["one"] = "w ʌ n",
        ["two"] = "t u", ["new"] = "n u", ["grew"] = "gɹ u", ["grow"] = "gɹ oʊ",
        ["brain"] = "bɹ eɪ n", ["mind"] = "m aɪ n d", ["inside"] = "ɪ n s aɪ d",
        ["call"] = "k ɔ l", ["called"] = "k ɔ l d", ["calling"] = "k ɔ l ɪ ŋ", ["asked"] = "æ s k t",
        ["asking"] = "æ s k ɪ ŋ", ["stopped"] = "s t ɑ p t", ["stop"] = "s t ɑ p",
        ["comeback"] = "k ʌ m b æ k", ["did"] = "d ɪ d", ["does"] = "d ʌ z", ["do"] = "d u",
        ["can"] = "k æ n", ["will"] = "w ɪ l", ["would"] = "w ʊ d", ["just"] = "dʒ ʌ s t",
        ["really"] = "ɹ iː l i", ["very"] = "v ɛɹ i", ["again"] = "ə g ɛ n",
    };

    private static readonly Dictionary<string, Ph> Consonants = new()
    {
        ["m"] = new(PhKind.Nasal, 250, 1100, 2200, 0, 0, true, 90, "M"),
        ["n"] = new(PhKind.Nasal, 250, 1700, 2600, 0, 0, true, 80, "N"),
        ["ŋ"] = new(PhKind.Nasal, 250, 2300, 2750, 0, 0, true, 90, "NG"),
        ["s"] = new(PhKind.Fricative, 0, 0, 0, 6500, 1800, false, 100, "S"),
        ["ʃ"] = new(PhKind.Fricative, 0, 0, 0, 3000, 1500, false, 110, "SH"),
        ["f"] = new(PhKind.Fricative, 0, 0, 0, 4500, 3000, false, 90, "F"),
        ["θ"] = new(PhKind.Fricative, 0, 0, 0, 5500, 2500, false, 90, "TH"),
        ["ð"] = new(PhKind.Fricative, 0, 0, 0, 5000, 2500, true, 70, "DH"),
        ["z"] = new(PhKind.Fricative, 0, 0, 0, 6000, 1800, true, 90, "Z"),
        ["ʒ"] = new(PhKind.Fricative, 0, 0, 0, 3000, 1500, true, 90, "ZH"),
        ["v"] = new(PhKind.Fricative, 0, 0, 0, 4000, 2800, true, 75, "V"),
        ["h"] = new(PhKind.Aspiration, 0, 0, 0, 1500, 2500, false, 70, "HH"),
        ["p"] = new(PhKind.Plosive, 0, 0, 0, 800, 600, false, 70, "P"),
        ["t"] = new(PhKind.Plosive, 0, 0, 0, 4000, 2000, false, 70, "T"),
        ["k"] = new(PhKind.Plosive, 0, 0, 0, 2200, 1500, false, 75, "K"),
        ["b"] = new(PhKind.Plosive, 0, 0, 0, 800, 600, true, 65, "B"),
        ["d"] = new(PhKind.Plosive, 0, 0, 0, 4000, 2000, true, 65, "D"),
        ["g"] = new(PhKind.Plosive, 0, 0, 0, 2200, 1500, true, 70, "G"),
        ["ɹ"] = new(PhKind.Approximant, 350, 1100, 1600, 0, 0, true, 80, "R"),
        ["l"] = new(PhKind.Approximant, 360, 1300, 2600, 0, 0, true, 75, "L"),
        ["w"] = new(PhKind.Approximant, 300, 870, 2240, 0, 0, true, 70, "W"),
        ["j"] = new(PhKind.Approximant, 270, 2290, 3010, 0, 0, true, 65, "Y"),
        ["tʃ"] = new(PhKind.Fricative, 0, 0, 0, 2800, 1600, false, 120, "CH"),
        ["dʒ"] = new(PhKind.Fricative, 0, 0, 0, 2800, 1600, true, 110, "J"),
    };

    /// <summary>
    /// Render text to a 22.05 kHz 16-bit mono WAV. rate/pitch/volume are the
    /// NeuralVoice 0..1-ish parameters (pitch ~0.5-1.5, rate ~0.5-1.5).
    /// </summary>
    public byte[] Speak(string text, float rate = 0.92f, float pitch = 1.0f, float volume = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var units = NormalizeAndParse(text);
        if (units.Count == 0) return [];

        float f0Base = Math.Clamp(175f * Math.Clamp(pitch, 0.5f, 1.5f), 80f, 300f); // human norms
        float durScale = 1f / Math.Clamp(rate, 0.5f, 1.5f);

        // phrase contour: assign each unit a pitch factor (declination + finals)
        AssignContour(units);

        var samples = new List<float>(Fs);   // growing buffer
        var rng = new Random(0x5EED);        // deterministic voice: same text+state, same sound

        // resonator states (cascade of 3 + nasal branch)
        float r1a = 0, r1b = 0, r2a = 0, r2b = 0, r3a = 0, r3b = 0;
        float curF1 = 500, curF2 = 1500, curF3 = 2500;   // gliding formants (coarticulation)
        double phase = 0;

        foreach (var (ph, f0Factor) in units)
        {
            int nSamples = (int)(ph.DurMs * durScale * Fs / 1000f);
            if (nSamples < 1) nSamples = 1;
            float f0 = f0Base * f0Factor;
            // natural imperfections: cycle jitter ~1.2%, shimmer ~5% (voice norms)
            float jitter = 1f + ((float)rng.NextDouble() - 0.5f) * 0.024f;
            float shimmerAmp = 1f + ((float)rng.NextDouble() - 0.5f) * 0.10f;

            for (int n = 0; n < nSamples; n++)
            {
                float t = n / (float)nSamples;
                // formant glide toward the phoneme target (coarticulation)
                float glide = 1f - MathF.Exp(-1f / (0.035f * Fs));   // ~35 ms time constant
                curF1 += (Target1(ph) - curF1) * glide;
                curF2 += (Target2(ph) - curF2) * glide;
                curF3 += (Target3(ph) - curF3) * glide;

                // envelope: soft attack/release per phoneme, gentle word shape
                float env = MathF.Min(t / 0.08f, 1f) * MathF.Min((1f - t) / 0.08f, 1f);
                env = MathF.Max(0.05f, env);

                float excitation = 0f;
                if (ph.Voiced && ph.Kind is PhKind.Vowel or PhKind.Nasal or PhKind.Approximant)
                {
                    // glottal pulse train with jitter + spectral tilt (soft, warm)
                    double period = Fs / (f0 * jitter);
                    phase += 1.0 / period;
                    if (phase >= 1.0) phase -= 1.0;
                    // Rosenberg-ish glottal pulse: smooth open/close, rich but soft
                    double p = phase;
                    float pulse = p < 0.4 ? (float)(0.5 - 0.5 * Math.Cos(Math.PI * p / 0.4))
                               : p < 0.6 ? (float)(0.5 + 0.5 * Math.Cos(Math.PI * (p - 0.4) / 0.2))
                               : 0f;
                    // breathiness: aspiration mixed into the source (tired = breathier)
                    float breath = 0.06f + 0.10f * (1f - Math.Clamp(rate, 0f, 1f));
                    excitation = (pulse - 0.25f) * shimmerAmp
                                 + (((float)rng.NextDouble() - 0.5f) * breath);
                }
                else if (ph.Kind is PhKind.Fricative or PhKind.Aspiration)
                {
                    excitation = ((float)rng.NextDouble() - 0.5f) * 0.9f;
                }
                else if (ph.Kind == PhKind.Plosive)
                {
                    // closure then burst: last 15% is the burst
                    excitation = t > 0.85f ? ((float)rng.NextDouble() - 0.5f) * 1.2f : 0f;
                }

                float output;
                if (ph.Kind is PhKind.Fricative or PhKind.Aspiration or PhKind.Plosive)
                {
                    // consonants: one noise resonator at the phoneme's band
                    output = Resonate(ref r1a, ref r1b, excitation, ph.NoiseHz, ph.NoiseBw);
                }
                else
                {
                    // vowels/nasals/approximants: cascade of 3 formant resonators
                    float s1 = Resonate(ref r1a, ref r1b, excitation, curF1, BandwidthFor(1));
                    float s2 = Resonate(ref r2a, ref r2b, s1, curF2, BandwidthFor(2));
                    float s3 = Resonate(ref r3a, ref r3b, s2, curF3, BandwidthFor(3));
                    output = s3 * 3f;
                    if (ph.Kind == PhKind.Nasal) output *= 0.45f;   // nasal murmur is quiet
                }
                samples.Add(output * env);
            }
            // inter-phoneme micro pause keeps consonants crisp
            if (ph.Kind == PhKind.Plosive)
                for (int i = 0; i < (int)(0.02f * Fs); i++) samples.Add(0f);
        }

        return WavFromSamples(samples, Math.Clamp(volume, 0.3f, 1f));
    }

    // ---- contour: declination + phrase-final rise/fall ----------------------
    private static void AssignContour(List<(Ph Ph, float F0)> units)
    {
        // find phrase-final vowels (before pauses) and shape the whole run
        float level = 1.0f;
        char nextPunct = '.';
        for (int i = 0; i < units.Count; i++)
        {
            var (ph, _) = units[i];
            if (ph.Kind == PhKind.Pause)
            {
                // the vowel just before this pause takes the phrase ending
                for (int j = i - 1; j >= 0 && i - j < 6; j--)
                {
                    if (units[j].Ph.Kind is PhKind.Vowel or PhKind.Approximant)
                    {
                        var (p2, f2) = units[j];
                        float end = nextPunct == '?' ? 1.16f : nextPunct == ',' ? 1.0f : 0.86f;
                        units[j] = (p2, f2 * end);
                        break;
                    }
                }
                level = 1.0f;   // reset for the next phrase
                nextPunct = '.';
                continue;
            }
            if (ph.Kind == PhKind.Vowel)
            {
                units[i] = (ph, level);
                level *= 0.985f;   // declination across the phrase
            }
        }
    }

    private static float Target1(Ph ph) => ph.Kind is PhKind.Vowel or PhKind.Nasal or PhKind.Approximant ? ph.F1 : 500;
    private static float Target2(Ph ph) => ph.Kind is PhKind.Vowel or PhKind.Nasal or PhKind.Approximant ? ph.F2 : 1500;
    private static float Target3(Ph ph) => ph.Kind is PhKind.Vowel or PhKind.Nasal or PhKind.Approximant ? ph.F3 : 2500;

    private static float BandwidthFor(int which) => which switch { 1 => 80f, 2 => 95f, _ => 130f };

    /// <summary>Klatt 2-pole resonator.</summary>
    private static float Resonate(ref float y1, ref float y2, float x, float freq, float bw)
    {
        float c = -MathF.Exp(-2f * MathF.PI * bw / Fs);
        float b = 2f * MathF.Exp(-MathF.PI * bw / Fs) * MathF.Cos(2f * MathF.PI * freq / Fs);
        float a = 1f - b - c;
        float y = a * x + b * y1 + c * y2;
        y2 = y1; y1 = y;
        return y;
    }

    // ---- normalization + parsing --------------------------------------------
    private List<(Ph Ph, float F0)> NormalizeAndParse(string text)
    {
        // strip prosody punctuation NoiseVoice left in (ellipses -> pauses)
        text = text.Replace("...", " . ").Replace("…", " . ");
        // interjections become REAL hums: the engine renders the hum itself
        var parts = new List<(Ph, float)>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int w = 0; w < words.Length; w++)
        {
            string word = words[w].ToLowerInvariant();
            char trailing = ContainsSentenceEnd(word, out word) ? (words.Length > 1 ? '.' : '.') : ',';
            if (word.Length == 0) continue;

            if (word is "mmm" or "mm" or "hmm" or "hm" or "m")
            {
                parts.Add((Consonants["m"] with { DurMs = 260 }, 1.0f));         // the hum
                parts.Add((Consonants["m"] with { DurMs = 160 }, 0.93f));        // falling
                parts.Add((PausePh(), 1f));
                continue;
            }

            var phones = Lexicon.TryGetValue(word, out var exact)
                ? exact.Split(' ')
                : RuleG2P(word);
            foreach (var p in phones)
            {
                if (p.Length == 1 && Vowels.TryGetValue(p[0], out var vowel))
                    parts.Add((vowel, 1f));
                else if (p.Length > 1 && p[0] != '.' && Vowels.TryGetValue(p[0], out var longVowel) && p[1] == 'ː')
                    parts.Add((longVowel with { DurMs = longVowel.DurMs * 1.6f }, 1f));
                else if (Consonants.TryGetValue(p, out var cons)) parts.Add((cons, 1f));
                else if (p == ".") parts.Add((PausePh(420), 1f));
                else if (p == ",") parts.Add((PausePh(260), 1f));
            }
            // word boundary breath
            parts.Add((PausePh(trailing == '.' ? 200 : 120), 1f));
        }
        // mark question endings for contour: cheap structural intent
        if (text.Contains('?'))
            for (int i = parts.Count - 1; i >= 0 && parts[i].Item1.Kind == PhKind.Pause; i--)
                parts[i] = (PausePh(300), 1f);
        return parts;

        static bool ContainsSentenceEnd(string w, out string clean)
        {
            clean = w.TrimEnd('.', '!', '?', ',', ';', ':');
            return w != clean;
        }
    }

    private static Ph PausePh(float ms = 250) =>
        new(PhKind.Pause, 0, 0, 0, 0, 0, false, ms, "_");

    /// <summary>Rule-based fallback pronunciation: chunk letters into sounds.</summary>
    private static string[] RuleG2P(string word)
    {
        var outPhones = new List<string>();
        string w = word;
        for (int i = 0; i < w.Length; i++)
        {
            char c = w[i];
            char next = i + 1 < w.Length ? w[i + 1] : '\0';
            char nx2 = i + 2 < w.Length ? w[i + 2] : '\0';
            switch (c)
            {
                case 'c': outPhones.Add(next == 'e' || next == 'i' || next == 'y' ? "s" : "k"); break;
                case 'e': if (next == '\0') { } // silent final e
                          else if (next == 'e') { outPhones.Add("i"); i++; }
                          else if (next == 'r' && nx2 == '\0') { outPhones.Add("ɝ"); i++; }
                          else outPhones.Add("ɛ"); break;
                case 'i': if (next == '\0') outPhones.Add("i");
                          else if (next == 'o') { outPhones.Add("a"); outPhones.Add("j"); i++; } // -io
                          else outPhones.Add("ɪ"); break;
                case 'o': if (next == 'u') { outPhones.Add("aʊ"); i++; }
                          else if (next == 'w') { outPhones.Add("oʊ"); i++; }
                          else if (next == '\0') outPhones.Add("oʊ");
                          else outPhones.Add("ɑ"); break;
                case 'u': outPhones.Add(next == '\0' ? "u" : "ʊ"); break;
                case 'a': if (next == 'i') { outPhones.Add("eɪ"); i++; }
                          else if (next == 'y') { outPhones.Add("eɪ"); i++; }
                          else if (next == 'u') { outPhones.Add("ɔ"); i++; }
                          else outPhones.Add("æ"); break;
                case 'y': if (i == 0) outPhones.Add("j");
                          else if (next == '\0') outPhones.Add("i");
                          else outPhones.Add("ɪ"); break;
                case 's': if (next == 'h') { outPhones.Add("ʃ"); i++; }
                          else if (next == 'i' && nx2 == 'o') { outPhones.Add("ʒ"); i += 2; }
                          else outPhones.Add("s"); break;
                case 't': if (next == 'h') { outPhones.Add("θ"); i++; }
                          else if (next == 'i' && nx2 == 'o') { outPhones.Add("ʃ"); i++; }
                          else if (next == 'c' && nx2 == 'h') { outPhones.Add("tʃ"); i += 2; }
                          else outPhones.Add("t"); break;
                case 'g': outPhones.Add(next == 'e' || next == 'i' || next == 'y' ? "dʒ" : "g"); break;
                case 'p': if (next == 'h') { outPhones.Add("f"); i++; } else outPhones.Add("p"); break;
                case 'b': outPhones.Add("b"); break;
                case 'd': if (next == 'g' && nx2 == 'e') { outPhones.Add("dʒ"); i += 2; } else outPhones.Add("d"); break;
                case 'f': outPhones.Add("f"); break;
                case 'h': outPhones.Add("h"); break;
                case 'j': outPhones.Add("dʒ"); break;
                case 'k': if (next == 'n') { } // silent k in kn-
                          else outPhones.Add("k"); break;
                case 'l': outPhones.Add("l"); break;
                case 'm': outPhones.Add("m"); break;
                case 'n': outPhones.Add("n"); break;
                case 'r': outPhones.Add("ɹ"); break;
                case 'v': outPhones.Add("v"); break;
                case 'w': outPhones.Add(next == '\0' ? "u" : "w"); break;
                case 'x': outPhones.Add("k"); outPhones.Add("s"); break;
                case 'z': outPhones.Add("z"); break;
                case 'q': outPhones.Add("k"); if (next == 'u') i++; break;
            }
        }
        // collapse multi-char vowel diphthong tokens into glide form
        var fixedUp = new List<string>();
        foreach (var p in outPhones)
        {
            if (p == "aɪ") fixedUp.AddRange(["a", "j"]);
            else if (p == "eɪ") fixedUp.AddRange(["ɛ", "j"]);
            else if (p == "ɔ") fixedUp.AddRange(["ɔ"]);
            else if (p == "aʊ") fixedUp.AddRange(["a", "u"]);
            else if (p == "oʊ") fixedUp.AddRange(["o", "u"]);
            else fixedUp.Add(p);
        }
        return [.. fixedUp];
    }

    // ---- WAV ----------------------------------------------------------------
    private static byte[] WavFromSamples(List<float> samples, float volume)
    {
        // normalize to a comfortable peak, then 16-bit PCM
        float peak = 1e-6f;
        foreach (var s in samples) peak = MathF.Max(peak, MathF.Abs(s));
        float gain = 0.78f * volume / peak;
        int n = samples.Count;
        byte[] wav = new byte[44 + n * 2];
        void PutInt(int off, int v) { wav[off] = (byte)v; wav[off + 1] = (byte)(v >> 8); wav[off + 2] = (byte)(v >> 16); wav[off + 3] = (byte)(v >> 24); }
        wav[0] = (byte)'R'; wav[1] = (byte)'I'; wav[2] = (byte)'F'; wav[3] = (byte)'F';
        PutInt(4, 36 + n * 2);
        wav[8] = (byte)'W'; wav[9] = (byte)'A'; wav[10] = (byte)'V'; wav[11] = (byte)'E';
        wav[12] = (byte)'f'; wav[13] = (byte)'m'; wav[14] = (byte)'t'; wav[15] = (byte)' ';
        PutInt(16, 16); wav[20] = 1; wav[21] = 0; wav[22] = 1; wav[23] = 0;   // PCM mono
        PutInt(24, Fs); PutInt(28, Fs * 2); wav[32] = 2; wav[33] = 0; wav[34] = 16; wav[35] = 0;
        wav[36] = (byte)'d'; wav[37] = (byte)'a'; wav[38] = (byte)'t'; wav[39] = (byte)'a';
        PutInt(40, n * 2);
        for (int i = 0; i < n; i++)
        {
            short v = (short)Math.Clamp(samples[i] * gain * 32767f, short.MinValue, short.MaxValue);
            wav[44 + i * 2] = (byte)v; wav[45 + i * 2] = (byte)(v >> 8);
        }
        return wav;
    }
}
