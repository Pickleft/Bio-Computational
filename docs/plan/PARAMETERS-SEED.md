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
