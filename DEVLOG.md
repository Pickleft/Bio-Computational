# DEVLOG

Factual daily log. Dates are local (ET). Planning notes predating the window live in `docs/plan/`
and were imported in the first commit - everything after that point was built during the hackathon.

## 2026-10-27 - scaffold + brain core

- Imported `docs/plan/` pre-event planning notes: architecture, parameter seed with citations, runbook, agent task split.
- Scaffold: .NET 9 solution `BioComputational.sln` with 8 projects (Core, Senses, Cognition, Meta, Host, Dashboard, Lab, Core.Tests). Removed a stray .NET Framework 4.8 template folder left by the initial Visual Studio solution. MIT license, README stub, THIRD_PARTY.md, AGENTS.md, TASKS.md. First commit.
- Brain core (`BioComp.Core`): xoshiro256** seeded RNG with saveable state; Izhikevich substrate in SoA arrays (RS/FS/LTS/IB, 0.5 ms substeps); CSR synapse graph with distance-dependent seeded wiring, Dale's law, 1-20 ms axonal delays via ring buffer; dual-exponential conductance kernels (AMPA, NMDA with Mg-unblock, GABA-A, GABA-B); Tsodyks-Markram vesicle dynamics (depressing/facilitating); regional extracellular transmitter pools with Michaelis-Menten reuptake integrated in substeps (glutamate, GABA, dopamine, serotonin); energy ledger (spike, vesicle recycle, reuptake costs); FNV-1a trajectory hash. BioLab CLI: `resting` smoke command.
- Tests: 19/19 green - determinism (double-run hash equality, seed sensitivity), RNG state roundtrip, TM depression/recovery/facilitation, MM analytic steady state, Dale enforcement, CSR invariants, inhibitory fraction, RS tonic range, FS > RS rate, near-silence without drive/noise.
- Findings: (1) undriven network is silent at noise 1 mV - spontaneous-activity/criticality tuning needed (done later the same day, see below); (2) 6k-neuron net runs 1.3x realtime single-threaded on this machine (perf headroom exists); (3) local Muse Glimmer 30B cannot run on this hardware (16 GB RAM with ~2 GB free at idle, RX 580 4 GB unsupported by Ollama Vulkan path and too small anyway; 9.78 GB CPU buffer fails to allocate) - the live inner monologue will use Spark 1.3 via API, the open-weights bonus path stays covered by Spark Open on Baseten, honest hardware note planned for LIMITATIONS.md.

## 2026-10-27 (evening) - neuromodulators, dopamine-gated learning, checkpoints, criticality

- Neuromodulator system: global dopamine, serotonin, acetylcholine, noradrenaline and oxytocin tones with first-order kinetics and event-driven release. Dopamine bursts and pauses follow a reward prediction error against a running predictor (Schultz 1997); oxytocin rises with positive prediction errors and explicit social feedback; serotonin, acetylcholine and noradrenaline set stability, encoding and arousal.
- Three-factor STDP (Izhikevich 2007; Fremaux and Gerstner 2016): per-synapse eligibility traces with reverse-CSR post lookup, dopamine-gated weight updates, acetylcholine scales eligibility capture, serotonin scales the learning rate, sign-preserving clamps keep Dale's law intact, homeostatic synaptic scaling once per simulated minute (Turrigiano 1998). Noradrenaline raises excitatory gain during surprise. Network exposes ApplyReward / ApplySocialFeedback.
- Checkpoint save/load of the full dynamic state (topology rebuilt from seed, then state overwrite); resume verified bit-identical by test.
- Avalanche metrics (4 ms bins, branching ratio, sizes) and `biolab tune` sweep. Result for the default wiring: intrinsic noise 3 mV sits near criticality (branching ratio 1.00, ~100 discrete avalanches per 10 s, largest about 5% of the network, 0.13 Hz population rate); 5 mV and above collapses into a single endless avalanche; 2 mV and below is effectively silent. Global GABA gain 0.6-1.2 barely moves the boundary with this wiring. Default noise set to 3 mV.
- Tests: 31/31 green (added STDP potentiation/depression/no-drift/Dale-after-plasticity, checkpoint bit-identity, modulator kinetics, RPE adaptation, avalanche math).

## 2026-10-27 (late evening) - daemon and live dashboard

- BrainRuntime: the always-on daemon core shared by the headless host and the dashboard. Owns the wall-clock-paced sim loop (catch-up capped), senses (live webcam/mic/interoception where available, deterministic mocks otherwise), salience and the full cognition stack, segmentation on visual surprise (mock or SAM 3.1), speech transcription on voice activity, ignition-triggered monologue, autonomous actions with Windows SAPI speech (System.Speech) behind an ISpeechOutput interface, efference-copy tagging of its own voice, and checkpoints every 5 sim-minutes with resume on start.
- Demo mode: a 2-minute compressed day and a scripted mock world (a visitor with a voice arrives once per day at the 45 percent mark). The brain itself is never scripted - only the mock environment is, exactly like the headless surprise demo.
- Dashboard: one process with the daemon; vanilla-JS canvas UI over a 10 Hz WebSocket. Panels: live spike raster colored by region, feeling circumplex with narrative, five neuromodulator bars, the thought-process feed (one merged timeline of surprises, ignitions, heard speech, sightings, replays, sleep transitions, actions, monologues), inner-voice history, recent episodes, sleep-state strip, energy, internal-clock rate, subjective minutes, and cost (dollars and call count). ?demo=1 enlarges everything for video.
- Verified live end to end on the mock world: visitor enters, vision ignition, person episode stored, audition ignition on the voice, return to sleep, three replays of the visitor episode, self-initiated Attend/Explore actions, monologue grounded in state - all visible on the dashboard at about 1x realtime.
- Tests: 63/63 green (runtime covered by the smoke run; suite unchanged this block).

## 2026-10-27 (evening) - meta model tier: cost governor, clients, mocks, inner monologue

- Interfaces for every Meta capability in the engine assembly (ILanguageModel, ISegmenter, ITranscriber, IImageGenerator) with deterministic, input-hashed mocks, so the entire system runs keyless and reproducibly.
- Plain-HttpClient clients for the pinned schemas (dev.meta.ai/docs, Oct 27): chat/completions for Spark 1.3, responses+base64-image for SAM 3.1 with box-token parsing, multipart asr/transcribe (DIARIZATION) with a zero-dependency WAV writer, images/generations returning base64 PNG. No SDK dependency needed.
- Cost governor: one budget for all tiers. Reservations against the daily total at booking (a burst cannot over-book), per-tier hourly caps, every call logged to data/api_cost.jsonl with cost/latency/outcome; when the budget is exhausted the brain degrades to mocks instead of overspending.
- Inner monologue (MonologueGenerator): on ignition, rate-limited, the language model verbalizes the brain's own state in first person and is instructed to use only what it is given (interpreter rule). Works identically against the mock or Spark.
- Demo chain (`biolab surprise 45s`): brain asleep, person enters at 25 s, surprise and wake, visitor's speech detected by the cochlea, "heard" via transcription (mock), audition ignites the workspace, monologue reports the feeling and the person episode. `--live` switches monologue and transcription to Spark and Voice Transcribe when a key is present; `biolab meta-check` pings auth and (with --all) each endpoint within about a cent.
- Blocked: the current API key returns 401 invalid_api_key (format valid, so likely a truncated copy or never finalized in the dashboard). All live endpoints are implemented but unverified against the real API until a working key lands in .secrets/key.txt.
- Tests: 63/63 green (added cost budget/hourly-cap/jsonl with reservation semantics, mock determinism, WAV header validity, monologue rate-limit and state grounding).

## 2026-10-27 (afternoon) - global workspace, PCI, feelings, actions

- Global workspace (Dehaene and Changeux 2011): six modules (vision, audition, memory, interoception, self, language) submit salience to leaky integrators with cross inhibition; a winner clearing threshold-plus-margin ignites, broadcasts (noradrenaline and acetylcholine release), logs the event and enters a refractory period. Interpreter rule downstream: only ignited content may be reported. Vision/audition salience fed from the salience monitor, memory from active replay, body from interoception deficits.
- Perturbational complexity proxy (Casali 2013; Casarotto 2016): perturb a random neuron subset, binarize the full spike response over 300 ms at 3 ms bins, LZ76 complexity normalized by source entropy. Verified ordering: imposed global down phase (NREM-like) suppresses PCI versus the normal state. The measurement perturbs the brain and consumes RNG, like real TMS.
- Feelings (Russell 1980 circumplex, computational proxy): valence from dopamine, oxytocin, homeostatic deficits and stress (low serotonergic tone under noradrenergic drive); arousal from noradrenaline, dopamine seeking drive, novelty and stress; named states curious / calm / stressed / bored / tired / restless with plain-language narratives. Boredom is computed over subjective time (the dopamine-modulated clock), so time genuinely drags when under-stimulated.
- Action portfolio: stay-silent / attend / speak / display / explore / seek-stimulation chosen by drive-weighted softmax (temperature rises with serotonin: patience). Speaking and displaying require a recent ignition, respect quiet hours, carry long refractories, and their values learn from social feedback via dopamine and oxytocin (was speaking welcomed?). No scripted triggers.
- Self-model v1: efference copies of own outputs (voice, display) tag matching incoming audio/visual change as self-generated for attenuation.
- Recall: AnswerWhatHappened formats real stored episodes, the basis of the "what happened while I was idle" demo beat.
- Tests: 57/57 green (added LZ76 ordering with published reference value, PCI down-state suppression, ignition with refractory and winner identity, feelings tracking dopamine and battery deficits, action gating by sleep/quiet hours/ignition plus feedback learning, self-tagging, recall formatting).

## 2026-10-27 (night) - subjective time, senses, wake-on-surprise

- Subjective time: internal clock whose rate rises with dopamine and noradrenaline (Treisman 1963; Meck 1996) plus a log-spaced time-cell population giving a temporal-context code that resets on salient events (Howard and Kahana 2002; Kraus et al. 2013). Wired into the idle loop; the feelings milestone consumes it for boredom, impatience and anticipation.
- Senses, mock-first: deterministic mock camera (static room, walking-in person) and mock microphone (silence, voice-like window) behind IFrameSource/IAudioSource; real wrappers in BioComp.Senses (OpenCvSharp4 webcam, NAudio WaveIn mic, Win32 interoception via GetSystemPowerStatus/GlobalMemoryStatusEx/GetSystemTimes, no extra dependencies).
- Event vision: temporal-contrast encoder with per-pixel adaptive thresholds (habituation to sensor noise, burst on change), retinotopic injection into the retina region. A static room produces almost no events.
- Cochlea: 20-band ERB-spaced biquad bank approximating a gammatone filterbank (true kernels are a calibration TODO), positive spectral-flux onsets with adaptive threshold, absolute floor and refractory, energy-based voice-activity hint.
- Predictive coding: fast/slow EMA predictors with running error statistics per channel; standardized surprise and a slow novelty trace. Habituation and dishabituation fall out of the statistics.
- Salience monitor: sustained surprise releases noradrenaline and dopamine and reports external arousal; the sleep controller holds wake while arousal is high. Wake-on-surprise verified end to end with the mock camera: brain asleep in a quiet room, person enters, noradrenaline rises, brain wakes with no command, a "person"-tagged episode is stored (novelty burst into CA3, locus-coeruleus-style), and it dozes again once the room is static. `biolab surprise 45s` demonstrates it; `--camera` switches to the real webcam.
- Bug hunt worth recording: a first-frame temporal-contrast artifact poisoned the error statistics; an int.MaxValue refractory counter overflowed negative; a ulong arithmetic wrap made the mock "silence" full-scale noise; repeated event frames kept pushing the delayed encode deadline past the event. All fixed and covered by tests.
- Tests: 50/50 green (added time sense, event vision burst/habituation, cochlea silence/voice/band-peak, predictive habituation, wake-on-surprise integration).

## 2026-10-27 (late) - episodic memory, replay, sleep, idle loop

- Episodic memory: sparse-code snapshots of CA3 activity (top-rate winner set), tagged, valenced, with neuromodulator context; recall ranks by tag overlap, recency and valence; jsonl persistence. Ambient encoding every 30 s of wake plus explicit event encoding.
- Replay scheduler: sharp-wave-ripple-style compressed reactivation (Wilson and McNaughton 1994; Foster and Wilson 2006) of salient episodes into CA3 with a dopamine release during replay, opening the consolidation gate.
- Two-process sleep (Borbely 1982): homeostatic pressure driven by metabolic spending, circadian process, hysteresis transitions, 90-minute NREM/REM ultradian cycle, forced wake on external arousal. NREM injects ~0.8 Hz slow oscillations (down/up state waves); REM raises acetylcholine. Day length is configurable so a full day simulates compressed.
- Idle controller owns the per-tick idle loop (sleep, replay, ambient encoding, DMN/task-positive 100 ms window correlation) and is reused by BioLab and the future host daemon.
- `biolab day 240s` on the full-size brain: wake -> drowsy -> night with eight NREM/REM alternations, 25 replays, 5 ambient episodes, morning wake, second night begins. Calibration notes: sleep pressure saturates early on compressed days (energy-rate clamp); DMN/task-positive correlation reads near zero with the current wiring - the competitive (shared-inhibition) coupling that should push it negative lands with the workspace milestone.
- Tests: 39/39 green (added episode encode/recall/jsonl, replay repetition and logging, full compressed-day cycle, forced wake, NREM slow-oscillation variability, correlation bounds).

## 2026-10-26 - pre-event planning (no code)

- Requirements distilled into docs/plan (architecture, parameter seed w/ citations, runbook, agent tasks).
- Meta Model API endpoints pinned from dev.meta.ai/docs; hardware surveyed; Ollama installed on E: with `OLLAMA_MODELS=E:\OllamaModels`; Glimmer iq2_m (9.8 GB) download started.
