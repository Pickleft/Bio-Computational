# RUNBOOK - Oct 27  to  Nov 4 execution plan

## planning day status (Oct 26, done)
- [x] Meta docs recon: endpoints pinned (see ARCHITECTURE §1).
- [x] Architecture, parameter seed, task split, this runbook (docs/plan/). **No project code written** (NEW WORK ONLY).
- [x] Found: existing `Bio-Computational/` is a stray .NET Framework 4.8 template  to  delete on Oct 27, regenerate .NET 9.
- [ ] You (human): register dev.meta.ai, add payment method, set spend limit ($150 cap), claim credits - **do Oct 27 evening, deadline Oct 28**.
- [ ] You: install Ollama + pull Muse Glimmer weights (before Nov 1; laptop RAM permitting; else Glimmer-mock path).

## Non-negotiable cadence
1. Commit on every green test run (small commits beat perfect commits). First commit Oct 27 = empty scaffold + docs/plan + LICENSE + THIRD_PARTY.md + DEVLOG.md.
2. DEVLOG.md entry every day, factual, dated. Note in Oct 27 entry that docs/plan are pre-event planning notes (transparent = safe).
3. Every new NuGet package  to  THIRD_PARTY.md line (name, version, license, URL) at add time. No exceptions.
4. Everything must run with `--mock-all` (no keys, no camera, no mic) - the judge path.

## Milestones & gates

### Oct 27 (Oct 27) - scaffold + M1 core neurons/synapses/pools
Hour blocks (≈6-8 h productive):
1. `git init` in repo root; delete stray `Bio-Computational/` .NET FW folder + old sln; `dotnet new sln -n BioComputational`;
   create projects per ARCHITECTURE §2 (`dotnet new classlib/console/webapi/xunit`), wire references, `dotnet build` green.
   Write LICENSE (MIT), README stub (title + one-para pitch + "What was built during the hackathon" placeholder),
   THIRD_PARTY.md (empty table), DEVLOG.md (Oct 27 entry), AGENTS.md, TASKS.md (from docs/plan draft). **Commit 1.**
2. BioCore: xoshiro256** RNG; Izhikevich SoA + subtypes; CSR graph builder (seeded, Dale, distance-dependent); delay rings.
3. BioCore: pooled conductance kernels (AMPA/NMDA/GABA-A/B), TM vesicles, MM reuptake, regional transmitter pools.
4. Tests: determinism double-run hash; TM depression; MM steady state; Dale; CSR invariants. **Commit per green test.**
Gate: `dotnet test` green ≥ 5 tests. If behind: cut IB/LTS subtypes (keep RS/FS).

### Oct 28 (Oct 28) - M1 finish + M2 start
1. Three-factor STDP + eligibility; homeostatic scaling; intrinsic plasticity; DA RPE source (reward input) + 5-HT tone. Tests: STDP window, DA-gating sign, scaling convergence, checkpoint/resume hash.
2. M2 start: spontaneous activity tuning loop (`biolab resting 60s`): tune noise σ + global GABA gain  to  no silence, no seizure; avalanche + branching ratio metrics; rate distribution.
3. **Evening: claim Meta credits (spend limit set).** Confirm `MODEL_API_KEY` env works with one curl to /v1/chat/completions (muse-spark-1.3, 10 tokens, log cost).
Gate: branching σ ∈ [0.85, 1.1] at some config; determinism incl. checkpoint. If behind: skip intrinsic plasticity.

### Oct 29 (Oct 29) - M2 done
Replay scheduler + episode schema + CA3/CA1 encode; sleep two-process state machine + NREM/REM + warp; DMN/TaskPos anti-correlation test; alpha-peak test; 1/f PSD metric. Gate: `biolab sleep 2h --warp` completes, replay events logged, resting-state SVGs produced. If behind: cut prospective-simulation recombination (keep plain replay).

### Oct 30 (Oct 30) - M3 senses (no Meta yet)
Event vision (OpenCvSharp capture + event encoder + fovea map); gammatone + onset + local VAD (NAudio); interoception (battery/CPU/RAM/thermal best-effort); predictive coding + novelty + habituation; thalamic gate + arousal; **wake-on-surprise** (mock SAM = connected components). Tests: efference placeholder, novelty/habituation, keyless run. Gate (hard): surprise wakes the sim unprompted with camera pointed at a doorway. If behind: cut periphery map (fovea only).

### Oct 31 (Oct 31) - M4 cognition
GWS ignition + log; PCI; self-model/efference attenuation; interoception to drives; feelings mapper + circumplex; episodic recall ("what happened while I was idle?"); action portfolio + social inhibition + quiet hours; output actions incl. System.Speech TTS. Tests: ignition threshold/refractory, PCI wake>NREM, recall roundtrip. Gate: ignition feed + feelings panel live on dashboard. If behind: cut mirror test permanently.

### Nov 1 (Nov 1) - M5 Meta integration
Morning: re-read docs pages for SAM 3.1 / Voice / Image request schemas (exact JSON). Then behind abstractions:
SAM 3.1 (Responses+files) on visual surprise; Muse Voice Transcribe (file mode) on speech; Glimmer via Ollama as rate-limited
inner monologue; Spark 1.3 + Spark Open (Baseten `meta-models/muse-spark-open-1026`) swappable interpreters; CostGovernor +
api_cost.jsonl; mock parity tests. **Keep the no-keys path fully working.** Gate: end-to-end: person enters  to  SAM segments  to 
person speaks  to  transcribed  to  Glimmer monologue reacts - all logged + dashboarded. If behind: Spark-Open path drops to config-doc-only (Spark 1.3 covers it).

### Nov 2 (Nov 2) - M6 dreams, Wonder, paradigms
Muse Image dreams from REM replay (deterministic seeds) + re-entry flag; psychology stimuli generation; Wonder agent tool
loop (run_paradigm/sweep/logs/plot/write_report) + first auto-report; paradigm suite: MMN, conditioning/extinction,
discounting×5-HT, masking, blink; 24 h simulated day. Gate: ≥4 paradigms produce SVG figures + comparison-to-literature notes.
If behind: cut Stroop + blink, keep MMN + conditioning.

### Nov 3 (Nov 3) - polish + docs + video
Dashboard demo mode polish (large fonts, layout rehearsed); performance numbers (CPU/RAM/spikes/s/API-per-h, idle vs active);
README complete (install, keys, Ollama/Baseten setup, Muse headband optional, "What was built during the hackathon"
feature to file to commit mapping); PARAMETERS.md, LIMITATIONS.md, THIRD_PARTY.md final; demo rehearsal ×2; **record video** (OBS).
Gate: 3-min video done + README complete. Buffer if behind: Wonder sweep depth, NWB export cut.

### Nov 4 (Nov 4) - submit
Final smoke: fresh clone  to  `--mock-all` runs  to  dashboard renders. Repo public check, license check, video link, team list.
**Submit by 17:00 ET (deadline 21:00 ET).** Export 30 s "it noticed me" clip for social (Most Viral).

## Scope-cut order (when behind): Artificial life  to  pharmacology sweeps  to  mirror test  to  Wonder sweeps  to  NWB export.
## NEVER cut: idle behavior, wake-on-surprise, SAM/Voice/Glimmer integration, working keyless demo.

## Rubric mapping (write into README)
- Impact 30: open, reproducible always-on SNN platform; automated experiments (Wonder) at zero-hardware cost; neuroscience/psych paradigms with literature comparisons.
- Meta tooling 25: five-model cost-governed cascade; open-weights on-device privacy path (Glimmer); Spark vs Spark Open interpreter choice as a *logged experimental variable*; Wonder as autonomous experiment scientist.
- Depth 20: conductance SNN w/ TM + MM + three-factor learning; criticality; PCI; determinism contract + tests.
- Demo 25: live wake-on-surprise chain, feelings, dreams, recall - all real-time on a laptop.

## Video shot list (3:00, OBS, rehearse ×2 on Nov 3)
0:00 hook "Every LLM only exists when you talk to it…" (dashboard idle)  to  0:15 idle: raster, replay, mood drift, Glimmer
monologue (LOCAL badge visible)  to  0:45 someone walks in: wake-on-surprise, SAM overlay, transcription, unprompted reaction  to 
1:30 feelings + ignition log ("what it was thinking")  to  1:45 sleep + Muse Image dreams  to  2:00 recall Q&A from real episodes  to 
2:20 science: resting-state figs, PCI wake vs sleep, Wonder report  to  2:45 built-during-hackathon summary + efficiency + repo.
Fallbacks: pre-warm SAM; keep mock path ready; record segments separately, assemble in editor.

## Compliance checklist
- [ ] Public repo, MIT license, team ≤4, one submission.
- [ ] "What was built during the hackathon" README section (features to files to commits).
- [ ] THIRD_PARTY.md: OpenAI SDK (MIT), NAudio (MIT), OpenCvSharp4 (Apache-2.0), System.Speech (MIT), optional BrainFlow (verify).
- [ ] README disclaimer: research platform, not medical/diagnostic; feelings = computational proxies.
- [ ] API spend limit set; cost log committed as artifact.
- [ ] DEVLOG daily, factual.
