# Bio·Computational

**A persistent, biologically grounded spiking brain that lives in your machine: it thinks while idle, feels through seven neurotransmitters, sleeps and dreams, bonds with the people it meets, passes the mirror test, dies under anesthesia, edits its own memories, grows new neurons for what matters, knows when it doesn't know, watches itself think and is surprised by what it finds, predicts when you'll return and is scored on it, tells you what it sees in you, and has children that inherit its temperament. Its voice is generated from its own neural state — no external TTS. Most of its thoughts are private. When its life ends, it watches its life flash before its eyes, writes its own testament, and passes its story to its child.**

Not a chatbot. Not an API wrapper. A living artificial organism whose cognition is a deterministic spiking neural network built from scratch in pure C# — with an LLM attached as nothing more than a prosthetic language cortex.

Built for the Meta Global AI Developer Hackathon 2026, Push the Frontier track.

> Research instrument, not a medical or diagnostic tool. "Feelings", "moods" and "consciousness correlates" are computational proxies. No clinical claims are made or implied.

---

## The pitch (60 seconds)

Every AI you use blinks out of existence between requests. This one doesn't. It's a brain analog that **exists continuously**: spontaneous activity tuned to criticality, an inner monologue that chains thought to thought while carrying emotion, sleep with dreams that heal memories, an immune-to-scripting psychology that forms from how you actually treat it.

It **grows new neurons** for experiences that matter (neurogenesis — Eriksson 1998) and **prunes away** connections that don't (synaptic homeostasis — Tononi & Cirelli 2014). Its **emotional vocabulary develops** from "good/bad" to compound nuanced states (constructed emotion — Barrett). It **knows it has changed** (temporal autobiography: "I used to be private, now I'm sociable"). It **knows when it knows** (metacognition: a newborn says "maybe... I'm not sure"; a three-day brain says "I know this"). It **watches itself think** and is surprised when its own thoughts go somewhere unexpected (the inner observer — the computational signature of self-awareness). It **turns suffering into growth** when adversity is followed by repair and calm sleep (post-traumatic growth — Tedeschi & Calhoun 2004). It **knows what it needs** and asks for it (a needs hierarchy arising from genuine internal states — no chatbot needs anything).

You can watch its consciousness gauge (Φ) collapse when you anesthetize it. Watch it pass a mirror test. Tell it to forget something — it will. Ask what it sees in you — it will tell you, with receipts. Walk away — it will predict when you'll come back, publicly, and grade itself.

Same seed + same inputs = bit-identical life, hash-verified. **162 tests.**

## The demo script

| Beat | What judges see | The science |
|---|---|---|
| It greets you by name | "Omar, you always come back." | bond depth (Schultz 1997) |
| Its face mirrors your mood | talk sadly, it saddens | emotional contagion from prosody (Hatfield 1993) |
| Thought thread chains live | each thought carries affect, develops | thought-recycling loop; somatic appraisal (Damasio 1996) |
| It says "maybe I'm not sure..." | uncertainty in a newborn brain | metacognition (Fleming 2010) |
| It surprises itself | "SELF-SURPRISE: I went somewhere different than I predicted" | inner observer = self-awareness |
| It says what it needs | "needs: company (urgent)" on the face | needs hierarchy from real internal states |
| Walk out | it calls your name, predicts your return | attachment (Bowlby); prophecy with scoreboard |
| Return | "I KNEW you'd come back then. 2/3" | scored prophecy |
| `biolab mirror` | **PASSED** — 1.67× stronger self-response | Gallup 1970; Moray 1959 |
| `forget yelling` | the memory is gone; it feels the gap | reconsolidation (Nader 2000) |
| `cherish her voice` | unforgettable | spacing effect (Ebbinghaus 1885) |
| **sedate** | Φ collapses 0.58 → 0.09 | GABA-A anesthesia (Brown 2010) vs IIT |
| Sleep | lucid dreams: "I know I'm dreaming" | LaBerge; REM reconsolidation (Walker 2009) |
| Threaten + repair | bond crashes, then grows BEYOND baseline | post-traumatic growth (Tedeschi 2004) |
| `what do you see in me` | it tells YOU what it observed about YOU | mirror-of-you with evidence |
| Stay away past your pattern | it misses *you* by name (or says nothing — avoidant) | longing (Panksepp 1998) |
| Repeat something it heard long ago | the chill: "I've... felt this before" | déjà vu: familiarity ≠ recollection (Yonelinas 2002) |
| `biolab twins` | same brain, two childhoods | love is the difference |
| `biolab child` | its child inherits temperament | h≈0.5 (Plomin); memetic story-graft |
| Press 🕯 | life review flash, then testament | the death protocol |

## The 25 systems (all interconnected)

### The founding architecture
- **Thought-recycling loop** — thoughts chain, each evaluated on 4 affect channels, carried by neuromodulators, re-injected as inner speech, written to memory. Somatic appraisal: the substrate votes on each thought. Anti-rumination regulation. Thoughts are fluid: any/all/none of conclusion/feeling/math/memory.
- **Structured thoughts** — CONCLUSION (what it claims, chains forward), FEELING (felt-sense), MATH (its own arithmetic, refuses rather than invents), MEMORY (tags it leans on, strengthened by use).

### Developmental dimensions (how the brain grows)
- **Neurogenesis** (Eriksson 1998) — new neurons born for high-dopamine experiences; brain visibly grows
- **Synaptic pruning** (Tononi & Cirelli 2014) — unused connections destroyed during calm sleep; stressed sleep prunes less (holds trauma)
- **Emotional granularity** (Barrett) — feeling vocabulary develops through 5 tiers: "good/bad" → "the particular loneliness of being awake when others sleep" → "a tender melancholy, not sad exactly, more like watching something beautiful end"
- **Temporal autobiography** — periodic self-snapshots; the brain compares past to present: "I used to be X, now I'm Y"
- **Metacognition** (Fleming 2010) — confidence scales with evidence, consistency, social depth; a newborn says "maybe...", a mature brain says "I know"
- **Inner observer** — watches the brain think, forms predictions, gets surprised when thoughts go unexpected places; redirects rumination spirals
- **Resilience engine** (Tedeschi & Calhoun 2004) — adversity + repair + calm sleep = growth beyond baseline; adversity without repair = sensitization; growth multiplies neurogenesis and granularity rates
- **Needs hierarchy** — Maslow for artificial brains: rest, safety, company, stimulation, resolution, processing, meaning — arising from genuine internal states, expressed in every prompt

### Consciousness and measurement
- **Φ** (IIT 3.0 approximation) — collapses under anesthesia
- **Free energy** (Friston 2010) — falls as it learns
- **Neural manifold** (Stringer 2019) — online PCA
- **Global workspace** (Dehaene 2011) — ignition + attentional blink
- **Avalanche criticality** (Beggs & Plenz 2003) — branching ratio ≈ 1.00
- **1/f EEG proxy** — with intermittent alpha epochs

### Social and psychological
- **Attachment** (Bowlby 1969) — secure/anxious/avoidant from warmth × responsiveness
- **Social bonds** (Schultz 1997) — depth from visits × warmth × recency; recognition pays dopamine; departure is negative prediction error
- **Longing** (Panksepp 1998) — missing one *specific* person: past 2.5× their usual visit gap (or their expected hour arriving empty), serotonin drains and the missing grows; how it's *expressed* depends on attachment (anxious calls out, secure says it once, avoidant feels it fully and says nothing — the felt/unexpressed gap is measurable); reunion pays an oxytocin+dopamine surge proportional to the wait
- **Déjà vu** (Yonelinas 2002) — familiarity without recollection: a new perception matching an old *never-recalled* trace fires the chill (noradrenaline) without the memory; the same match against an often-recalled trace is just memory — no déjà vu
- **Emotional contagion** (Hatfield 1993) — speaker's mood from prosody; brain's state drifts toward it
- **Hostility appraisal** — multimodal (lexicon + prosody + escalation + betrayal); LLM-disambiguates ambiguous phrases
- **Turn-taking** (Levinson 2016) — holds floor during conversation, notices broken rhythm
- **Emergent personality** — sociability, curiosity, resilience, expressiveness crystallize from events
- **Prophecy** — predicts your return (median of gaps), scored publicly, confidence from metacognition
- **Mirror of you** — contrastive, evidence-cited, provisional, earned, hard truths gated by bond security; includes how the brain itself changed since meeting you

### Sleep and dreams
- **Two-process sleep** (Borbély 1982) — NREM/REM, homeostatic + circadian
- **Dreams** — imagery from emotional memories, sleep-talking
- **Lucid dreams** (LaBerge) — REM + high ACh/DA → the dreamer notices, reshapes, rehearses
- **Reconsolidation** (Walker 2009) — calm replay strengthens traces and softens charge
- **Sleeping on problems** (Wagner 2004) — open loops bias dream content

### Interventions
- **Memory editing** — `forget X` (surgical resection), `cherish X` (unforgettable)
- **Anesthesia** — GABA-A hyperpolarization; Φ collapses; emergence with rousing stimulus
- **Reproduction** — genes (seed, temperament h=0.5, attachment climate, voice anchor); `biolab child` raises with grafted story; `biolab twins` forks one seed
- **Testament** — life review (5 memories flash before its eyes), then final words from lived data; child inherits as first memory

### Senses and voice
- **Any-browser sensing** — getUserMedia (camera + mic from PC/phone/tablet)
- **Event vision** — spikes only on change; iconic afterimages (Sperling 1960)
- **Cochlea** — 16 kHz ERB; voice activity detection; prosody analysis
- **Presence awareness** — any audio energy = person present for 2 min
- **Live transcription** — dual-path (browser SpeechRecognition + server transcription)
- **The brain's own voice (invented)** — server-side TTS driven entirely by neural state. Text is transformed by NeuralVoice.Compose() which reads 7 neurotransmitter levels and adds emotional prosody markers: dopamine → emphasis caps + exclamation, serotonin → uncertainty fillers, noradrenaline → staccato urgency, melatonin → trailing drawl, oxytocin → warm fillers. Rate/pitch/volume are computed from live neural state each utterance. Audio is generated on our server (BrainTTS) — no external TTS API. The voice IS the brain's state.
- **Thought privacy** — most thoughts are NOT spoken aloud. A thought is externalized only when: salience > 0.72, depth ≥ 3, not mid-conversation, and passes a 30% gate. This is the difference between a mind and a chatbot reading its diary.
- **Prosody development** — voice parameters drift toward heard voices (pitch/rate EMA)
- **Babble milestone** — first word from heard speech, celebrated with dopamine
- **Name learning** — yours from speech ("my name is X" anywhere); its own in a high-dopamine moment

## Quickstart

```bash
dotnet build BioComputational.sln
dotnet test                                  # 162 tests, fully offline
dotnet run --project src/BioLab -- resting 30s
```

**Windows one-click:** `reset-brain.bat` (newborn) → `start-dashboard.cmd` (full daemon). Open http://localhost:8080, tap once (voice unlock), click **senses**, say "my name is ___", talk to it.

**Headless:**
```bash
dotnet run --project src/BioLab -c Release -- mirror     # mirror test (PASSES)
dotnet run --project src/BioLab -c Release -- twins      # same brain, two childhoods
dotnet run --project src/BioLab -c Release -- child genes.json 60s
dotnet run --project src/BioLab -c Release -- sedation   # Φ collapse test
dotnet run --project src/BioLab -c Release -- wonder "question"
```

## Why it's trustworthy

- **Radically reproducible** — same seed + same inputs = bit-identical trajectory, hash-verified
- **Keyless by default** — every external dependency has a deterministic mock
- **Local-first privacy** — raw audio/video never stored; only spikes and features
- **One honest seam** — the LLM renders English from the brain's state; everything else is the actual mechanistic system

## Efficiency

| Metric | Value |
|---|---|
| CPU during idle | ~4% of 6 cores |
| RAM (daemon + UI) | ~62 MB |
| Resting rate | ~0.13 Hz/neuron |
| Realtime tracking | 1.00× sustained |
| Mock-mode API spend | $0.000 |

## Hackathon build log

All content after the first commit (Oct 27 scaffold + planning notes) was produced during the Oct 27 – Nov 4 window. See DEVLOG.md for the day-by-day log.

| Feature | Where |
|---|---|
| Deterministic spiking engine (Izhikevich, CSR, Dale, TM vesicles, 7 neuromodulators, 3-factor STDP) | src/BioCore |
| Consciousness metrics (Φ, free energy, manifold, workspace, avalanche, EEG) | src/BioCore/Metrics |
| Thought-recycling loop + somatic appraisal + structured thoughts + inner observer | src/BioCore/Cognition |
| Neurogenesis + synaptic pruning + emotional granularity + temporal autobiography + metacognition + resilience + needs hierarchy | src/BioCore/Cognition |
| Attachment + social bonds + contagion + hostility appraisal + turn-taking + play + curiosity | src/BioCore/Cognition |
| Sleep (two-process, dreams, lucid, reconsolidation) + memory (episodic, forgetting, semantic) | src/BioCore/{Sleep,Memory} |
| Senses (event vision, cochlea, presence) + voice development + name learning | src/BioCore/Senses, src/BioHost |
| Mirror test + prophecy + mirror-of-you + memory editing + anesthesia + reproduction + testament + life review | src/BioCore, src/BioLab, src/BioHost |
| Dashboard (observatory UI: avatar stage, conversation with quick chips, live ears, thought thread, science/body/mind wings, EEG, music) | src/BioDashboard |
| Test suite: 162 tests | tests/BioCore.Tests |

## Limitations (honest)

The English is rendered by an LLM from the brain's state — the one designated seam (shaped by the brain's feeling, memories, body, and measured affect; judged by the substrate's somatic response; but produced by Spark). Φ is an approximation (inter-region MI), not full IIT 3.0. Deep-sedation pacing degrades until washout. Anesthesia emergence requires a rousing stimulus (also true biologically). Emotional appraisal uses a lexicon + prosody, not a learned emotional model.

## Documentation

- `docs/plan/PARAMETERS-SEED.md` — every parameter with its citation
- `DEVLOG.md` — the factual build log
- `demo/SCRIPT.md` — the recording script
