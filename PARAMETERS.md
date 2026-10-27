# PARAMETERS

Defaults as implemented, with sources. CALIBRATE marks values tuned in-platform sweeps
rather than taken from literature. Full derivations and sweep results: docs/plan/.

## Neurons (Izhikevich 2003, IEEE Trans Neural Netw 14:1569)
| Type | a | b | c | d | Role |
|---|---|---|---|---|---|
| RS | 0.02 | 0.20 | -65 | 8 | excitatory principal |
| FS | 0.10 | 0.20 | -65 | 2 | fast interneuron |
| LTS | 0.02 | 0.25 | -65 | 2 | low-threshold interneuron |
| IB | 0.01 | 0.20 | -55 | 4 | intrinsically bursting |
Integration: 2 x 0.5 ms substeps per 1 ms tick. Inhibitory fraction 0.20 (Dale).
Intrinsic noise sigma = 3.0 mV, set by criticality sweep (CALIBRATE; branching ~1 at
the default wiring, bounded avalanches, 0.13 Hz population rate).

## Synapses
- AMPA 0.5/5 ms, NMDA 10/100 ms with Mg-block 1/(1+0.28 e^(-0.062 V)) (Jahr and
  Stevens 1990), GABA-A 0.5/10 ms, GABA-B 25/200 ms (Dayan and Abbott 2001 ch. 5).
- Tsodyks-Markram vesicles (Tsodyks, Pawelzik and Markram 1998): depressing U=.5,
  tau_rec=450 ms, tau_fac=50 ms; facilitating U=.25, tau_rec=200 ms, tau_fac=1000 ms.
- Axonal delays 1-20 ms (Gaussian around per-projection means).
- Weights: Q15, sign-preserving, magnitude cap 0.9, floor 0.004.

## Learning
- Three-factor STDP (Izhikevich 2007; Fremaux and Gerstner 2016): eligibility
  tau 2000 ms; pairing window A+ 1.0 / A- 0.85, tau+ 20 ms / tau- 40 ms (Bi and Poo
  1998); eta 0.005 scaled by serotonin (1.2 - 0.6 x 5-HT); DA gate tanh((DA - 0.3)/0.25).
- Homeostatic scaling (Turrigiano et al. 1998): (target/rate)^0.02 per simulated
  minute, band-clamped 0.95-1.05, target 5 Hz.

## Neuromodulators (global tones, 0..3)
| Modulator | Baseline | tau (ms) | Release driver |
|---|---|---|---|
| Dopamine | 0.30 | 2000 | reward prediction error x 1.2 (Schultz 1997) |
| Serotonin | 0.50 | 60000 | slow tone; NREM entry |
| Acetylcholine | 0.30 | 5000 | ignition broadcast; REM |
| Noradrenaline | 0.20 | 1500 | surprise 0.9; ignition 0.3 (Aston-Jones and Cohen 2005) |
| Oxytocin | 0.20 | 30000 | positive RPE; positive social feedback (Dolen et al. 2013) |
Regional fast pools (glutamate, GABA) with Michaelis-Menten reuptake; DA effective
half-time ~1-2 s (Garris and Wightman 1994). Values CALIBRATE.

## Sleep (Borbely 1982)
Pressure +0.02/min awake (metabolism-scaled), -0.12/min NREM, -0.03/min REM;
circadian peak 14:00; NREM/REM ultradian 90 min scaled to the simulated day; wake
thresholds with hysteresis; external arousal > 0.5 wakes and holds wake. NREM slow
oscillation ~0.8 Hz (down 220 ms / up 200 ms waves). Rates scale with day compression.

## Senses
- Event vision: per-pixel adaptive threshold, margin 14 gray levels (CALIBRATE),
  habituation asymmetric (up 0.02 / down 0.10); event current 22 pA (CALIBRATE).
- Cochlea: 20 ERB-spaced biquad bands 100-6000 Hz, Q 6 (CALIBRATE); onset = positive
  spectral flux > mean + 2.5 sigma and > 0.003 absolute, 128 ms refractory; voice
  floor RMS 0.004 sustained 0.5 s.
- Predictive coding (Rao and Ballard 1999): fast tau 1000 ms, slow 30 s, error-variance
  tau 5 s, novelty trace 2000 ms; salience triggers at 6 sigma sustained 250 ms,
  5 s cooldown. CALIBRATE.

## Workspace, feelings, time
- Ignition threshold 1.0, margin 0.3, cross-inhibition 0.25, integration tau 100 ms,
  broadcast 150 ms, refractory 300 ms (Dehaene and Changeux 2011). CALIBRATE.
- PCI (Casali et al. 2013): perturb 3-5% of neurons, 300 ms window, 3 ms bins,
  LZ76 (Kaspar and Schuster 1987) / source entropy.
- Feelings (Russell 1980): valence = 1.1(DA-0.3) + 0.5(OXT-0.2) - 0.9 deficit -
  0.7 stress; arousal = 0.45 NA + 0.35 DA-drive + 0.10 novelty + 0.30 stress;
  boredom over subjective time > 1 min with novelty < 0.25. Proxies, CALIBRATE.
- Subjective time (Treisman 1963; Meck 1996): clock rate = 1 + 0.6(DA-0.3) +
  0.6(NA-0.2), clamped 0.25-3; 48 log-spaced time cells 0.5 s to 30 min
  (Howard and Kahana 2002; Kraus et al. 2013).

## Energy and cost (model units)
Spike 1.0; vesicle recycle 0.2; reuptake 0.05/unit; API metabolic equivalents
20k-100k per tier. Cost governor: daily budget 20 USD (5 for one-off agent runs),
chat 1 USD/h cap, images 2 USD/h cap, per-call estimates logged to data/api_cost.jsonl.

## Paradigms
Oddball 90/10, roving blocks, 600 ms ISI; MMN = prediction-error sigma difference
(Wacongne et al. 2011 account). Conditioning: CS 500 ms tone, US at offset, ITI 2 s;
predictor alpha 0.05. Discounting: V = r/(1+kD), k = 0.35/5-HT (Mazur 1987;
Miyazaki et al. 2014).
