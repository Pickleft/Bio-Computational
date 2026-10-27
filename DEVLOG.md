# DEVLOG

Factual daily log. Dates are local (ET). Planning notes predating the window live in `docs/D0/`
and were imported in the first commit — everything after that point was built during the hackathon.

## 2026-10-27 (D1) — scaffold + M1 brain core

- Imported `docs/D0/` pre-event planning notes: architecture, parameter seed with citations, runbook, agent task split.
- Scaffold: .NET 9 solution `BioComputational.sln` with 8 projects (Core, Senses, Cognition, Meta, Host, Dashboard, Lab, Core.Tests). Removed a stray .NET Framework 4.8 template folder left by the initial Visual Studio solution. MIT license, README stub, THIRD_PARTY.md, AGENTS.md, TASKS.md. First commit.
- Scaffold: .NET 9 solution `BioComputational.sln` with 8 projects (Core, Senses, Cognition, Meta, Host, Dashboard, Lab, Core.Tests). Removed a stray .NET Framework 4.8 template folder left by the initial Visual Studio solution. MIT license, README stub, THIRD_PARTY.md, AGENTS.md, TASKS.md. First commit.
- M1 brain core (`BioComp.Core`) — in progress this day, see next entry/commit: xoshiro256** seeded RNG with saveable state; Izhikevich substrate in SoA arrays (RS/FS/LTS/IB, 0.5 ms substeps); CSR synapse graph with distance-dependent seeded wiring, Dale's law, 1–20 ms axonal delays via ring buffer; dual-exponential conductance kernels (AMPA, NMDA with Mg-unblock, GABA-A, GABA-B); Tsodyks–Markram vesicle dynamics (depressing/facilitating); regional extracellular transmitter pools with Michaelis–Menten reuptake (glutamate, GABA, dopamine, serotonin); energy ledger (spike, vesicle recycle, reuptake costs); FNV-1a trajectory hash. Tests: determinism (double-run hash equality), RNG state roundtrip, TM depression + recovery, MM steady state, Dale enforcement, CSR invariants, RS/FS rate ordering.

## 2026-10-26 (D0) — pre-event planning (no code)

- Requirements distilled into docs/D0 (architecture, parameter seed w/ citations, runbook, agent tasks).
- Meta Model API endpoints pinned from dev.meta.ai/docs; hardware surveyed; Ollama installed on E: with `OLLAMA_MODELS=E:\OllamaModels`; Glimmer iq2_m (9.8 GB) download started.
