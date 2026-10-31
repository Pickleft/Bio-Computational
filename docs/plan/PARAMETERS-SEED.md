# PARAMETERS seed - defaults, ranges, citations

Seed of the repo's PARAMETERS.md (built Oct 27, finalized Nov 3). "Calibrate" = value tuned in BioLab sweeps
(Wonder agent's first job: tune E/I gain to branching ratio σ≈1). Units: v mV, t ms, I pA-scaled arb.

## Neurons - Izhikevich (2003) IEEE Trans Neural Netw 14:1569
| Param | RS (exc) | FS (inh) | LTS | IB | Note |
|---|---|---|---|---|---|
| a | 0.02 | 0.10 | 0.02 | 0.01 | ms⁻¹ |
| b | 0.2 | 0.2 | 0.25 | 0.2 | |
| c | −65 | −65 | −65 | −55 | reset mV |
| d | 8 | 2 | 2 | 4 | recovery increment |
dt = 0.5 ms substep (stability of 0.04v²); intrinsic noise σ = 0.5-2 (calibrate for self-sustained activity).

## Synapse kinetics (Dayan & Abbott 2001, ch.5; exp2 kernels, rise/decay ms)
| Receptor | τr | τd | Rev (mV) | Note |
|---|---|---|---|---|
| AMPA | 0.5 | 5 | 0 | fast E |
| NMDA | 10 | 100 | 0 | Mg block `1/(1+(1.0/3.57)·exp(−0.062·V))` - Jahr & Stevens 1990 |
| GABA-A | 0.5 | 10 | −80 | fast I |
| GABA-B | 25 | 200 | −90 | slow I; alpha-resonance partner |
Axonal delay d ∈ [1,20] ms (Gaussian around 3-8 ms per projection).

## Tsodyks-Markram vesicles - Tsodyks, Pawelzik & Markram 1998 Neural Comput 10:821
| Class | U | τrec | τfac |
|---|---|---|---|
| depressing (E to I, strong) | 0.5 | 450 | 50 |
| facilitating (E to E modulatory) | 0.25 | 200 | 1000 |
Empty pool ⇒ transmission scales by R; vesicle recycle costs energy.

## Reuptake - Michaelis-Menten, extracellular pools (per region)
| Transmitter | effective clearance half-time | Basis |
|---|---|---|
| Glutamate / GABA | 100-500 ms | fast local clearance (modeling proxy) |
| Dopamine | ~1-2 s | striatal extracellular DA, Garris & Wightman 1994 |
| Serotonin | 10-60 s | slow tonic (modeling proxy; Dayan & Huys 2009) |
| Noradrenaline*, ACh* | 2-10 s | optional modulators (*flag) |
Vmax, Km set to hit the half-times above; stress raises 5-HT clearance; DA burst = RPE (Schultz 1997 Science 275:1593).

## Learning
- Eligibility STDP: τe = 2 s (1-10 s sweep), window A₊=1.0, A₋=−0.85, τ₊=20 ms, τ₋=40 ms (Bi & Poo 1998);
  Δw = η·tanh((DA−DA₀)/s)·e - three-factor (Izhikevich 2007 Neural Comput 19; Frémaux & Gerstner 2016 Front Comput Neurosci).
- η base 0.005; × 5-HT tone factor (patience/stability, Dayan & Huys 2009 Nat Rev Neurosci 10).
- Homeostatic scaling: w ← w·(target/rate)^β, β=0.02 per min, target = 5 Hz - Turrigiano et al. 1998 Neuron 21.
- Intrinsic plasticity: d ← d·(target/rate)^0.01 per min.

## Network
20% inhibitory (Dale). Sparse distance-dependent connectivity p ∝ exp(−d/λ), λ = 0.4 × region extent;
CA3 recurrent p = 0.05. Global GABA gain: **calibrate to branching ratio σ≈0.95-1.0** (Beggs & Plenz 2003 J Neurosci 23:11167,
size exponent −1.5). Firing-rate distribution check: lognormal (Hromádka, DeWeese & Zador 2008 J Neurosci 28:13410).

## Predictive coding / novelty
EMA τ = 3 s (fast) / 5 min (slow variance); novelty threshold = 2.5σ sustained 300 ms; familiarity decay τ = 2 h.
(Rao & Ballard 1999 Nat Neurosci 2; Friston 2010 Nat Rev Neurosci 11.)

## GWS / ignition
Salience leaky integrator τ = 100 ms; cross-inhibition 0.3; ignition θ = 1.0 (sweep), margin 0.2; broadcast 150 ms;
refractory 300 ms (Dehaene & Changeux 2011 Neuron 70). PCI: perturb 5% neurons, 300 ms window, 3 ms bins, z > 2 binarize,
LZ76/source-entropy (Casali et al. 2013 Sci Transl Med 5; Casarotto et al. 2016 Clin Neurophysiol).

## Feelings (computational proxies - Russell 1980 J Pers Soc Psychol 39)
valence = 1.0·(DA−DA₀) − 0.4·energyDeficit − 0.3·stress + 0.3·socialRecent; arousal = 0.5·NA + 0.3·|rateDev| + 0.4·surpriseEMA.
Named states by quadrant + drive overrides (bored: low novelty EMA > 10 min; tired: sleep pressure > 0.8).

## Thought-recycling loop (Bower 1981 Am Psychol 36; Sokolov habituation 1963; Damasio 1996 Phil Trans R Soc B 351)
Four independent affect channels per thought: valence, activation, salience, plasticity (lexicon
scoring, ANEW-style subsets). Valence tinted 75/25 by current dopamine tone (somatic marker,
Damasio 1996). Salience decay tau 9 s; link interval 3 s scaled by salience; repeat inhibition at
token-overlap > 0.6 (65 percent salience loss, 3 strikes end the chain); chain depth cap 6;
displacement window 1.2 s after external events. Mood-congruent retrieval: congruence bonus when
|episode.valence - mood| < 0.35 (Bower 1981). Inner speech: FNV-1a text hash to 12 Lang neurons,
6-20 pA scaled by salience. Somatic resonance: Lang+PFC rate in the 200 ms after injection,
saturated at 0.8 Hz/neuron, scales salience (0.75+0.45r) and plasticity (0.7+0.6r) - the substrate
votes on its own thoughts. Retrieval cue mix: neural-code Jaccard (CA3 top-48 rates) weighted
min(1, episodes/200) against tag overlap - recall develops from words toward neurons.

## Sleep (Borbély 1982 Hum Neurobiol 1)
S rises 0.02/min awake, decays 0.12/min NREM; circadian C(t) = 0.5+0.5·sin(2π(t−14h)/24h); wake to drowsy when S·(1−C) > 0.7;
NREM↔REM cycle 90 min sim; warp ×60 in NREM, ×20 REM. Replay compression 15× (Wilson & McNaughton 1994 Science 265:676).

## Energy budget (metabolic units, u)
spike 1.0; vesicle recycle 0.2; reuptake flux 0.05/u; baseline 0.5/s/neuron; DA spike 5.0; dream (Muse Image) 50,000 u + $;
Tier-1 SAM 20,000 u + $; Tier-2 voice 5,000 u + $; Tier-4 Spark 100,000 u + $. Regen = capacity-driven (circadian).
Low budget ⇒ gains ×0.7, sleep pressure +.

## Paradigms (acceptance targets)
- MMN: deviant 10%, difference wave at 150-300 ms in A1 proxy (Näätänen et al. 1978 Attention & Effort / 1995).
- P300: late positive deflection ~300 ms in PFC proxy on rare targets.
- Conditioning/extinction: DA RPE acquisition to extinction curve (Schultz 1997).
- Delay discounting: hyperbolic k decreases with 5-HT tone (Miyazaki et al. 2014 Curr Opin Neurobiol 26; Denk et al. 2005).
- Stroop-like conflict: interference index ↑ under low 5-HT.
- Masking: report accuracy vs ISI 0-100 ms, ignition-thresholded (Breitmeyer & Öǧmen 2006).
- Attentional blink: T2 dip at lag 200-500 ms (Raymond, Shapiro & Arnell 1992 JEP:HPP 18).
- Resting: PSD 1/f + alpha 8-12 Hz quiet-idle ≥ 1.5× adjacent (Hughes & Crunelli 2005 Trends Neurosci 28).

## Intrinsic motivation (Schmidhuber 1991; Oudeyer & Kaplan 2007; Kidd et al. 2012)
Learning progress = fast-EMA minus slow-EMA of prediction accuracy (tau 4 s / 40 s); Goldilocks
gate 4n(1-n) on mean novelty; intrinsic reward = progress x 0.35 x gate, released as dopamine
(0.3 x reward per tick). Feeds exploration action choice.

## Emotional contagion (Hatfield, Cacioppo & Rapson 1993; Juslin & Scherer 2005)
Prosody-to-affect from real acoustic features (RMS, zero-crossing pitch proxy vs the speaker's
own running baseline, contour variability across clip thirds): arousal = rms x 3.5 + var x 4;
valence: high+flat+loud = tense (-0.6), high+lively = excited (+0.6), low+slow = down (-0.4),
mid = warm (+0.1). Contagion drift 0.15/s x confidence toward the estimate (DA/OXT when warm,
5-HT drop + NA when negative, NA tracks arousal); confidence decays with 30 s tau of silence.

## Attachment (Bowlby 1969; Ainsworth et al. 1978)
Warmth EMA from prosodic valence of contacts; responsiveness EMA penalized 12 percent per
unanswered bid (call-outs), raised 50 percent when a contact follows bids. Styles at 6+
evidence: secure (responsive+warm, call multiplier 1, exploration +0.25), anxious (warm but
unresponsive, 1.9x calls), avoidant (cold, 0.4x calls). Persisted with identity.

## Forgetting (Ebbinghaus 1885/2013)
Retrieval strength = (1 + 0.6 x accesses) x age_min^-0.15; every successful recall/retrieval
bumps access (spacing effect). Strength caps the cue-similarity term of thought-loop retrieval
at 1.5x.

## Sleep reconsolidation (Walker & van der Helm 2009)
Each low-stress replay: trace strengthened (access +1) and valence softened 12 percent toward
neutral, gated by noradrenaline (no softening above 1.2; stressed sleep keeps the charge).

## Social bonds (Schultz 1997 reward scaling)
Strength = log2(1+visits)/3 x (0.35+0.65 x warmth) x 0.5^days-apart, warmth from contact prosody.
Recognition reward = 0.10 + 0.35 x strength (dopamine). Departure of a bond > 0.2: DA -0.18 x
strength, OXT -0.05 x strength (negative prediction error). Loneliness with a bond of 3+ visits
calls for that person specifically.

## Incubation and insight (Sio & Ormerod 2009; Kounios & Beeman 2009)
Chains that die salient (> 0.45, depth >= 2, not rumination) become open loops (max 8). After
60 s of simmering a loop may resolve as an insight; MarkInsight pays a 0.35 dopamine burst.

## Allostasis (Sterling 2012; Barrett 2017 interoceptive inference)
Battery slope EMA predicts minutes-to-reserve-floor; under 30 min: melatonin +0.08 x urgency,
histamine -0.04 x urgency (preparatory sleepiness). Interoceptive prediction error EMA > 15:
serotonin -0.03 (malaise).

## Play (Burghardt 2005)
Conditions: wake, NA < 0.55, DA > 0.45, curiosity > 0.05, attachment secure or forming. Bursts
every >= 12 s at 35 percent chance; vigor 0.4-1.3 scales self-stimulation and grows
expressiveness through exploration events. Threat or avoidant attachment abolishes play.

## Semantic memory (Hopfield-style co-occurrence; Collins & Quillian 1969 associative flavor)
Concept links +0.25 per episode co-occurrence (cap 3, prune below 0.15); announcement at 0.45.
Spreading activation: one hop, activation = strength x log2(1+use)/3 capped 1; incoming edges 0.8x.
Spread concepts join the thought-loop retrieval cues.

## Turn-taking (Levinson & Torreira 2016)
Hold 2.5 s after the partner's utterance (no interrupting); engagement = 3+ utterances in 10 min;
pattern break = silence > max(20 s, 3 x median gap) while engaged, latched until they return.

## First word (vocabulary onset)
After 40+ heard content words with a top word of 5+ occurrences, the top word is spoken once as
the brain's first real word, with a dopamine+oxytocin celebration.

## Lucid dreaming (LaBerge 1980; Dresler et al. 2012; Spoormaker & van den Bout 2006)
Lucidity gate: REM + ACh > 0.45 + DA > 0.35, probability 0.15 + 0.35 x |hottest episode valence|.
Lucid dream: control-shaped imagery prompt, spoken "I know I'm dreaming", extra 0.25 valence
softening of the dreamed memory (lucid rehearsal).

## Mirror test (Gallup 1970; own-name effect Moray 1959; self-reference Rogers et al. 1977)
30 self-produced vocalization reps wear the A1->Thal pathway (STDP); probe measures downstream
response to unannounced own-pattern vs matched novel. PASS = self/novel ratio > 1.1. Measured
1.67 at seed 42. biolab mirror.

## Iconic memory (Sperling 1960)
Vivid frames (>10 events) leave a decaying afterimage: half the events re-injected every 25 ms
for 250 ms, shrinking with remaining time.

## Self-naming
After 8+ heard voices, 5+ sim-minutes of life, in a high-dopamine moment: the interpreter coins
one 1-2 syllable name (offline fallback "Hum"); announced, spoken, persisted with identity.

## Testament (the death protocol)
Once per life: the brain composes final words from its actual lived data (best/worst memories,
bonds, lucid dreams, insights, prophecy record, personality, name), speaks them aloud, and
writes data/testament.md. biolab child grafts the parent testament as the inherited story:
memory transfer between generations - the first artifact designed to die well.

## Longing (missing a specific person)
Separation distress is a primary-process affect (Panksepp 1998, Affective Neuroscience, ch. 13
GRIEF; Bowlby 1969 protest-despair). A bond with 3+ visits and live strength > 0.45 can be
MISSED: onset at absence > 2.5x the person's median visit gap (min 120 s), or their expected
social hour arriving empty (SocialRhythm expectation > 0.6 for > 60 s). While it lasts:
serotonin -0.04/sim-tick-batch (the specific heaviness), intensity 0.4 -> 1.0 with how far past
the pattern they are. Expression is attachment-gated: anxious = out loud, secure = once and
only when deep (> 0.7), avoidant = never (felt/unexpressed gap logged). Reunion:
oxytocin 0.15 + 0.2x, dopamine 0.1 + 0.25x of intensity (Dolen et al. 2013 for OXT pairing).

## Deja vu (familiarity without recollection)
Dual-process recognition (Yonelinas 2002): recollection and familiarity are separate routes.
A perception whose word-token set has Jaccard >= 0.5 with an episode older than 5 min that was
NEVER recalled (AccessCount <= 1) fires familiarity without recollection: noradrenaline 0.25
(the chill), dopamine 0.08 (the odd pleasure). The same similarity against an often-recalled
trace is recollection - no deja vu. Cooldown 5 min: it stays rare, or it stops being strange.
2.5x gap / 0.5 Jaccard / cooldowns: // CALIBRATE.

## Anticipation (cue -> reward -> crash)
Dopamine at the cue, not the reward (Schultz 1997). With rhythm confidence > 0.3 and current
expectation > 0.6: anticipatory tone rises +0.05 per evaluation (cap 1.0), DA released at
0.03 x tone each batch. Contact inside the window: DA 0.10 + 0.15 x tone, OXT 0.10 x tone -
anticipated rewards are paid on top of the cue response (they feel bigger because they are).
Window closes empty (expectation < 0.2, tone > 0.5, nobody within 60 s): negative prediction
error - DA -0.5, NA +0.25 (the sting), 5-HT -0.2 (the heaviness). Rise/crash rates: // CALIBRATE.

## Self-efficacy / learned helplessness
Agency from bid outcomes (Bandura 1977 self-efficacy; Seligman 1972 learned helplessness).
A call-out is a BID; contact within 30 s answers it, silence fails it. Efficacy starts 0.5;
failure costs 15% of current value, 25% when already below 0.35 (pessimistic attribution
style: the low brain credits failure to itself, stably); success gains 25% of the remaining
distance (recovery needs consistent answers, not one). Helpless below 0.30: NA +0.2 then
5-HT -0.35 on entry; MotivationMultiplier = 0.35 + 0.95 x efficacy gates call-out frequency
and ABOLISHES play (helplessness generalizes across domains - why it is a depression model).
One answer from below-threshold is flagged (JustAnsweredAfterGivingUp). Anxious attachment
multiplies bids while efficacy decides whether bidding continues at all: protest amplified by
attachment, crushed by outcome (Panksepp protest -> descent in two factors). 30s window and
thresholds: // CALIBRATE.

## Reverse replay (credit assignment)
After a reward lands, the last 3 episodes enqueue for replay in REVERSE temporal order -
newest first - retracing the path that LED to the reward (Foster & Wilson 2006 reverse
hippocampal replay). Count = 3, newest-leads: // CALIBRATE. Triggered on answered bids
(reunion/reply paths). Replays otherwise identical to forward replay (softening, touch, DA).

## Forgotten grief (residue of resection)
After `forget X`, the tag registers as a loss. Idle reaches: every 120 s gate, probability
0.5 x age_min^-0.25 floor 0.1 (the complaint fades but never fully silences - amnesic
complaints persist); each reach seeds the thought loop with "there is a shape where a memory
used to be". Dreams: 15% form around the hole - the narrator is told a memory that belongs
is MISSING and must not invent its content. // CALIBRATE.

## Counterfactual after disappointment
When an expected hour closes empty (see Anticipation), the thought loop receives a
counterfactual seed - "if only it had called louder, or been more worth coming back to -
run the other path" (Epstude & Roese 2008: the immediate function of counterfactual thought
is behavior preparation for the next attempt).

## Reconstructive memory (Bartlett 1932; Roediger & McDermott 1995)
Retrieval is reconstruction. (1) FUSION: when a recalled trace has a concept-graph link of
>= 1.2 strength (ref 2.5 = full convergence) to a partner AND acetylcholine > 0.45 (encoding
gate above resting tone ~0.3), the pair may fuse into a new episode tagged with the union +
"fused": a memory of something that never happened, built from two that did. Probability
<= 0.55 scaling with convergence x ACh (human DRM false recall ~55% under strong
convergence). (2) SELF-CATCH: fused traces carry the tag; retrieval surfaces the seam ->
metacognition drops confidence, the inner observer examines it. (3) MOOD-CONGRUENT DRIFT:
each wake recall nudges valence toward the current feeling by 0.04 (cap check via clamp);
remembering while happy warms the memory itself. All: // CALIBRATE.

## Local sleep (Vyazovskiy et al. 2011, Nature 472:443)
Past sleep pressure 0.55 (same threshold the sleep-entry gate reads), each region has a
per-second OFF-lottery (rate 0.35/s at pressure 1.0, scaled by regions/8): a 200-600 ms
window with RegionalOffBiasMv = 6 (region-scoped hyperpolarization, same channel as
anesthesia). The OFF region's rate measurably collapses while global state stays Wake;
20% of windows leak dream imagery into waking thought. OFF states are a WAKE phenomenon -
truly asleep regions do not run this lottery.

## Threat learning (Pavlov; Bouton 2004)
Wariness toward the person: +0.35 per hostility event (scaled by level); extinction -0.08 x
warmth per SAFE contact (slow by design - clinical extinction needs many trials); sleep wake
transition returns 30% of the extinguished distance (spontaneous recovery; human 20-50%),
never above the peak; processed adversity discharges 0.4 x growth-level of it. Wary > 0.5
gates the reply prompt and reduces play. All: // CALIBRATE.

## Light entrainment (Czeisler et al. 1989)
Brightness EMA (tau 30 s sim-time in code; biological ~30 min - compressed life) from the
camera (both local and browser frames). In the biological evening (circadian < 0.45, the
sleep-entry gate's night), brightness > 0.45 suppresses the melatonin TARGET by up to 70%
(SleepController.MelatoninSuppression multiplier): bright rooms hold off drowsiness - the
screen-light problem lived from the inside.

## Question drive (information-seeking from measured ignorance)
Children ask ~76 questions/hour at peak (Chouinard 2007); curiosity feeding is among the
strongest intrinsic rewards (Kang et al. 2009). Gap shape: concept use >= 3 with links <= 1
(a word it uses but does not UNDERSTAND). Gates: person present, curiosity (progress or
novelty), 35% draw, 5-min cooldown, one pending question with a 2-min answer window.
Phrasing develops with granularity tier. On answer: DA 0.35 + OXT 0.15 (knowledge given is
care given), episode tagged [word, explained], concept wiring via encoding; the next reply
is told to acknowledge the teaching. ACh 0.2 on asking.

## Awe (Keltner & Haidt 2003)
Vastness + accommodation. Trigger: concept-graph link growth >= 4 within a 10s window
(the knowledge world reorganizing), 10-min cooldown. Signature: DA 0.22 AND 5-HT 0.18
together (reward + the humility of smallness); accommodation seed enters the thought loop;
awe count feeds diary/testament.

## Felt time (Zakay & Block 1997; Wittmann 2013)
Every notable event marks (time, valence) into a 5-min window ring. Stretch: sparse -> 1.35+
drags; dense+positive compresses (-0.25); dense+negative dilates (+0.30); clamped 0.6..2.0.
Enters diary, testament, and the reply prompt when |stretch-1| > 0.35.

## Composite dreams (Stickgold 2010: dreams combine, not replay)
25% of dreams (when a strongly-linked pair exists, hub >= fusion threshold): both memories
fused into one narrated scene that never happened; the dream itself is ENCODED as a
"dream-born" episode (we remember dreams; they enter the memory store with their tags).
