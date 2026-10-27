# LIMITATIONS

What this system is, and what it is not. Written for judges and researchers; kept
blunt on purpose.

## Claims we do not make

- **No consciousness claims.** Ignition, PCI, metastability and the like are
  computational correlates measured on a spiking network. They are research
  instruments, not evidence of experience. "Feelings", "moods", "dreams" and
  "subjective time" are named interfaces over neuromodulator and drive state -
  computational proxies, nothing more.
- **No medical, diagnostic or mental-health functionality.** None. The valence and
  arousal readouts are not validated against any clinical instrument.
- **No claim of biological realism beyond what is cited.** Each parameter carries a
  citation in PARAMETERS.md or is marked CALIBRATE. The brain is a 12-region
  caricature at 6k-24k neurons; real cortex has ~10^5 more.

## Known gaps and simplifications

- **Neuron and synapse model:** Izhikevich point neurons with pooled conductances
  (dual-exponential cascades), not multi-compartment biophysics. NMDA and GABA-B
  kinetics are approximations; the "cochlea" is an ERB-spaced biquad bank
  approximating a gammatone filterbank.
- **Conditioning:** the reward-prediction (RPE) acquisition/extinction curve is solid,
  but the neural CS-response growth is dominated by repetition dynamics at current
  parameters; we report it without claiming the classic rising CR. Calibration debt,
  recorded in DEVLOG.
- **Oddball/MMN:** deviance is carried by the predictive-coding channel (prediction
  error, the modern account). Direct-current tone injection bypasses synapses, so
  Tsodyks-Markram repetition suppression shows downstream of the stimulated band, not
  in its own firing; band rates are exported for inspection.
- **PCI:** LZ76 over binarized spike responses normalized by source entropy, per
  Casali et al., on spikes rather than EEG. Expect direction (wake > down-state), not
  clinical numbers.
- **Delay discounting** is a computational-level hyperbolic model tied to serotonergic
  tone (k = 0.35 / 5-HT), not an emergent network behavior. Labeled as such.
- **Dreams:** prompts are deterministic functions of memory and mood; the image
  endpoint has no seed parameter, so pixels vary between runs. In mock mode the dream
  is a placeholder image exercising the same pipeline.
- **Local open-weights interpreter:** Muse Glimmer 30B cannot run on the development
  machine used for this build (16 GB RAM, 4 GB GPU); the inner monologue therefore
  uses Muse Spark via the API when a key is present and the deterministic mock
  otherwise. The architecture accepts any OpenAI-compatible endpoint (Ollama, Baseten)
  via configuration.
- **Wonder agent:** with a key, the model plans tool calls; keyless, the agent runs
  the identical tool battery in a fixed order with a template narrative. Both paths
  share tools, files and cost accounting.
- **Not yet implemented:** backward masking, attentional blink, Stroop conflict,
  EEG-headband comparison (BrainFlow), NWB export, social-feedback detection from
  real human responses (speaking outcomes currently default to welcomed).

## Privacy boundary

Raw audio and video are never persisted. Camera and microphone run behind interfaces
with deterministic mocks; cloud calls are opt-in (`--live-meta`), cropped to triggers,
rate-limited and logged to data/api_cost.jsonl. The dashboard shows which tiers are
live at all times.
