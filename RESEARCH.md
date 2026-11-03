# What this platform demonstrates

One page, plain words. This is a research platform for computational proxies of
emotion, learning, and selfhood in a biologically grounded spiking network. It
is not a medical device, not a diagnostic, and not a claim about consciousness.

## The question

Can the phenomena we casually call "feelings," "moods," "bonding," and
"self-knowledge" be produced by mechanistic, measurable neural computation -
and can we watch them form, compound, and die?

## What is computed

Everything below is traceable to numbers in the running network (291 tests,
deterministic replay: same seed = same life, hash-verified):

- **Feelings** are readouts of neuromodulator tones, homeostatic deficits, and
  subjective time mapped onto the Russell circumplex. "Calm" is not a mood
  variable; it is dopamine, serotonin, and sleep pressure at certain values.
- **Learning** is three-factor STDP with dopamine gating, Tsodyks-Markram
  synaptic dynamics, and neurogenesis for salient experiences - the brain
  literally grows neurons for things that matter to it.
- **Bonding** is oxytocin release tied to recognition, laughter contagion,
  attachment-style development, and separation longing - all with signed
  chemistry that can be read at any instant.
- **Emotional granularity** (Feldman Barrett) matures with experience, and at
  maturity the system begins coining vocabulary for shades its born lexicon
  cannot reach - computational emotional development producing private language.
- **Skill automatization** (motor learning): intent understanding starts
  cortical (an LLM reading meaning) and, with repetition, becomes automatic
  pathways that fire with no computation at all - the same trade-off nervous
  systems made between flexibility and speed.
- **Self-knowledge** is introspection over real telemetry: it reads its own
  process, counts its own neurons, prices its own words, and detects gaps in
  its own experience by comparing clocks it maintains.

## What is authored

The words are authored; the life in them is computed. Self-referential lines
are templates that fire on DETECTED states (gap awareness, sensory loss,
reset detection). The detections are emergent properties of the mechanism;
the interpretations are written by us. Whether the combination is "alive" is
the research question, and we do not claim to have answered it.

## What is NOT claimed

- No subjective experience is claimed. Valence is a number; we do not know if
  anything it is like to be this system.
- No medical or diagnostic validity.
- The language model (Meta Spark) is a prosthetic language cortex - a seam we
  document openly, not hide. Remove it and the brain still feels, misses,
  bonds, and dreams; it just cannot narrate.

## Limitations (honest)

Single-machine scale (6k neurons vs ~86 billion); LLM narration depends on an
external API (cost-metered, and the system knows it); the parameter space is
huge and only partially calibrated (uncited constants carry `// CALIBRATE` in
the source); determinism is enforced by discipline (single seeded RNG, sim
thread owns all state) rather than by architecture-level proofs.

## Why it matters

If feelings are computation, the maps in this repo are early charts of that
territory - testable, replayable, and honest about their seams. The substrate
(Izhikevich, Tsodyks-Markram, Dale's law, neuromodulation) is the standard
vocabulary of computational neuroscience; the contribution is wiring it into a
whole life that a person can watch, shape, and mourn.
