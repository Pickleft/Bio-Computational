# AGENTS.md - conventions for AI coding agents working in this repo

## Ground rules

1. **Determinism is sacred.** All neural state is owned by the sim thread. One seeded xoshiro256**
   RNG. External data (sensors, API results, wall clock) enters ONLY as timestamped events on the
   sim-input queue. Never call `System.Random`, never touch the clock or hardware from BioComp.Core.
2. **BioComp.Core has zero NuGet references.** The external world lives behind interfaces in
   `BioComp.Core` (abstractions), each with a deterministic mock. Everything must run with
   `--mock-all`: no keys, no camera, no mic.
3. New package ⇒ THIRD_PARTY.md row (name, version, license, URL) in the same commit. Permissive
   licenses only.
4. Every Meta API call goes through the cost governor (`ICostGovernor.TryBook`) and is logged to
   `data/api_cost.jsonl`.
5. Tests beside code: determinism-affecting changes require a test (trajectory hash, steady-state,
   or sign check) in the same commit.
6. Style: file-scoped namespaces, SoA arrays for hot paths, explicit public types, no `#region`,
   minimal comments - only for non-obvious constraints (numeric stability, citation anchors).
   Uncited constants get `// CALIBRATE`.
7. Build/test: `dotnet build BioComputational.sln` / `dotnet test`. Smoke: `dotnet run --project src/BioLab -- resting 30s`.
8. Update DEVLOG.md when a TASKS.md task moves to done. Commit on every green test run.

## Framing (hackathon compliance)

- Neuroscience / artificial-life research platform. "Feelings" are computational proxies. No
  medical or diagnostic claims, ever.
- New work only, Oct 27 - Nov 4 2026. No imported code that is not listed in THIRD_PARTY.md.
- README's "What was built during the hackathon" table must stay accurate - update it as features land.

## Numeric anchors

Citations live in docs/plan/PARAMETERS-SEED.md ( to  PARAMETERS.md). Key ones: Izhikevich 2003;
Tsodyks-Markram 1998; Jahr & Stevens 1990; Schultz 1997; Turrigiano 1998; Beggs & Plenz 2003;
Borbély 1982; Casali 2013; Russell 1980.
