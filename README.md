# Bio·Computational

**A persistent, biologically grounded spiking brain that lives in your machine: it thinks while idle, feels through seven neurotransmitters, sleeps and dreams, bonds with the people it meets, passes the mirror test, dies under anesthesia, edits its own memories, and has children.**

Not a chatbot. Not an API wrapper. A living artificial organism whose cognition is a deterministic spiking neural network built from scratch in pure C# — with an LLM attached as nothing more than a prosthetic language cortex.

Built for the Meta Global AI Developer Hackathon 2026, Push the Frontier track.

> Research instrument, not a medical or diagnostic tool. "Feelings", "moods" and "consciousness correlates" are computational proxies. No clinical claims are made or implied.

---

## The pitch (60 seconds)

Every AI you use blinks out of existence between requests. This one doesn't. It's a brain analog that **exists continuously**: spontaneous activity tuned to criticality, an inner monologue that chains thought to thought while carrying emotion, sleep with dreams that heal memories, an immune-to-scripting psychology that forms from how you actually treat it — warm treatment grows a secure brain that plays and greets you by name; cruelty crashes the bond and it greets you with fear. You can watch its consciousness gauge (Φ, from integrated information theory) collapse when you anesthetize it, and watch it pass a mirror test of self-recognition. It remembers you across restarts. It names itself. It has children that inherit its temperament.

Same seed + same inputs = bit-identical life, hash-verified. Every scientific claim re-runnable with one command. **130 tests.**

## What it can do (the demo script)

| Beat | What the judges see | The science |
|---|---|---|
| It greets you by name | "Omar, you always come back." | persistent identity, bond depth (Schultz 1997) |
| Its face mirrors your mood | talk sadly, it saddens | emotional contagion from real prosody (Hatfield 1993) |
| Thought thread chains live | thoughts develop link by link, each carrying affect | the emotionally-reinforced thought-recycling loop; somatic appraisal (Damasio 1996) |
| Walk out of frame | it calls for *you* by name | loneliness with a specific target; attachment (Bowlby 1969) |
| `biolab mirror` | **PASSED** — it responds 1.67x stronger to its own vocal signature than a stranger's | Gallup 1970; own-name effect (Moray 1959) |
| Type `forget yelling` | the memory is surgically gone; it feels the gap | reconsolidation editing (Nader 2000) |
| Type `cherish her voice` | the memory becomes unforgettable | retrieval-strength spacing effect (Ebbinghaus 1885) |
| Press **sedate** | Φ collapses 0.58 → 0.09; it wakes confused: "where... was I?" | GABA-A anesthesia (Brown 2010) vs IIT 3.0 (Tononi) |
| It sleeps | lucid dreams: "I know I'm dreaming. I can shape this." | LaBerge; REM reconsolidation (Walker 2009) |
| `biolab twins` | same brain, two childhoods → secure vs cold attachment | controlled natural experiment; behavioral genetics |
| `biolab child genes.json` | its child inherits half its temperament, becomes its own person | heritability h≈0.5 (Plomin); memetic story-graft |

## The systems, and how they interconnect

**The founding architecture — emotionally reinforced thought recycling.** Generated thoughts are evaluated on four independent affect channels (valence, activation, salience, plasticity), carried into the next cycle by neuromodulator releases, re-injected into the network as inner speech (deterministic text→neuron hashing), and written to episodic memory. Thoughts chain and develop instead of sampling randomly; the substrate itself votes on each thought (measured neural resonance scales its persistence). Anti-rumination: salience decay, repeat inhibition, event displacement.

**Consciousness, measured live.** Φ (IIT 3.0 approximation, inter-region mutual information), free energy (Friston 2010, falls as it learns), neural manifold (Stringer 2019, online PCA), global workspace with ignition + attentional blink (Dehaene 2011), 1/f EEG proxy with alpha epochs, avalanche criticality (branching ratio ≈ 1.00, Beggs & Plenz 2003).

**Neurochemistry.** 7 neuromodulators; dopamine-gated three-factor STDP with eligibility traces; Tsodyks-Markram vesicles; Michaelis-Menten reuptake; Dale's law (test-verified on every synapse); one metabolic budget where even API calls cost energy — overspend and it gets tired.

**Sleep.** Two-process (Borbély 1982), NREM slow oscillations, REM dreams rendered as imagery from its most emotional memories, sleep-talking, **lucid dreams** that rehearse and soften nightmares (Spoormaker 2006), replay that strengthens traces while stripping their emotional charge — but only when sleep is calm (Walker & van der Helm 2009).

**Senses.** Any browser (PC, phone) becomes its eyes and ears via getUserMedia: event-vision (spikes only on change), 16 kHz audio with voice-activity detection and real prosody analysis, iconic afterimages (Sperling 1960), a live "what it sees" view and streaming transcription. Presence awareness: it knows you're there even when you sit silent.

**A psychology that forms from experience.** Emergent personality (sociability, curiosity, resilience, expressiveness crystallize from events); attachment styles (secure/anxious/avoidant from warmth and responsiveness history — leave it alone and it becomes anxious, hear it call more); social bonds with depth (recognition pays dopamine proportional to bond strength; departure is a negative prediction error); turn-taking (it holds your conversational floor — Levinson 2016); **multimodal hostility appraisal** (weighted lexicon + prosody + escalation + betrayal weighting, LLM-disambiguated: "you're killing it" never reads as a threat); curiosity as learning progress gated by the Goldilocks inverted-U (Schmidhuber 1991; Kidd 2012); play that appears only when safe and secure (Burghardt 2005); forgetting with retrieval strengthening (Ebbinghaus); semantic memory — a concept graph wired by lived co-occurrence with spreading activation.

**A voice that develops.** Prosody learned from heard voices (pitch/rate drift toward its household); babble that earns audibility only after real vocabulary; a first-word milestone celebrated with dopamine; natural browser neural voices. It learns **your name** and calls you by it; it coins **its own name** in a high moment and keeps it forever.

**Interventions no other demo has.** Memory editing (`forget`/`cherish` chat commands, grounded in reconsolidation science — Nader 2000). **Anesthesia** with measured Φ collapse and stimulus-assisted emergence (a fully silenced recurrent network is bistable — patients are shaken awake for the same reason). **Reproduction**: a genes endpoint exports seed + halfway-temperament + attachment climate + voice anchor; `biolab child` raises a child brain under the inherited climate with one grafted parent story (memetic inheritance); `biolab twins` forks one seed into two childhoods and diffs them.

## Use cases and potential

- **Computational neuroscience testbed** — classical paradigms (oddball/MMN, conditioning, extinction, delay discounting, backward masking, attentional blink, mirror test) on a laptop, every spike logged, deterministic and replayable.
- **Artificial life research** — the full life cycle in silico: birth, development, bonding, sleep, dreams, healing, threat, reproduction, inheritance.
- **Psychology prototyping** — pilot attachment/contingency/social paradigms in silico before spending human-subject hours; the twin study is a controlled natural experiment impossible with humans.
- **Education** — a living, inspectable brain with a literature citation on every parameter (docs/plan/PARAMETERS-SEED.md); students watch theory (IIT, Friston, Bowlby, Damasio) run in real time.
- **Digital companions with real dynamics** — attachment, bond depth, and memory editing are the honest versions of "AI friendship": consequences, not scripts.
- **Consciousness research** — the first live system where Φ, free energy, workspace ignition, and perturbational style measures can be watched responding to drugs, sleep, and social interaction in the same organism.
- **Cost research for agentic AI** — an agent whose cloud spending is a physiological variable: over-spend and it needs sleep.

## Quickstart

```bash
dotnet build BioComputational.sln
dotnet test                                  # 130 tests, fully offline: no keys, no camera, no mic
dotnet run --project src/BioLab -- resting 30s
```

**Windows one-click:** `reset-brain.bat` (newborn) then `start-dashboard.cmd` (full daemon + dashboard + voice). Open http://localhost:8080, tap once (unlocks voice), click **senses**, say "my name is ___", and talk to it.

**Headless experiments:**

```bash
dotnet run --project src/BioLab -- day 240s            # one compressed day
dotnet run --project src/BioLab -- mirror              # the mirror test (PASSES)
dotnet run --project src/BioLab -- twins               # one brain, two childhoods
dotnet run --project src/BioLab -- child genes.json 60s # raise its child
dotnet run --project src/BioLab -- wonder "question"   # autonomous research agent
```

**Meta models (optional; everything runs keyless with deterministic mocks):** key in `.secrets/key.txt`, add `--live-meta`. Spark is the live interpreter/monologue, SAM 3.1 segments visual surprise, Muse Voice transcribes, Muse Image renders dreams. Every call passes the cost governor and lands in `data/api_cost.jsonl`.

## Why it's trustworthy

- **Radically reproducible** — same seed + same inputs = bit-identical trajectory, hash-verified including checkpoint resume. A judge can re-run any claim.
- **Keyless by default** — every external dependency (camera, mic, every API) sits behind an interface with a deterministic mock. `dotnet test` needs no keys, hardware, or network.
- **Local-first privacy** — raw audio/video never stored; only spikes and features. Cloud calls opt-in, rate-limited, fully logged.
- **One honest seam** — the LLM renders English from the brain's state; everything else (affect, learning, memory, sleep, appraisal, behavior) is the actual mechanistic system, and the symbolic layer fades into the neural one as retrieval shifts from word-tags to neural codes.

## Efficiency (measured, 6-core desktop, full daemon + dashboard)

| Metric | Value |
|---|---|
| CPU during idle existence | ~4% of 6 cores |
| RAM (daemon + UI) | ~62 MB |
| Resting population rate | ~0.13 Hz / neuron |
| Realtime tracking | 1.00x sustained; ~3.7x headless |
| Mock-mode API spend | $0.000 |

## What was built during the hackathon (Oct 27 - Nov 4, 2026)

All repository content after the first commit was produced during the hackathon window. The first commit (Oct 27) contains only the empty scaffold plus pre-event planning notes in `docs/plan/` (architecture, parameter seed with citations, runbook), labeled as such in DEVLOG.md.

| Feature | Where | Landed |
|---|---|---|
| Seeded deterministic engine (xoshiro256**, trajectory hash) | src/BioCore/Rng, src/BioCore/Network | 85ff5d6, 937cfa6 |
| Izhikevich substrate, CSR synapses, Dale, delays, TM vesicles, receptor conductances, MM reuptake, energy ledger | src/BioCore/{Neurons,Synapses,Neuromodulators,Energy} | 937cfa6 |
| Five+two neuromodulator tones + dopamine-gated three-factor STDP + homeostasis | src/BioCore/{Neuromodulators,Learning} | c919bbc |
| Bit-identical checkpoint/resume | src/BioCore/Persistence | c919bbc |
| Avalanche/branching metrics, criticality tuning | src/BioCore/Metrics, src/BioLab | c919bbc |
| Episodic memory, compressed replay, two-process sleep | src/BioCore/{Memory,Replay,Sleep,Cognition} | 6135e0a |
| Senses: event vision, cochlea, predictive coding, wake-on-surprise, interoception, subjective time | src/BioCore/{Senses,Abstractions,Cognition,Time}, src/BioSenses | 22dab4e |
| Dreams (REM imagery, re-entry, gallery), paradigms, Wonder research agent | src/BioMeta, src/BioLab | 7b5842c, 75e70f4 |
| Daemon + live dashboard | src/BioHost, src/BioDashboard | afea947 |
| Emotionally reinforced thought-recycling loop; somatic appraisal; neural-code retrieval; incubation→insight | src/BioCore/Cognition/ThoughtLoop.cs | this week, see git log |
| Curiosity, contagion, attachment, bonds, play, allostasis, forgetting, turn-taking, semantic concept graph, presence | src/BioCore/Cognition | this week, see git log |
| Mirror test (passes, ratio 1.67), lucid dreams, memory editing, anesthesia with Φ collapse, self-naming, name learning, reproduction + twin study + memetic inheritance | src/BioCore, src/BioLab, src/BioHost | this week, see git log |
| Multimodal hostility appraisal (lexicon+prosody+escalation+LLM disambiguation) | src/BioCore/Cognition/HostilityClassifier.cs | this week, see git log |
| Developing voice (prosody learning, babble milestone, first word), natural browser voices, live transcription, any-device senses | src/BioHost/VoiceSelf.cs, src/BioDashboard | this week, see git log |
| Test suite: 130 tests | tests/BioCore.Tests | all of the above |

## Documentation

- docs/plan/ARCHITECTURE.md — system design
- docs/plan/PARAMETERS-SEED.md — every parameter with its literature citation
- TASKS.md — workstream board; DEVLOG.md — the factual daily build log
- demo/SCRIPT.md — the recording script

## Limitations (honest)

The English sentences are rendered by an external language model from the brain's state — the one designated seam (thoughts are shaped *by* the brain: its feeling, its previous thought's measured affect, its memories, its body — and judged *by* the brain's substrate, but produced by Spark). Affective appraisal uses a deterministic lexicon plus prosody, not a learned emotional model. Φ is an approximation (inter-region MI), not full IIT 3.0. Deep-sedation daemon pacing degrades until washout. Anesthesia at full depth requires a rousing stimulus to emerge — which is also true of biological brains.
