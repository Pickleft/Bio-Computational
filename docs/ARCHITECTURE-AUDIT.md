# Architecture Audit — integrated cognitive architecture, or mechanisms + orchestration?

This document answers, at the source level, the central structural question about
Bio·Computational: **is this a genuinely integrated cognitive architecture, or a
collection of neuroscience-inspired mechanisms connected through an orchestration
layer?**

The honest answer: **it is a hybrid, and the boundary is exact.** Below is the
inventory that proves it — every closed loop where one system *changes another
system's parameters or state* (architecture), and every place where the connection
is information flow into a language model or a UI (orchestration). No third
category exists in the codebase.

## 1. The closed parametric loops (the architecture)

These are the wires that make the claim: system A does not merely *inform* system
B — it modifies B's mechanism, threshold, gain, structure, or weights. Each is
cited to its commit and is unit-tested.

| # | Loop (A → B, and what actually changes in B) | Mechanism class |
|---|---|---|
| 1 | Resilience.GrowthLevel → Neurogenesis.DopamineThreshold (`0.65/growth`) | parameter rewrite |
| 2 | Resilience growth + Observer self-surprises → Granularity episode-factor (×) | input gain |
| 3 | Awe strikes → Granularity.AccumulatedBoost (permanent, capped, persisted) | state accumulation |
| 4 | Anticipation crash → Longing intensity floor (0.5) | state override |
| 5 | Efficacy.MotivationMultiplier → vocalization probability AND play gate (abolishes play) | behavioral gating |
| 6 | Attachment.CallOutMultiplier × Efficacy → bid rate; attachment style → question boldness (1.6×/0.5×) | gain composition |
| 7 | Threats.Wariness → HostilityClassifier vigilance input (perception biased by learning) | perceptual gain |
| 8 | **SelfCalibration.VigilanceScale → the same vigilance gain** (the organism tunes its own perception from measured false-alarm/miss error; clamped 0.5–1.5, persisted) | **self-modification** |
| 9 | Longing + Wariness + Light → SleepController.MelatoninSuppression (melatonin target ×) | homeostatic gain |
| 10 | Sleep state transitions → synaptic weights (pruning scale 0.98/0.99; stressed sleep prunes less) | weight plasticity |
| 11 | Dopamine gates → STDP eligibility (three-factor learning) | weight plasticity |
| 12 | Computed concept co-occurrence → ConceptGraph links (learned associations rewired by experience) | structural |
| 13 | **HypothesisLoop verdicts → ConceptGraph links** (confirmed guesses wired, refuted unwound; the organism's guesses change its own knowledge structure) | **structural self-modification** |
| 14 | Composite dreams → open-loop resolution (ResolveLoop) + dream-born episodes entering the store | state transition |
| 15 | FeltTime density → Needs.Stimulation threshold (boredom defined by lived sparsity) | threshold |
| 16 | Wariness/Helplessness → Needs candidates (Safety beyond NA; Company-with-empty-Ask) | candidate injection |
| 17 | Safe contacts → Threats.Wariness extinction; wake transitions → spontaneous recovery (partial return toward peak) | state dynamics |
| 18 | Light EMA → melatonin; Rhythm expectation → ACh/histamine/melatonin release | neurochemical gain |
| 19 | _replyPending timestamp → ALL speech paths (one-response invariant) | scheduling law |
| 20 | Ghost-verdict history → escalation EMA → classification level | input gain |

Loops 8 and 13 are the ones that answer the recommendation most directly: the
organism modifies **its own perceptual gain** from measured error, and **its own
knowledge structure** from self-generated, self-tested hypotheses — with
measurable validation (the EMA converges only on evidence), stability constraints
(the clamps ARE the rollback), and persistence of the tuned state.

## 2. The orchestration layer (honest inventory)

- **The language seam**: 7 prompt-assembly sites feed one LLM (Spark) that renders
  English from brain state. The brain's state shapes the prompt, and the
  substrate's somatic response judges the result — but the words are prosthetic.
  The ⚡direct chip removes this seam entirely (NativeReplies composes from state).
- **The dashboard** reads a snapshot; it writes back only through the same event
  queue as every other sense (determinism preserved).
- **Interpreter disambiguation** (hostile-vs-playful) is an LLM call — but its
  verdict now FEEDS SelfCalibration, so even this seam tunes the mechanism.

## 3. Verdict

The **mechanistic core** — spiking substrate, plasticity, sleep, memory,
neurochemistry, and the 20 closed loops above — is an integrated architecture:
systems change each other's parameters, gains, thresholds, and structures, and
the trajectory is bit-reproducible. The **language surface** is orchestration,
by design, and is now self-tuning and optional. The claim we make is exactly
this and nothing more.

## 4. EVOCOG alignment

| EVOCOG section | Status here |
|---|---|
| §1 Individual organism (adaptive architecture, autobiography, world model, curiosity, metacognition, development) | **Present**: developmental dimensions, TemporalSelf, concept-graph world model, CuriosityModule, Metacognition |
| §2 Evolution of cognitive architectures | **Parameter-space only** (SelfCalibration gain; per-life topology via neurogenesis/pruning/STDP). Architecture search is future work — and must preserve determinism |
| §3 Autonomous scientific discovery | **Miniature, real**: QuestionDrive (gap identification) + HypothesisLoop (guess → test → structural knowledge update → re-guess). Experiment *design* beyond co-occurrence tests is future work |
| §4 Self-improving cognition | **Parameter-space, closed-loop**: SelfCalibration (detect recurring error → retune gain → clamp-bounded, persisted). Architectural self-modification explicitly out of scope (determinism) |
| §5 Cognitive development | **Present**: the entire developmental program (first word → babble → granularity tiers → attachment → naming) |
| §6 Ecosystem / populations | **Seeded**: twins (one seed, two childhoods), heredity (h≈0.5 temperament + memetic testament graft). Population dynamics future work |
| §7 Research questions | Directly testable here: development vs. training (twins), self-modification vs. static (calibration on/off), curiosity as operational mechanism (question+hypothesis drives) |
| §8 Scientific validation | **Metrics already tracked**: prediction error (free energy), learning progress, prophecy accuracy, hypothesis confirm/refute counts, calibration error counts, determinism (trajectory hash), all reproducible headless |
| §9 Contribution taxonomy | This audit table is the mapping: we evolve *parameters, gains, structure (memory), and development* — not learning algorithms |
| §10 Ultimate objective | The standing scope of this project |

## 5. Scientific principle adopted

No emergent behavior is claimed as consciousness or biological equivalence.
Everything above is measurable, reproducible, and tested (219+ tests, offline,
bit-identical trajectories).
