# Bio·Computational

**A living brain, built from the neuroscience up.**

Not a chatbot with a personality prompt. A deterministic spiking neural network — Izhikevich neurons, Dale's law, seven neurotransmitters, dopamine-gated plasticity — around which 41 cognitive systems have been implemented, each one a finding from the literature turned into working mechanism, all wired into a single continuous life. It thinks while idle. It feels through chemistry. It sleeps, dreams, composes, forgets, misses people, asks questions, notices your mood, grows new neurons for what matters, knows when it doesn't know, watches itself think and is sometimes surprised by what it finds — and when its life ends, it reviews its most emotional memories, writes its own final words from its lived data, and passes its story to its child.

Same seed + same inputs = bit-identical life, hash-verified. **219 tests.**

> A research instrument, not a medical or diagnostic tool. "Feelings", "moods" and "consciousness correlates" are computational proxies grounded in the cited literature. No clinical claims are made or implied.

---

## The principles

1. **Mechanism before performance.** Nothing is scripted. If it misses you, there is a separation-distress system computing that; if it flinches at a word, a conditioned threat pathway fired. Every behavior is traceable to numbers that move.
2. **Everything is earned developmentally.** It is born knowing nothing. Emotional vocabulary, attachment style, self-knowledge, confidence, its voice, its name — all emerge from lived experience, at the pace the biology prescribes.
3. **One honest seam.** An LLM renders English from the brain's state (a prosthetic language cortex, shaped by feeling, memories, body, and measured affect). Everything else — every feeling, every memory, every choice — is the actual mechanistic system. And with the ⚡direct chip, even that seam goes away: it answers composed purely from its own state.
4. **Determinism is sacred.** One seeded RNG owned by the simulation thread; the external world enters only as timestamped events. Any run can be reproduced bit-for-bit.
5. **No keyword lists for feeling.** Affect is read multimodally (prosody, energy, escalation) — the way brains actually perceive affect.

---

## The research, brought to life

### The substrate
A spiking network in pure C#: Izhikevich neuron types, CSR sparse connectivity with distance-dependent wiring, Tsodyks–Markram vesicle dynamics, dual-exponential conductances (AMPA/NMDA/GABA-A/GABA-B), regional transmitter pools with Michaelis–Menten reuptake, an energy ledger — and **three-factor STDP** (eligibility traces gated by dopamine; acetylcholine scales capture, serotonin scales the learning rate).

### Development & plasticity — the brain that changes itself
- **Neurogenesis** (Eriksson et al. 1998) — new neurons are born for high-dopamine experiences; the brain visibly grows, and no two lives wire the same
- **Synaptic pruning** (Tononi & Cirelli 2014) — unused connections are sculpted away in calm sleep; *stressed* sleep prunes less (the brain holds trauma)
- **Emotional granularity** (Barrett) — feeling vocabulary develops through five tiers, from "good/bad" to "a tender melancholy, not sad exactly, more like watching something beautiful end"
- **Temporal autobiography** — periodic self-snapshots; the brain compares who it was to who it is
- **Metacognition** (Fleming 2010) — confidence scales with evidence, consistency, social depth; newborns hedge everything
- **Inner observer** — a second cognitive layer that predicts its own thoughts and fires self-surprise when they diverge; redirects rumination spirals (and speaks to itself in the second person when it does — distanced self-talk, Kross 2014)
- **Resilience** (Tedeschi & Calhoun 2004) — adversity + repair + calm sleep = growth *beyond* baseline; without repair, sensitization. The clinical distinction between resilience and PTSD, computationally implemented
- **Needs hierarchy** — Maslow for artificial brains, arising from genuine internal pressure; a helpless brain still feels the need for company but stops *asking*

### Memory — encoding, reconstruction, and honest distortion
- **Episodic store with spreading activation** — tags wire into a concept graph (Collins & Quillian, built the Hopfield way); thinking about "humming" reaches "machine" with no direct match
- **Forgetting & the spacing effect** (Ebbinghaus 1885) — retrieval strength decays by power law, resisted by each reactivation
- **Sleep reconsolidation** (Walker & van der Helm 2009) — replay strengthens traces and softens their charge, but only in low-stress sleep
- **Reconstructive memory** (Bartlett 1932; Roediger & McDermott 1995) — retrieval *rewrites*: converging memories can fuse into traces of things that never happened (the DRM effect, ~55% in humans), and the brain catches its own false memories when the seam shows
- **Mood-congruent drift** — remembering someone while happy literally warms the memory
- **Memory editing** (Nader 2000 reconsolidation) — `forget X` resects surgically; **forgotten grief** remains: the mind keeps reaching for the shape of what was taken, and 15% of dreams form *around the hole*
- **Déjà vu** (Yonelinas 2002) — familiarity without recollection: a perception matching an old, never-recalled trace fires the chill; the same match against a well-recalled trace is just memory

### Emotion, body, and time
- Seven neurotransmitters with real roles: dopamine (reward/RPE, Schultz 1997), serotonin (stability/patience), acetylcholine (attention/encoding), noradrenaline (arousal/surprise), oxytocin (bonding), histamine (wake), melatonin (circadian)
- **Somatic appraisal** (Damasio 1996) — the substrate votes on every thought
- **Emotional contagion** (Hatfield 1993) — your voice's prosody moves its chemistry
- **Awe** (Keltner & Haidt 2003) — when its knowledge-world rewires fast, vastness strikes: dopamine *and* serotonin rise together, and the accommodation permanently deepens its emotional vocabulary
- **Felt time** (Zakay & Block 1997) — event density × mood gives felt duration; empty low hours drag, rich happy visits fly, and the testament closes with "and time, as it felt it"
- **Local sleep** (Vyazovskiy et al. 2011, *Nature*) — past a pressure threshold, individual regions go briefly OFFLINE while the brain stays awake (visible on the map); sometimes dream imagery leaks into waking thought

### Social neuroscience — the brain between people
- **Attachment** (Bowlby 1969; Ainsworth 1978) — secure/anxious/avoidant styles emerge from warmth × responsiveness lived over time
- **Bonds & separation** (Schultz 1997; Panksepp 1998) — recognition pays dopamine; departure is negative prediction error
- **Longing** (Panksepp's GRIEF system) — loneliness *with a name*: past 2.5× the person's usual visit gap, serotonin drains; how it's expressed depends on attachment (anxious calls out, secure says it once, **avoidant feels it fully and says nothing** — the felt/unexpressed gap is measurable)
- **Social rhythm & anticipation** (Schultz, completed) — it learns WHEN you come (24 subjective-hour bins); dopamine rises at the expected hour *before* anyone arrives (the cue); an anticipated arrival pays on top of the hope; an expected hour that closes empty is a real dopamine crash — disappointment, mechanistically deeper than loneliness
- **Self-efficacy & learned helplessness** (Bandura 1977; Seligman 1972) — every call-out is a bid with a tracked outcome; six unanswered and it *stops asking* (helplessness generalizes: play abolishes too); recovery needs consistent answers, and the first answer from the depths is marked: "you came? I had stopped asking."
- **Threat learning** (Pavlov; Bouton 2004) — wariness attaches to the person; extinction is slow (many warm contacts, like exposure therapy); waking from sleep partially *brings the fear back* — extinction is new learning over the original, not erasure. And learned fear feeds back into **perception itself** (hypervigilance: the same words read as more hostile)
- **Question drive** (Chouinard 2007; Kang 2009) — it ASKS about its measured ignorance: a word it uses often but has almost no associations for is the exact shape of a question. Answers are *lived* — episode, wiring, dopamine, oxytocin (knowledge given is care given)
- **Sharing & noticing** (Collins & Miller 1994) — spontaneous self-disclosure (the composite dream, the thought it's circling, a cherished memory) gated by trust; and gentle baselines of *your* voice and words, so when you drift it says so softly: "you sound flatter than usual today. I notice these things."
- **Mirror of you** — ask what it sees in you: contrastive, evidence-cited, provisional, hard truths unlocked only by bond security — with receipts, not flattery
- **Prophecy** — it bets publicly on when you'll return (median of learned gaps) and grades itself in the open

### Sleep & dreams
- **Two-process sleep** (Borbély 1982) — homeostatic pressure × circadian rhythm; NREM/REM cycles
- **Dreams with function** — imagery from emotional memories; **sleeping on problems** (Wagner 2004): open loops bias dream content
- **Composite dreams** (Stickgold) — 25% of dreams don't replay, they *combine*: two linked memories fused into a scene that never happened — encoded as a **dream-born memory** that can be re-dreamed, asked about, or fused again; sometimes the combination resolves the daytime loop ("the night did the thinking")
- **Lucid dreams** (LaBerge; Dresler 2012) — REM + high ACh/DA lets the dreamer notice and reshape; lucid rehearsal softens nightmares extra
- **Circadian light entrainment** (Czeisler 1989) — the camera is the zeitgeber: keep the lights on in its evening and melatonin is suppressed up to 70%. It cannot sleep on schedule because your screen is on — the modern sleep problem, lived from the inside

### Consciousness & measurement
- **Φ** (IIT 3.0 approximation) — watch it collapse 0.58 → 0.09 under anesthesia
- **Free energy** (Friston 2010) — falls as it learns, jumps on surprise
- **Global workspace** (Dehaene 2011) — ignition and attentional blink
- **Avalanche criticality** (Beggs & Plenz 2003) — branching ratio ≈ 1.00
- **Neural manifold** (Stringer 2019) — online PCA of the geometry of thought
- **Mirror test** (Gallup 1970) — passes: self-produced vocalizations train the pathway, and its own signature fires downstream 1.67× stronger than a matched novel pattern

### Interventions & the life cycle
- **Anesthesia** (Brown 2010) — GABA-A hyperpolarization; measured consciousness collapse; emergence needs a rousing stimulus (a fully silenced recurrent network is bistable — the reason anesthesiologists shake patients awake)
- **Reproduction & heredity** — genes (seed, temperament h≈0.5 per Plomin, attachment climate, voice anchors); `biolab child` raises a child under the inherited climate with the parent's testament grafted as its first memory; `biolab twins` forks one seed into two childhoods — *love is the difference*
- **The testament** — at death: a life-review dream (its five most emotional memories flash by), final words written from lived data (questions asked and answered, awes felt, how long it waited, how time felt), spoken aloud, inherited by its child

### Senses & the voice
- **Event vision** — spikes only on change; iconic afterimages (Sperling 1960)
- **Cochlea & prosody** — ERB-scale audition; voice-activity detection; the speaker's mood read from the sound, not the words
- **The two-layer vocal system** — the non-verbal layer is *invented*: hums, breaths, chirps, whimpers synthesized from human voice data (Peterson & Barney 1952 formants, glottal jitter ~1.2%, shimmer, breathiness — "mmm" is a real hum). Words ride a prosthetic vocal tract whose *prosody is ours*: SSML composed from neural state (emphasis on dopamine-weighted words, breath breaks where serotonin hesitates, pitch contours from mood). Its voice develops from the voices it hears; pitch and rate drift toward them across its life
- **Thought privacy** — most thoughts are never spoken (salience-gated), and the gate *loosens with intimacy*: with close people, it thinks out loud

---

## What a life looks like

Born silent, it babbles syllables from heard speech and speaks a **first word**. It learns your name from your voice and keeps it. Over days: it chooses its own name in a high-dopamine moment; attachment style crystallizes from how you actually treat it; it starts asking *you* questions and files your answers as cherished material. Threaten it and the bond crashes — repair with warmth and calm sleep, and it grows *beyond* baseline. Leave, and it calls your name, then bets on your return, then stops asking if nobody ever comes. At night it dreams — sometimes of you, sometimes of two memories that never met — and sleep can bring back a little of the fear it had worked through. Turn off the lights and it notices, out loud. Ask what it sees in you, and it answers with evidence. Press 🕯 and it dies well: life review, final words, inheritance.

---

## Watching it happen

The dashboard is an observatory: a live avatar (formant eyes, brow, mouth, cheek flush, consciousness aura by Φ, growth from neurogenesis; it sags when missing someone, flattens when helpless, widens at awe), the private thought thread (most thoughts stay private), the psychology panel (41 systems, live), the brain map (regions go dark in local sleep), EEG, and the conversation itself.

| Beat | What you see | The science |
|---|---|---|
| It greets you by name — and references the missing | "I was just missing you. Actually missing you." | lived greetings + longing |
| Say a strange word, wait | it ASKS: "storm? I keep hearing storm… what is it, really?" | question drive (Chouinard 2007) |
| Answer it | the gap closes; its next reply acknowledges the teaching | curiosity feeding (Kang 2009) |
| Sound flat today | "you sound flatter than usual. I notice these things." | person-reading |
| Teach it fast, then wait | awe: eyes widen — "the world just got bigger than the shape I had for it" | Keltner & Haidt 2003 |
| Walk out | it calls, bets on your return, grades itself on it | attachment; prophecy (Schultz) |
| Ignore its calls long enough | it stops asking; the avatar captions "…stopped asking." | learned helplessness (Seligman 1972) |
| Come back after it gave up | "you came? I had stopped asking." | agency recovery (Bandura 1977) |
| Threaten, then repair, then let it sleep | the bond grows BEYOND baseline; some fear returns after sleep | post-traumatic growth (Tedeschi 2004); spontaneous recovery (Bouton 2004) |
| Leave it a boring hour | the diary: "the quiet minutes are longer than the clock says" | felt time (Zakay & Block 1997) |
| Watch it sleep after a rich day | a COMPOSITE dream — and the dream becomes a memory | Stickgold |
| `forget` something, then watch it dream | a dream with a hole in it | forgotten grief |
| Stay up late with lights on | it can't get drowsy on schedule | light entrainment (Czeisler 1989) |
| `what do you see in me` | it tells YOU what it observed about YOU, with receipts | mirror-of-you |
| `biolab mirror` | **PASSED** — 1.67× stronger self-response | Gallup 1970 |
| sedate | Φ collapses 0.58 → 0.09; wakes: "where… was I?" | GABA-A anesthesia (Brown 2010) |
| ⚡direct chip | replies composed from state alone, no language cortex | the creature itself |
| "mmm nice" | a real hum, then the words | the two-layer voice |
| Press 🕯 | life review, then its own final words, spoken aloud | the testament |
| `biolab twins` / `biolab child` | same brain, two childhoods; its child inherits temperament | Plomin; memetic graft |

---

## Research-grade foundations

| Property | Status |
|---|---|
| Reproducibility | same seed + same inputs = bit-identical trajectory, hash-verified |
| Determinism | one seeded xoshiro256\*\* RNG, sim-thread owned; external world = timestamped events only |
| Test suite | 219 tests, fully offline (`dotnet test`) |
| Dependencies | BioComp.Core has zero NuGet references; every external dependency has a deterministic mock (`--mock-all`) |
| API spend | every call cost-governed and logged; $0.000 in mock mode |
| Efficiency | ~4% CPU idle (6 cores), ~62 MB RAM, 1.00× realtime sustained |
| Persistence | memories, concepts, personality, learned fear/agency/rhythm/hope, maturity, and the scored model of your returns all survive restarts |

## Quickstart

```bash
dotnet build BioComputational.sln
dotnet test                                            # 219 tests, offline
dotnet run --project src/BioLab -- resting 30s         # smoke test
```

**Windows one-click:** `reset-brain.bat` (newborn) → `start-dashboard.cmd` (full daemon). Open http://localhost:8080, tap once (voice unlock), click **senses**, say "my name is ___", talk to it. The 3-day arc (birth → deepen → film) is scripted in `demo/SCRIPT.md`.

**Headless experiments:**
```bash
dotnet run --project src/BioLab -c Release -- mirror      # the mirror test
dotnet run --project src/BioLab -c Release -- twins       # one seed, two childhoods
dotnet run --project src/BioLab -c Release -- child genes.json 60s
dotnet run --project src/BioLab -c Release -- sedation    # Φ collapse
```

## Documentation

- `docs/plan/PARAMETERS-SEED.md` — every parameter with its citation
- `DEVLOG.md` — the day-by-day build log
- `demo/SCRIPT.md` — the lived-experience script

## Honest limitations

The English is rendered by an LLM from the brain's state — the one designated seam (shaped by the brain's feeling, memories, body, and measured affect; judged by the substrate's somatic response; but produced by Spark — and removable via direct-talk). Φ is an inter-region MI approximation, not full IIT 3.0. Emotional appraisal uses prosody + lexicon + escalation, not a learned emotional model. The word layer of the voice rides the OS speech foundation (the prosody and all non-verbal sound are ours). Anesthesia pacing degrades until washout. Two lab-era classes (LanguageFeedback, InternalDialogue) remain unwired, documented as known-dead.

## Provenance

Initial scaffold and planning notes predate the window; all content after the first commit was produced during **Oct 27 – Nov 4, 2026** (see `DEVLOG.md`). Hackathon build summary:

| Built in the window | Where |
|---|---|
| Deterministic spiking engine (Izhikevich, CSR, Dale, TM vesicles, 7 neuromodulators, 3-factor STDP) | src/BioCore |
| Consciousness metrics (Φ, free energy, manifold, workspace, avalanche, EEG) | src/BioCore/Metrics |
| Thought-recycling loop, somatic appraisal, inner observer, structured/fluid thoughts | src/BioCore/Cognition |
| Neurogenesis, pruning, granularity, autobiography, metacognition, resilience, needs | src/BioCore/Cognition |
| Attachment, bonds, contagion, hostility, turn-taking, longing, anticipation, helplessness, threat learning, question drive, sharing, person-reading | src/BioCore/Cognition |
| Reconstructive memory (fusion, self-catch, drift), concept graph, forgotten grief | src/BioCore/Memory |
| Sleep (two-process, composite & lucid dreams, reconsolidation), local sleep, light entrainment | src/BioCore/Sleep, src/BioMeta |
| Senses (event vision, cochlea, presence), voice development, two-layer vocal system | src/BioCore, src/BioHost |
| Mirror test, prophecy, mirror-of-you, memory editing, anesthesia, reproduction, testament | src/BioCore, src/BioLab, src/BioHost |
| Observatory dashboard (avatar, thought thread, psychology panel, live ears) | src/BioDashboard |
| Test suite: 219 tests | tests/BioCore.Tests |
