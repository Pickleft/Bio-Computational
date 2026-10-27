# Bio-Computational

**An always-on, biologically grounded spiking brain that thinks while idle, feels, and responds unprompted to its environment.**

A persistent daemon — not a chatbot. It exists whether or not anyone interacts with it: it generates
spontaneous activity, replays and consolidates memories, drifts through moods, gets bored, sleeps and
dreams. Point a webcam and microphone at a quiet room and it costs almost nothing; when something
surprising happens, it wakes up, attends, and reacts — with no command from anyone.

Built for the **Meta Global AI Developer Hackathon 2026**, track *Push the Frontier*, as a
**neuroscience and artificial-life research platform**: a deterministic spiking neural network core
(Izhikevich neurons, conductance synapses, Tsodyks–Markram vesicles, dopamine-gated three-factor
STDP) whose cognition is measurable (ignition, PCI, criticality, resting-state signatures), with
Meta's models wired in as senses, imagination and verbalization — never as the dynamics themselves.

> **This is a research instrument, not a medical or diagnostic tool.** "Feelings", "moods" and
> "consciousness correlates" are **computational proxies** — neuromodulator states mapped to a
> valence/arousal space. No clinical or mental-health claims are made or implied.

## Status

Building **Oct 27 – Nov 4, 2026** (hackathon window). See [DEVLOG.md](DEVLOG.md) for the daily log,
[TASKS.md](TASKS.md) for the workstream board, [docs/D0/](docs/D0) for the architecture and
parameter tables with literature citations.

## Quickstart

```bash
dotnet build BioComputational.sln
dotnet test                      # runs fully offline — no keys, no camera, no mic needed
dotnet run --project src/BioLab -- resting 60s
```

Every external dependency (camera, mic, Meta APIs) sits behind an interface with a deterministic
mock, so the whole system runs keyless.

## What was built during the hackathon (Oct 27 – Nov 4, 2026)

*This table maps features to the files and commit range that implement them — all work in this
repository was produced during the hackathon window. Pre-window material is limited to the
planning notes in `docs/D0/` (imported in the first commit, see DEVLOG).*

| Feature | Files | Commits |
|---|---|---|
| *(filled at submission: brain core, idle dynamics, wake-on-surprise, Meta cascade, dashboard, paradigms)* | | |

## Documentation

- [docs/D0/ARCHITECTURE.md](docs/D0/ARCHITECTURE.md) — system design
- [docs/D0/PARAMETERS-SEED.md](docs/D0/PARAMETERS-SEED.md) — parameters + citations (grows into PARAMETERS.md)
- [docs/D0/RUNBOOK.md](docs/D0/RUNBOOK.md) — milestone plan
- [LIMITATIONS.md](LIMITATIONS.md) *(created D8)* — what the proxies are and are not
- [THIRD_PARTY.md](THIRD_PARTY.md) — dependencies and licenses

## License

MIT — see [LICENSE](LICENSE).
