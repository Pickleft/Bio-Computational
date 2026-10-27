# DEVLOG

Factual daily log. Dates are local (ET). Planning notes predating the window live in `docs/D0/`
and were imported in the first commit — everything after that point was built during the hackathon.

## 2026-10-27 (D1) — scaffold + M1 brain core

- Imported `docs/D0/` pre-event planning notes: architecture, parameter seed with citations, runbook, agent task split.
- Scaffold: .NET 9 solution `BioComputational.sln` with 8 projects (Core, Senses, Cognition, Meta, Host, Dashboard, Lab, Core.Tests). Removed a stray .NET Framework 4.8 template folder left by the initial Visual Studio solution. MIT license, README stub, THIRD_PARTY.md, AGENTS.md, TASKS.md. First commit.
- Scaffold: .NET 9 solution `BioComputational.sln` with 8 projects (Core, Senses, Cognition, Meta, Host, Dashboard, Lab, Core.Tests). Removed a stray .NET Framework 4.8 template folder left by the initial Visual Studio solution. MIT license, README stub, THIRD_PARTY.md, AGENTS.md, TASKS.md. First commit.
- M1 brain core (`BioComp.Core`): xoshiro256** seeded RNG with saveable state; Izhikevich substrate in SoA arrays (RS/FS/LTS/IB, 0.5 ms substeps); CSR synapse graph with distance-dependent seeded wiring, Dale's law, 1–20 ms axonal delays via ring buffer; dual-exponential conductance kernels (AMPA, NMDA with Mg-unblock, GABA-A, GABA-B); Tsodyks–Markram vesicle dynamics (depressing/facilitating); regional extracellular transmitter pools with Michaelis–Menten reuptake integrated in substeps (glutamate, GABA, dopamine, serotonin); energy ledger (spike, vesicle recycle, reuptake costs); FNV-1a trajectory hash. BioLab CLI: `resting` smoke command.
- Tests: 19/19 green — determinism (double-run hash equality, seed sensitivity), RNG state roundtrip, TM depression/recovery/facilitation, MM analytic steady state, Dale enforcement, CSR invariants, inhibitory fraction, RS tonic range, FS > RS rate, near-silence without drive/noise.
- Findings: (1) undriven network is silent at default noise σ=1 mV — spontaneous-activity/criticality tuning is the first M2 task tomorrow as planned; (2) 6k-neuron net runs 1.3× realtime single-threaded on this machine (perf headroom exists); (3) local Muse Glimmer 30B cannot run on this hardware (16 GB RAM with ~2 GB free at idle, RX 580 4 GB unsupported by Ollama Vulkan path and too small anyway; 9.78 GB CPU buffer fails to allocate) — Tier-3 live monologue will use Spark 1.3 via API, open-weights bonus path via Spark Open on Baseten, honest hardware note in LIMITATIONS.md.

## 2026-10-26 (D0) — pre-event planning (no code)

- Requirements distilled into docs/D0 (architecture, parameter seed w/ citations, runbook, agent tasks).
- Meta Model API endpoints pinned from dev.meta.ai/docs; hardware surveyed; Ollama installed on E: with `OLLAMA_MODELS=E:\OllamaModels`; Glimmer iq2_m (9.8 GB) download started.
