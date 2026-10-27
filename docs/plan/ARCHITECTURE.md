# Bio-Computational - Architecture (planning day planning, Oct 26 2026)

> Pre-event design notes. Committed on Oct 27 as planning artifacts. **No project code exists yet** -
> per hackathon rules ("NEW WORK ONLY", build window Oct 27 - Nov 4) all code is written starting Oct 27.

## Assumptions

- Solo participant (team of 1; ≤4 allowed). Windows 11 laptop, .NET 9 SDK 9.0.304 installed, webcam + mic available.
- License: **MIT**. Repo public from Oct 27.
- All sim runs deterministic from a seed; "feelings" are computational proxies (never clinical claims).
- The existing `Bio-Computational/` project folder is an old-style .NET Framework 4.8 template - **delete and
  regenerate with .NET 9 templates on Oct 27**.
- Third-party packages (all permissive; each gets a THIRD_PARTY.md entry at add time):
  `OpenAI` (.NET SDK, MIT), `NAudio` (MIT, mic), `OpenCvSharp4` + runtime (Apache-2.0, webcam),
  `System.Speech` (MIT, local TTS). Zero npm/JS deps in the dashboard (vanilla JS + WebSocket).
  No image library - write BMP/PPM manually; decode of generated PNGs via OpenCvSharp (already a dep).

## 1. Meta Model API facts (pinned from dev.meta.ai/docs, Oct 26)

| Capability | Endpoint | Notes |
|---|---|---|
| Base | `https://api.meta.ai/v1`, Bearer `MODEL_API_KEY` | OpenAI-SDK drop-in compatible (Responses + Chat Completions) |
| Muse Wonder | `muse-wonder-1.0` | research agent brain; 1M ctx; `reasoning.effort` |
| Muse Spark | `muse-spark-1.3` (also 1.2/1.1, `-contributor` cheaper) | 1M ctx; default cloud interpreter |
| Muse Spark Open | self-hosted / Baseten `meta-models/muse-spark-open-1026` | NOT served by Model API |
| Muse Glimmer | local open weights (Apache-2.0) via Ollama `http://localhost:11434/v1` | NOT on Model API; always-on monologue |
| SAM 3.1 | `sam-3.1` via `POST /v1/responses` + `POST /v1/files` | text-prompted boxes+masks |
| Muse Voice Transcribe | realtime `wss://api.meta.ai/v1/asr/realtime`; file `POST /v1/asr/transcribe` | $0.18/h, diarization, turn-level timestamps |
| Muse Image | `POST /v1/images/generations`, edits `/v1/images/edits` | dreams, stimuli |

Every endpoint/model id behind config + interface, with a deterministic mock. Nov 1 re-reads the docs pages for
SAM 3.1 / Voice / Image request schemas before implementing (one page each, ~15 min).

## 2. Solution layout (created Oct 27)

```
BioComputational.sln                 (regenerate: dotnet new sln + projects below; delete old .NET FW folder)
src/BioCore/          - deterministic engine, ZERO external deps. Neurons, synapses (CSR), transmitters,
                        learning, regions, GWS, PCI, metrics, checkpoint, seeded RNG, abstractions (interfaces
                        for senses + Meta tiers live here so the engine never references IO/SDKs).
src/BioSenses/        - Tier 0-2 pipelines: OpenCvSharp webcam  to  event-vision; NAudio mic  to  gammatone/onset/VAD;
                        interoception (GetSystemPowerStatus P/Invoke, PerformanceCounter, WMI thermal best-effort).
src/BioMeta/          - Meta clients behind BioCore abstractions: SegmenterSam31, VoiceTranscriber, GlimmerLocal
                        (Ollama, OpenAI SDK w/ custom base), SparkCloud (api.meta.ai), SparkOpenBaseten,
                        ImageDreamer, WonderAgent (tool-calling loop); Mocks for all; CostGovernor.
src/BioCognition/     - built on BioCore: replay scheduler, sleep state machine, self-model, feelings mapper,
                        episodic memory store (jsonl), narrative builder, action portfolio.
src/BioHost/          - daemon: config (appsettings.json + flags + --demo preset), heartbeat loop, sim thread,
                        channel wiring, quiet hours, kill switch, checkpoint service. This is `dotnet run`.
src/BioDashboard/     - ASP.NET Core Minimal API: static vanilla-JS canvas UI + raw WebSocket @10 Hz.
src/BioLab/           - CLI: `paradigm`, `resting`, `sleep`, `day24h`, `sweep`, `export`, `report`; SVG figure writer.
tests/BioCore.Tests/  - xunit; the ~30 tests listed in §10.
data/ (runtime, gitignored except seeds) : episodes.jsonl, ignition log, provenance log, api_cost.jsonl,
                        checkpoints/, dreams/, exports/, reports/
```

Dependency rule: `BioCore` references nothing. `BioSenses`/`BioMeta`/`BioCognition`  to  `BioCore`. Host/Dashboard/Lab  to  all.
Every external dependency (camera, mic, WMI, HTTP, TTS, file system, clock) sits behind a `BioCore.Abstractions` interface
with a mock, so `dotnet test` and judge-runs work with **no hardware and no keys**.

## 3. Process model & determinism contract

- **SimThread owns ALL neural state.** Fixed 1 ms tick (two 0.5 ms Izhikevich substeps). Adaptive dt: idle 2 ms tick,
  ignition/active 0.5 ms substeps - dt is a pure function of logged state ⇒ still deterministic.
- Everything external enters as timestamped events on a single **sim-input queue** (`SimEvent { simMs, kind, payload, provenance }`).
  Capture threads and async Meta calls only *enqueue*; the sim consumes in sim-time order. Real-time anchor = wall clock
  when senses live; **time-warp** (×10-×120) allowed only in headless BioLab runs, or live when mode ∈ {NREM, REM} and no
  ignition for 5 min (events during warp are queued, applied on return to real-time).
- **One RNG** (xoshiro256** custom, state 4×UInt64) owned by SimThread. No `System.Random`, no threading races.
- **Determinism claim (tested):** identical (seed, event script) ⇒ identical trajectory hash. Checkpoint = arrays + RNG state
  + queues (BinaryWriter, versioned); resume reproduces the hash.
- **Provenance** on every input: `World | SelfGenerated | InterpreterFed` (§7 flag; logged separately, default OFF).

## 4. Your four-layer diagram  to  substrate

| Spec layer | Implementation |
|---|---|
| Pre-thinking | Sensory areas: RetinaEventEnc, A1 (gammatone), thalamic relay (alpha resonator), LangEnc |
| Central integrator | Hippocampal CA3/CA1 + PFC/Global-Workspace coalition |
| Post-thinking | Slow cortical store (network weights) + consolidation layer driven by replay |
| Output | MotorOut populations  to  action portfolio |
| Post to Pre feedback | (a) transporter **reuptake** consumes residual transmitter at pre-terminals; (b) **retrograde signal**:
  post-synaptic Ca-spike detection  to  per-synapse retrograde factor that scales release probability (TM `u`),
  gates eligibility traces, consumes energy, and feeds the effort component of feelings |

## 5. Neural core

**Neurons (Izhikevich 2003):** `dv/dt = 0.04v² + 5v + 140 − u + I + ξ`; `du/dt = a(bv − u)`; reset `v≥30  to  v=c, u+=d`.
Subtypes RS (a .02 b .2 c −65 d 8), FS (a .1 b .2 c −65 d 2, inhibitory), LTS, IB (see PARAMETERS-SEED). AdEx
(Brette & Gerstner 2005) behind `INeuronModel` as Nov 2 stretch. State in SoA `float[]`/`short[]`; Span + Vector<float> kernels.

**Regions (default ≈22k neurons, config scale 0.5-4×; ~20% inhibitory, Dale enforced at synapse creation):**

| Region | N | Role |
|---|---|---|
| RetinaEventEnc | 2×1024 | fovea 32×32 dense + periphery 32×32 pooled event input |
| V1 | 3072 | retinotopic E/I microcircuits |
| A1 | 2048 | 32 gammatone × 64 tonotopy |
| ThalRelay | 512 | feedback loop (≈50 ms delay)  to  8-12 Hz alpha at rest (Hughes & Crunelli 2005) |
| CA3 / CA1 | 2048 / 1024 | sparse (5%) recurrent autoassociator / time-indexed sequence buffer |
| PFC-Workspace | 2048 | ignition site |
| DMN hub | 1536 | self/memory/prospective; anti-correlated w/ TaskPos (1536) (Fox 2005) |
| InteroC | 256 | body-model input |
| LangEnc | 512 | Tier-2/3 text encoder |
| MotorOut | 512 | one population per action |

**Synapses:** CSR (`rowPtr`, `postIdx`, weights int16 Q15), receptor mask per synapse, axonal delays 1-20 ms via per-delay
spike ring. Pooled per-neuron conductances (exp2 kernels): AMPA τr .5/τd 5 ms, NMDA 10/100 ms + Mg block
(`1/[1+ (Mg/3.57)·exp(−0.062·V)]`, Jahr & Stevens 1990), GABA-A 0.5/10 ms, GABA-B 25/200 ms. Reversal: E 0 mV, I −80 mV.

**Transmitters:** per-synapse Tsodyks-Markram vesicles (R, u; update on pre-spike only - Tsodyks et al. 1998).
Regional extracellular pools with Michaelis-Menten reuptake `dT/dt = release − Vmax·T/(Km+T) − leak·T`
(DA clearance tuned to ~1-2 s extracellular half-time, Garris & Wightman 1994). DA = RPE signal (Schultz 1997) gating
plasticity; 5-HT = slow tone setting learning rate/patience/stability (Dayan & Huys 2009); optional NA (surprise gain)
and ACh (attention/encoding).

**Learning:** three-factor eligibility STDP `e += pre/post pairing; e *= exp(−Δt/τe); Δw = η·f(DA)·e` (Izhikevich 2007;
Frémaux & Gerstner 2016), homeostatic synaptic scaling toward target rates (Turrigiano 1998), intrinsic plasticity
(adjust d / threshold). Dale + sign clamp; weights bounded.

**Memory:** CA3 k-WTA stores sparse codes via fast Hebbian eligibility; episodes = `{t, code, tags[], valence, modulatorCtx}`
in `episodes.jsonl`; replay at 10-20× compression (sharp-wave-ripple style, Wilson & McNaughton 1994) with DA-gated
consolidation into cortical weights. Tags come from Tier-1/2 symbolic outputs (object classes, speech keywords, events) -
no population-vector decoding (out of scope; honest limitation).

## 6. Senses (Tier 0-2), predictive coding, wake-on-surprise

- **Event vision:** 30 fps grayscale  to  temporal-contrast threshold ±Δ (adaptive per-pixel)  to  ON/OFF events  to  fovea map  to 
  spike injection. Static room ≈ zero events ⇒ near-zero compute (predictive-processing claim is measurable here).
- **Audio:** 16 kHz mono  to  32-band ERB gammatone filterbank  to  cochleagram; onset = rectified positive spectral flux over
  threshold; VAD = energy + spectral-flatness heuristic (local, no cloud).
- **Predictive coding:** per-channel predictor (EMA + slow variance). Precision-weighted error = |x − x̂|/σ̂; only errors
  propagate. Novelty = sustained error × (1 − familiarity); familiarity decays over hours.
- **Triggers:** visual surprise (error mass in a region > θ for >300 ms, 5 s cooldown)  to  SAM 3.1 on cropped frame
  (cloud iff `privacy.cloudOptIn`, else mock = connected components on event map). Speech (VAD ≥1 s)  to  Voice Transcribe.
  Surprise  to  NA burst  to  thalamic gate opens + gain ↑ ⇒ **wake-on-surprise with zero commands**.
  Habituation (repeat ⇒ decreasing response), dishabituation (novel ⇒ reset).

## 7. GWS, self-model, interoception, feelings, sleep

- **Ignition:** module saliences `s_m` integrate with cross-inhibition; ignition when winner > θ and margin > m; broadcast
  = 150 ms gain boost + refractory 300 ms; log `{t, winner, saliences, tags, trigger provenance}`. Interpreter may only
  read ignited content (testable claim).
- **PCI (Casali 2013 / Casarotto 2016 proxy):** perturb 5% random neurons 1 ms; binarize 300 ms response (z > 2);
  Lempel-Ziv LZ76 normalized by source entropy. Expect wake/REM ≫ NREM.
- **Self-model:** efference copy  to  linear forward map predicts sensory consequence; prediction subtracted (attenuation)
  before novelty; residual small ⇒ tag `SelfGenerated`. Own TTS tagged at synthesis; own dashboard display (mirror test, stretch).
- **Interoception:** battery %, AC status, CPU/RAM, thermal (best-effort) + internal energy/transmitters  to  body model;
  homeostatic drives: energy, thermal comfort, curiosity, social.
- **Feelings:** valence = w₁(DA−DA₀) − w₂·energyDeficit − w₃·stress + w₄·socialRecent; arousal = w₅·NA + w₆·rateDeviation +
  w₇·surpriseEMA  to  Russell circumplex + named states {curious, calm, stressed, bored, tired} (Russell 1980).
- **Sleep:** two-process (Borbély 1982): homeostat S (rises awake, decays asleep) × circadian C ⇒ wake to drowsy to NREM
  (0.5-1 Hz slow oscillation injection, consolidation, synaptic scaling)  to REM cycles (90 min sim, warped); REM = vivid
  replay + Muse Image dreams, seeded deterministically from the replayed episode hash. Dreams may re-enter vision (flag).

## 8. Meta tiers (all flagged, costed, mocked)

| Tier | Trigger | Client | Budget |
|---|---|---|---|
| 1 SAM 3.1 | visual surprise | SegmentSam31 (Responses API + files) | ≤6 calls/min, crop only, opt-in |
| 2 Voice | speech ≥1 s | VoiceTranscribe (file mode; realtime WS Nov 2 if time) | $0.18/h ≈ negligible |
| 3 Glimmer | ignition, ≥2 min apart | Ollama localhost - private monologue | free, local |
| 4 Spark 1.3 / Spark Open (Baseten `meta-models/muse-spark-open-1026`) | scheduled/rare | swap interpreters, choice logged as experimental variable | capped |
| 4 Wonder | nightly + on-demand | tool loop: run_paradigm / sweep / read_logs / episodes / plot / write_report  to  `reports/` | capped |
| - Muse Image | REM dreams, stimuli | ImageDreamer | capped |

`ICostGovernor.TryBook(tier, $est)` - every call books USD + metabolic-equivalent energy (API spend physically tires the
brain: nice cost-control narrative). Full log `api_cost.jsonl {t, tier, usd, latencyMs, ok}`. Mocks: deterministic canned
outputs seeded from input hash so the whole demo runs keyless.

## 9. Dashboard & output

Vanilla-JS canvas grid over raw WebSocket @10 Hz (JSON frames; raster sent as sparse diffs): raster by region, EEG-proxy
spectrogram (Goertzel bands), transmitter pools, energy + spend gauge, feeling circumplex, mode timeline, ignition feed,
monologue ticker, dream gallery, SAM overlay, replay markers, recording LED, kill switch. `--demo` preset = large-font
high-contrast layout. Outputs: TTS (System.Speech), text display, imagery, attention saccade, explore/seek-stimulation,
silence - gated by drives + ignition + social context; social inhibition via DA on speak outcomes; refractory periods +
quiet hours (22:00-08:00 default).

## 10. Test matrix (BioCore.Tests, xunit)

Determinism double-run hash; checkpoint/resume hash; TM depression ratio; MM reuptake steady state; STDP window + DA-gating
sign; Dale enforcement; CSR builder; avalanche exponent −1.5 + branching σ≈1 (Beggs & Plenz 2003); alpha peak 8-12 Hz in
quiet idle; lognormal rate distribution; DMN anti-correlation sign; ignition threshold + refractory; PCI(wake) > PCI(NREM);
MMN deviant-vs-standard difference wave (Näätänen); conditioning acquisition + extinction; delay discounting shifts with
5-HT; efference attenuation; episode encode/recall roundtrip; 24 h warp smoke; cost governor caps; mock determinism;
flag-fallback matrix (no keys path).

## 11. BioLab CLI

`biolab resting 600s --seed 42 --plot alpha.svg`, `biolab paradigm mmn|p300|conditioning|extinction|stroop|discounting|masking|blink`,
`biolab sleep 8h --warp`, `biolab day24h --scenario room-day.json`, `biolab sweep sweeps/criticality.json`, `biolab export csv|nwb`,
`biolab report` (Wonder entry point). Paradigms use a simulated stimulus stream (no webcam) ⇒ seeded, plotted, comparable
to published results. Figures: zero-dep SVG writer.

## 12. Performance budget

22k neurons / ~0.9 M synapses ⇒ ~160 spikes/ms × 30 post-syn each ≈ 5 M synaptic ops/s + 44 M neuron-substep updates/s -
comfortably <15% of one core with Span/SIMD. Report CPU %, RAM, spikes/s, API calls/h idle vs active in dashboard footer
and README efficiency table.
