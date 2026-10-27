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

## 2026-10-26 - pre-event planning (no code)

- Requirements distilled into docs/plan (architecture, parameter seed w/ citations, runbook, agent tasks).
- Meta Model API endpoints pinned from dev.meta.ai/docs; hardware surveyed; Ollama installed on E: with `OLLAMA_MODELS=E:\OllamaModels`; Glimmer iq2_m (9.8 GB) download started.
