# AGENTS.md / TASKS.md draft (commit near-verbatim on D1)

═══════════ AGENTS.md ═══════════

# Agent conventions for this repo (for any AI coding agent working here — primary agent: Muse Code)

## Ground rules
1. **Determinism is sacred.** All neural state is owned by the sim thread. One seeded xoshiro256** RNG. External data
   (sensors, API results, wall clock) enters ONLY as timestamped events on the sim-input queue. Never call System.Random,
   never read the clock or hardware from BioCore.
2. **BioCore has zero NuGet references.** External world lives behind interfaces in BioCore.Abstractions, each with a mock.
   Everything must run `--mock-all` (no keys, no camera, no mic).
3. New package ⇒ THIRD_PARTY.md entry (name, version, license, URL) in the same commit. Permissive licenses only.
4. Every Meta API call goes through ICostGovernor.TryBook and is logged to data/api_cost.jsonl.
5. Tests beside code: determinism-affecting changes require a test (hash, steady-state, or sign check).
6. Match existing style: file-scoped namespaces, explicit types in public APIs, no #region, minimal comments
   (only for non-obvious constraints like numerics-stability or citation anchors).
7. Build/test: `dotnet build BioComputational.sln` / `dotnet test`. Run BioLab smoke: `dotnet run --project src/BioLab -- resting 30s`.
8. Update DEVLOG.md when a TASKS.md task moves to done.

## Numeric anchors (citations in PARAMETERS.md)
Izhikevich 2003; Tsodyks–Markram 1998; Jahr & Stevens 1990; Schultz 1997; Turrigiano 1998; Beggs & Plenz 2003;
Borbély 1982; Casali 2013; Russell 1980. If a constant has no citation, mark `// CALIBRATE`.

## Hackathon compliance
- New work only (Oct 27–Nov 4, 2026). No imported code not listed in THIRD_PARTY.md.
- README maps features → files → commits for judges. Keep commits small and well-named.
- Framing: neuroscience / artificial-life research platform. "Feelings" are computational proxies. No medical claims.

═══════════ TASKS.md ═══════════

# TASKS — parallel agent workstreams (✅ when done; day mapping in RUNBOOK.md)

## WS1 Brain core (D1–D3) — depends: none
- T1.1 xoshiro256** RNG + state save/restore ✅_
- T1.2 Izhikevich SoA + subtypes + SIMD kernels
- T1.3 CSR graph builder: seeded, distance-dependent, Dale, delays
- T1.4 Conductance kernels AMPA/NMDA(+Mg block)/GABA-A/B; pooled per-neuron g
- T1.5 TM vesicles + MM reuptake + regional pools (glu, GABA, DA, 5HT)
- T1.6 Three-factor STDP + eligibility; homeostatic scaling; intrinsic plasticity
- T1.7 DA RPE source; 5-HT tone; optional NA/ACh
- T1.8 Checkpoint/resume (binary, versioned)
- T1.9 Metrics: raster, LFP proxy, PSD/bands, avalanches/branching, 1/f
- Tests: determinism hash; TM; MM; STDP; Dale; checkpoint; avalanche; alpha; lognormal rates

## WS2 Idle/existence (D2–D3)
- T2.1 Self-sustained activity tuning (noise σ, GABA gain) → criticality
- T2.2 DMN/TaskPos subnetworks + anti-correlation
- T2.3 Episode schema + CA3/CA1 encode/recall (jsonl store)
- T2.4 Replay scheduler (compressed, DA-gated consolidation)
- T2.5 Sleep two-process machine; NREM slow oscillation; REM; warp policy
- T2.6 Boredom/novelty drive; slow mood drift

## WS3 Senses (D4)
- T3.1 Webcam capture (OpenCvSharp) + event-vision encoder + fovea/periphery
- T3.2 Mic capture (NAudio) + gammatone bank + onset + local VAD
- T3.3 Interoception: battery (GetSystemPowerStatus), CPU/RAM, thermal best-effort
- T3.4 Predictive coding + novelty/habituation/dishabituation
- T3.5 Thalamic gate + arousal; wake-on-surprise (mock segmenter)

## WS4 Cognition (D5)
- T4.1 GWS salience/coalition/ignition/broadcast + ignition log
- T4.2 PCI (LZ76, perturbation)
- T4.3 Self-model: efference copy, forward map, attenuation, provenance tags
- T4.4 Feelings (circumplex + named states) + narrative builder
- T4.5 Action portfolio + social inhibition + quiet hours + TTS output
- T4.6 Recall query over episodes

## WS5 Meta integration (D6) — depends: T3.5, T4.1
- T5.1 Abstractions + mocks (segmenter/transcriber/monologue/interpreter/imager/agent) + CostGovernor + logging
- T5.2 SAM 3.1 client (Responses + files); object identity/tracking on events
- T5.3 Muse Voice Transcribe client (file mode)
- T5.4 Glimmer local via Ollama (OpenAI SDK custom base) — rate-limited monologue
- T5.5 Spark 1.3 + Spark Open (Baseten `meta-models/muse-spark-open-1026`) swappable interpreters, choice logged
- T5.6 §7 language-feedback flag (default OFF, provenance-logged)

## WS6 Dreams + Wonder + paradigms (D7) — depends: T2.5, T5.1
- T6.1 Muse Image dreamer (deterministic seeds from episode hash) + vision re-entry flag
- T6.2 Stimulus generation (psychology paradigms)
- T6.3 Wonder tool API (paradigm/sweep/logs/episodes/plot/report) + auto-report
- T6.4 Paradigm suite: MMN, conditioning/extinction, discounting×5-HT, masking, blink, stroop
- T6.5 24 h simulated day; CSV + NWB-JSON export

## WS7 Dashboard + host (D4→D8 continuous)
- T7.1 BioHost daemon: config/flags, heartbeat, sim loop, wiring, kill switch, recording indicator
- T7.2 WebSocket @10 Hz snapshot stream (raster diffs)
- T7.3 Canvas panels: raster, spectrogram, pools, energy/spend, circumplex, mode timeline, ignition feed, monologue, dreams, SAM overlay
- T7.4 Demo mode preset; efficiency counters (CPU/RAM/spikes/s/calls-per-h)

## WS8 Docs + compliance (D1→D9)
- T8.1 README (install, keys, Ollama/Baseten, mock path, built-during-hackathon mapping, disclaimer)
- T8.2 PARAMETERS.md (from docs/D0 seed) · LIMITATIONS.md · THIRD_PARTY.md · DEVLOG.md daily
- T8.3 Demo script + video + viral clip; submission checklist
