# Bio-Computational

**An always-on, biologically grounded spiking brain that thinks while idle, feels, and reacts unprompted to the world through a webcam and microphone.**

Not a chatbot. Not a wrapper around an API. A persistent daemon whose cognition is a deterministic spiking neural network built from scratch in pure C#: it generates its own spontaneous activity, replays and consolidates its memories, drifts through moods, gets bored, falls asleep, and dreams. A quiet, unchanging room costs it almost nothing. Walk in, and it notices on its own.

Built for the Meta Global AI Developer Hackathon 2026, Push the Frontier track, as a neuroscience and artificial-life research platform.

> Research instrument, not a medical or diagnostic tool. "Feelings", "moods" and "consciousness correlates" are computational proxies: neuromodulator states mapped to a valence/arousal space. No clinical or mental-health claims are made or implied.

## The problem

Every AI system you use blinks out of existence between requests. An LLM has no ongoing activity, no idle thoughts, no memory of the last five minutes of its environment, and no ability to act unless spoken to. The gap is not intelligence, it is existence: no persistent state, no self-generated behavior, no internal life to measure.

Studying that side of cognition the traditional way is brutally expensive. Real brains need labs, EEG rigs, subjects and ethics boards. Existing spiking simulators (NEST, Brian, BMTK) are powerful but heavy, and none of them come as a living, sensing agent you can just run on a laptop.

Bio-Computational closes both gaps: a brain analog that exists continuously, and a lab you can carry.

## What it is

A .NET 9 daemon hosting a 6k-24k neuron spiking network (Izhikevich neurons, conductance synapses, Dale's law), wired into real senses (webcam event-vision, gammatone audio, machine interoception) and into Meta's models as its verbalization, imagination and scientist. The neural dynamics are 100 percent home-grown: language models never drive them, they only report on them. Everything is deterministic from a seed, checkpointable, and runs fully offline with zero API keys.

## Why it is different

- **Seven neurotransmitters, not a buzzword.** Glutamate and GABA pools with per-synapse vesicle dynamics (Tsodyks-Markram) and Michaelis-Menten transporter reuptake, plus dopamine, serotonin, acetylcholine, noradrenaline and oxytocin as global tones. Dopamine bursts and pauses follow reward prediction errors (Schultz 1997); noradrenaline raises excitatory gain on surprise; serotonin makes it patient; oxytocin tracks social feedback.
- **It learns the way brains do.** Three-factor STDP: pairing writes eligibility traces, dopamine decides whether they consolidate (Izhikevich 2007; Fremaux and Gerstner 2016). Homeostatic synaptic scaling keeps it stable (Turrigiano 1998). Dale's law is enforced and verified by tests that walk every synapse.
- **It exists between requests.** Self-sustained activity tuned to criticality (branching ratio 1.00 with discrete neuronal avalanches, verified by sweep), an episodic hippocampal store, compressed sharp-wave-ripple replay of salient episodes during rest (Wilson and McNaughton 1994), and a two-process sleep machine (Borbely 1982) with NREM slow oscillations, REM phases and dopamine-gated consolidation.
- **It wakes when someone walks in.** Event-camera-style vision (spikes only on change) plus predictive coding mean a quiet room costs nearly nothing; a sustained prediction error releases noradrenaline, opens the gate and wakes the brain with no command from anyone - verified end to end, with the person entering logged as a tagged memory.
- **It has subjective time.** An internal clock that dopamine and noradrenaline speed up (Treisman 1963; Meck 1996) plus a time-cell population that timestamps every memory with temporal context (Howard and Kahana 2002). Boredom and impatience are computed over subjective, not wall-clock, intervals - so time literally drags when it is under-stimulated.
- **Its cognition is measurable.** Neuronal avalanches, branching ratio, DMN/task-positive anti-correlation (Fox 2005), spike rasters and transmitter pools are first-class readouts. Ignition rate and perturbational complexity (PCI) land with the workspace milestone.
- **One metabolic budget rules everything.** Spikes, vesicle recycling, reuptake and even Meta API calls draw from a single energy ledger. Thinking costs energy. Talking costs more. Spending its cloud budget makes it tired.
- **Radically reproducible.** Same seed plus same input script gives a bit-identical trajectory (hash-verified, including checkpoint resume). Every scientific claim it makes can be re-run by a judge with one command.
- **Privacy by architecture.** Local-first: raw audio and video are never stored, only spikes and features; cloud calls are opt-in, cropped, rate-limited and fully logged, with a visible kill switch.
- **Runs keyless on a laptop.** Every external dependency (camera, mic, every Meta endpoint) sits behind an interface with a deterministic mock. `dotnet test` works with no keys, no hardware, no network.

## Use cases

- **Computational neuroscience testbed**: run classical paradigms (oddball/MMN, conditioning and extinction, delay discounting under serotonin, backward masking, attentional blink) in minutes on a laptop, with every spike logged, and compare against published results.
- **Psychology experiment prototyping**: design and pilot paradigms in silico before spending lab time on human subjects; export CSV and NWB-style JSON.
- **Artificial life research**: an organism with real ongoing existence, drives, boredom and self-initiated behavior, not a request-response loop.
- **Education**: a living, inspectable brain students can watch think, sleep and dream, with a literature citation on every parameter (see docs/plan/PARAMETERS-SEED.md).
- **Cognitive-architecture engineering**: a deterministic, checkpointable substrate for experimenting with global-workspace ignition, predictive processing and neuromodulation, at negligible cost.
- **Cost research for agentic AI**: the first agent whose cloud spending is a physiological variable - over-spend and it needs sleep.

## Quickstart

```bash
dotnet build BioComputational.sln
dotnet test                                        # fully offline: no keys, no camera, no mic
dotnet run --project src/BioLab -- resting 30s     # spontaneous activity at criticality
dotnet run --project src/BioLab -- day 240s        # one compressed day: wake, episodes, sleep, replay, wake
dotnet run --project src/BioLab -- tune 10s        # criticality sweep (noise x inhibition)
```

## Status and roadmap

Built Oct 27 - Nov 4, 2026 (daily log in DEVLOG.md):

- [x] Deterministic brain core: neurons, conductance synapses, transmitter pools, energy ledger
- [x] Neuromodulators: dopamine, serotonin, acetylcholine, noradrenaline, oxytocin
- [x] Dopamine-gated learning, homeostasis, bit-identical checkpoint/resume
- [x] Criticality-verified idle state, avalanche metrics, compressed day simulation
- [x] Episodic memory, replay with consolidation, two-process sleep (NREM slow oscillations, REM)
- [x] Event-based senses, predictive coding, wake-on-surprise (verified end to end with a mock camera; live webcam via --camera)
- [x] Global workspace with ignition and refractory, PCI perturbational complexity, self-model, feelings on the Russell circumplex, autonomous action portfolio
- [ ] Meta model cascade: SAM 3.1 segmentation, Muse Voice Transcribe, Muse Spark monologue, Muse Image dreams, Muse Wonder as an autonomous research agent
- [ ] Live dashboard: raster, transmitter bars, mood circumplex, ignition feed, monologue, dreams

## What was built during the hackathon (Oct 27 - Nov 4, 2026)

All repository content after the first commit was produced during the hackathon window. The first
commit (Oct 27) contains only the empty scaffold plus pre-event planning notes in `docs/plan/`
(architecture, parameter seed with citations, runbook), labeled as such in DEVLOG.md.

| Feature | Where | Landed |
|---|---|---|
| Seeded deterministic engine (xoshiro256**, trajectory hash) | src/BioCore/Rng, src/BioCore/Network | 85ff5d6, 937cfa6 |
| Izhikevich substrate, CSR synapses, Dale, delays, TM vesicles, receptor conductances, MM reuptake, energy ledger | src/BioCore/{Neurons,Synapses,Neuromodulators,Energy} | 937cfa6 |
| Five neuromodulator tones + dopamine-gated three-factor STDP + homeostasis | src/BioCore/{Neuromodulators,Learning} | c919bbc |
| Bit-identical checkpoint/resume | src/BioCore/Persistence | c919bbc |
| Avalanche/branching metrics, criticality tuning (biolab tune) | src/BioCore/Metrics, src/BioLab | c919bbc |
| Episodic memory, compressed replay, two-process sleep, idle controller, DMN/task-positive correlation | src/BioCore/{Memory,Replay,Sleep,Cognition} | 6135e0a |
| Senses: event vision, ERB cochlea, predictive coding, wake-on-surprise, interoception, subjective time | src/BioCore/{Senses,Abstractions,Cognition,Time}, src/BioSenses | 22dab4e |
| Test suite: 39 tests (determinism, Dale, STDP signs, checkpoint identity, sleep cycle, replay, episodes...) | tests/BioCore.Tests | all of the above |

## Documentation

- docs/plan/ARCHITECTURE.md - system design
- docs/plan/PARAMETERS-SEED.md - every parameter with its literature citation
- docs/plan/RUNBOOK.md - milestone plan
- TASKS.md - live workstream board
- LIMITATIONS.md (at submission) - what the proxies are and are not

## Ethics and privacy

Camera and microphone are opt-in and local-first; no raw audio or video is stored. Cloud calls only
on explicit opt-in, cropped to the triggering event, rate-limited, and logged (data/api_cost.jsonl).
A visible recording indicator and kill switch ship with the host. This project does not provide
medical, mental-health or diagnostic functionality.

## License

MIT - see LICENSE.
