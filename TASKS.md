# TASKS - workstream board (day mapping in docs/plan/RUNBOOK.md; [x]  when done)

## WS1 Brain core (Oct 27-Oct 29)
- T1.1 [x]  xoshiro256** RNG + state save/restore
- T1.2 [x]  Izhikevich SoA + subtypes (RS/FS/LTS/IB)
- T1.3 [x]  CSR graph builder: seeded, distance-dependent, Dale, delays
- T1.4 [x]  Conductance kernels AMPA/NMDA(+Mg block)/GABA-A/B
- T1.5 [x]  TM vesicles + MM reuptake + regional pools (glu, GABA, DA, 5-HT)
- T1.6 [x]  Three-factor STDP + eligibility; homeostatic scaling; intrinsic plasticity
- T1.7 [x]  DA RPE source; 5-HT tone; NA gain; ACh capture gate; oxytocin (social)
- T1.8 [x]  Checkpoint/resume (binary, versioned, bit-identical resume test)
- T1.9 Metrics: [x]  raster, [x]  avalanches/branching; pending: LFP proxy, PSD/bands, 1/f
- Tests: determinism hash [x]  · TM [x]  · MM [x]  · Dale [x]  · CSR [x]  · rates [x]  · STDP [x]  · checkpoint [x]  · modulators [x]  · avalanches [x] 

## WS2 Idle/existence (Oct 28-Oct 29)
- T2.1 [x]  Self-sustained activity tuning for the default wiring (noise 3 mV, branching ~1; retune when wiring changes)
- T2.2 DMN/TaskPos anti-correlation [x]  metric + tracking (competitive coupling lands with workspace)
- T2.3 [x]  Episode schema + CA3/CA1 encode/recall (jsonl store)
- T2.4 [x]  Replay scheduler (compressed, DA-gated consolidation)
- T2.5 [x]  Sleep two-process machine; NREM slow oscillation; REM; compressed-day support
- T2.6 Boredom/novelty drive; slow mood drift (needs senses, moved to workspace milestone)

## WS3 Senses (Oct 30)
- T3.1 [x]  Webcam capture (OpenCvSharp) + event-vision encoder (uniform retinotopy; fovea weighting pending)
- T3.2 [x]  Mic capture (NAudio) + ERB bandpass cochlea (gammatone-inspired) + onset + local VAD
- T3.3 [x]  Interoception: battery, RAM, CPU load via Win32 P/Invoke (thermal best-effort pending)
- T3.4 [x]  Predictive coding + novelty/habituation/dishabituation
- T3.5 [x]  Salience monitor (thalamic-gate precursor) + wake-on-surprise with episode tagging

## WS4 Cognition (Oct 31)
- T4.1 [x]  GWS salience/coalition/ignition/broadcast + ignition log
- T4.2 [x]  PCI (LZ76, perturbation; down-state suppression verified)
- T4.3 [x]  Self-model v1: efference copies, self-tagging of own audio/visual output (forward-map attenuation refines with the host)
- T4.4 [x]  Feelings (circumplex + named states + narratives), consuming subjective time
- T4.5 [x]  Action portfolio + social inhibition + quiet hours (TTS output lands with the host)
- T4.6 [x]  Recall query over episodes
- T4.7 [x]  Subjective time: dopamine/noradrenaline-modulated clock + time cells (feelings consume it in T4.4)

## WS5 Meta integration (Nov 1)
- T5.1 Abstractions + mocks (segmenter/transcriber/monologue/interpreter/imager/agent) + CostGovernor + logging
- T5.2 SAM 3.1 client (Responses + files); object identity/tracking on events
- T5.3 Muse Voice Transcribe client (file mode)
- T5.4 Glimmer local via Ollama - rate-limited monologue (bench first: CPU-only 2-bit on this hardware)
- T5.5 Spark 1.3 + Spark Open (Baseten `meta-models/muse-spark-open-1026`) swappable interpreters, choice logged
- T5.6 §7 language-feedback flag (default OFF, provenance-logged)

## WS6 Dreams + Wonder + paradigms (Nov 2)
- T6.1 Muse Image dreamer (deterministic seeds from episode hash) + vision re-entry flag
- T6.2 Stimulus generation (psychology paradigms)
- T6.3 Wonder tool API (paradigm/sweep/logs/episodes/plot/report) + auto-report
- T6.4 Paradigm suite: MMN, conditioning/extinction, discounting×5-HT, masking, blink, stroop
- T6.5 24 h simulated day; CSV + NWB-JSON export

## WS7 Dashboard + host (Oct 30 to Nov 3)
- T7.1 BioHost daemon: config/flags, heartbeat, sim loop, wiring, kill switch, recording indicator
- T7.2 WebSocket @10 Hz snapshot stream (raster diffs)
- T7.3 Canvas panels: raster, spectrogram, pools, energy/spend, circumplex, mode timeline, ignition feed, monologue, dreams, SAM overlay
- T7.4 Demo mode preset; efficiency counters (CPU/RAM/spikes/s/calls-per-h)

## WS8 Docs + compliance (Oct 27 to Nov 4)
- T8.1 README (install, keys, Ollama/Baseten, mock path, built-during-hackathon mapping, disclaimer)
- T8.2 PARAMETERS.md (from docs/plan seed) · LIMITATIONS.md · THIRD_PARTY.md · DEVLOG.md daily
- T8.3 Demo script + video + viral clip; submission checklist
