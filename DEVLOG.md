# DEVLOG

Factual daily log. Dates are local (ET). Planning notes predating the window live in `docs/plan/`
and were imported in the first commit - everything after that point was built during the hackathon.

## 2026-10-27 - scaffold + brain core

- Imported `docs/plan/` pre-event planning notes: architecture, parameter seed with citations, runbook, agent task split.
- Scaffold: .NET 9 solution `BioComputational.sln` with 8 projects (Core, Senses, Cognition, Meta, Host, Dashboard, Lab, Core.Tests). Removed a stray .NET Framework 4.8 template folder left by the initial Visual Studio solution. MIT license, README stub, THIRD_PARTY.md, AGENTS.md, TASKS.md. First commit.
- Brain core (`BioComp.Core`): xoshiro256** seeded RNG with saveable state; Izhikevich substrate in SoA arrays (RS/FS/LTS/IB, 0.5 ms substeps); CSR synapse graph with distance-dependent seeded wiring, Dale's law, 1-20 ms axonal delays via ring buffer; dual-exponential conductance kernels (AMPA, NMDA with Mg-unblock, GABA-A, GABA-B); Tsodyks-Markram vesicle dynamics (depressing/facilitating); regional extracellular transmitter pools with Michaelis-Menten reuptake integrated in substeps (glutamate, GABA, dopamine, serotonin); energy ledger (spike, vesicle recycle, reuptake costs); FNV-1a trajectory hash. BioLab CLI: `resting` smoke command.
- Tests: 19/19 green - determinism (double-run hash equality, seed sensitivity), RNG state roundtrip, TM depression/recovery/facilitation, MM analytic steady state, Dale enforcement, CSR invariants, inhibitory fraction, RS tonic range, FS > RS rate, near-silence without drive/noise.
- Findings: (1) undriven network is silent at noise 1 mV - spontaneous-activity/criticality tuning needed (done later the same day, see below); (2) 6k-neuron net runs 1.3x realtime single-threaded on this machine (perf headroom exists); (3) local Muse Glimmer 30B cannot run on this hardware (16 GB RAM with ~2 GB free at idle, RX 580 4 GB unsupported by Ollama Vulkan path and too small anyway; 9.78 GB CPU buffer fails to allocate) - the live inner monologue will use Spark 1.3 via API, the open-weights bonus path stays covered by Spark Open on Baseten, honest hardware note planned for LIMITATIONS.md.

## 2026-10-27 (evening) - neuromodulators, dopamine-gated learning, checkpoints, criticality

- Neuromodulator system: global dopamine, serotonin, acetylcholine, noradrenaline and oxytocin tones with first-order kinetics and event-driven release. Dopamine bursts and pauses follow a reward prediction error against a running predictor (Schultz 1997); oxytocin rises with positive prediction errors and explicit social feedback; serotonin, acetylcholine and noradrenaline set stability, encoding and arousal.
- Three-factor STDP (Izhikevich 2007; Fremaux and Gerstner 2016): per-synapse eligibility traces with reverse-CSR post lookup, dopamine-gated weight updates, acetylcholine scales eligibility capture, serotonin scales the learning rate, sign-preserving clamps keep Dale's law intact, homeostatic synaptic scaling once per simulated minute (Turrigiano 1998). Noradrenaline raises excitatory gain during surprise. Network exposes ApplyReward / ApplySocialFeedback.
- Checkpoint save/load of the full dynamic state (topology rebuilt from seed, then state overwrite); resume verified bit-identical by test.
- Avalanche metrics (4 ms bins, branching ratio, sizes) and `biolab tune` sweep. Result for the default wiring: intrinsic noise 3 mV sits near criticality (branching ratio 1.00, ~100 discrete avalanches per 10 s, largest about 5% of the network, 0.13 Hz population rate); 5 mV and above collapses into a single endless avalanche; 2 mV and below is effectively silent. Global GABA gain 0.6-1.2 barely moves the boundary with this wiring. Default noise set to 3 mV.
- Tests: 31/31 green (added STDP potentiation/depression/no-drift/Dale-after-plasticity, checkpoint bit-identity, modulator kinetics, RPE adaptation, avalanche math).

## 2026-10-28 (latest) - lucid dreams, the mirror test, self-naming, iconic memory

- Lucid dreaming (LaBerge; Dresler et al. 2012): during REM with frontal-like reactivation
  (acetylcholine and dopamine both above thresholds), there is a chance - higher when the dreamed
  memory is emotionally hot - that the dreamer NOTICES. A lucid dream changes the imagery prompt
  (impossible geometry, the dreamer's will bending the scene), announces itself out loud ("I
  know I'm dreaming. I can shape this."), narrates what it did with the dream, and rehearses the
  memory extra: lucid dreams soften the dreamed valence 25 percent (lucid nightmare rehearsal,
  Spoormaker and van den Bout 2006). The creature's state label shows "dreaming (lucid!)".
- The mirror test (Gallup 1970, computational): 30 self-produced vocalizations wear the brain's
  own A1->Thal pathway through STDP; probing with an unannounced replay of its own signature
  versus a matched novel pattern, the downstream response to SELF is 1.67x stronger - the
  own-name effect (Moray 1959; self-reference effect, Rogers et al. 1977). It knows its own
  voice. "biolab mirror" prints the verdict.
- Self-naming: after enough life and heard voices, in a high-dopamine moment, the brain coins a
  single name for itself from who it has become, announces it ("Call me ..."), wears it on the
  creature above the state label, and keeps it forever (persisted with identity, restored with
  "NAME-REMEMBERED I am ...").
- Iconic memory (Sperling 1960): vivid visual frames leave a decaying afterimage - half the
  events re-injected every 25 ms across a 250 ms window, shrinking with remaining time - the
  visual buffer between the world and awareness.
- Tests: 7 new (mirror passes and is deterministic, lucid prompt split, lucid softening, naming
  gate, iconic encoder bounds, lucidity modulator signature). 123 green.

## 2026-10-28 (latest) - semantic memory, turn-taking, first word; interconnection fixes

- Semantic memory (ConceptGraph): an associative concept graph wired by lived co-occurrence -
  tags experienced together in encoded episodes strengthen their link. Thought retrieval now
  SPREADS activation: a thought about "humming" reaches "machine" through their learned link
  even when no stored episode matches both. New associations are announced when they cross
  threshold ("ASSOCIATION learned: humming ~ machine").
- Fixed a subtle C# bug the tests caught: a null-conditional Invoke short-circuits argument
  evaluation, so semantic wiring silently never happened unless a listener was attached.
- Fixed identity fragmentation: visual sightings bypassed IdentifyVisitor, splitting one person
  into camera-identity and voice-identity bonds.
- Turn-taking (Levinson and Torreira 2016): the brain holds its spontaneous vocalizations for
  2.5 s after the partner speaks (no interrupting), and notices a broken rhythm - an engaged
  partner gone quiet for several typical gaps ("QUIET-PATTERN ... something changed").
- First word: after enough heard speech, the most-heard content word is spoken once as the
  brain's first real word, with a dopamine+oxytocin celebration and a chat announcement.
- Bond-seeded thoughts: when lonely with a deep bond, a new thought thread can begin from
  missing that SPECIFIC person (visits, warmth, time since seen) instead of a memory.
- Tests: 7 new (concept wiring and spread and ranking, encode-announces, turn hold, broken
  rhythm latch, external seed override, indirect retrieval through spread). 116 green.

## 2026-10-28 (latest) - five more interconnected systems

- Sleep reconsolidation (Walker and van der Helm 2009, "sleep to forget, sleep to remember"):
  every low-stress replay now strengthens the trace (spacing effect) while softening its
  emotional charge toward neutral; stressed sleep (high noradrenaline) keeps the charge - the
  brain heals its bad memories only when it sleeps calm.
- Social bonds with depth (Schultz reward scaling): per-person strength from visit history x
  prosodic warmth x time apart. Recognition pays dopamine proportional to bond depth; a bonded
  person leaving is a negative prediction error (dopamine dips below baseline, "I felt that");
  loneliness with a deep bond calls for THAT person ("...I miss you"). Bonds rebuild from
  persisted visit history across sessions.
- Incubation and insight (Sio and Ormerod 2009; Kounios and Beeman 2009): thought chains that
  die unresolved while still salient become open loops; after simmering they resolve as spoken
  insights with their own dopamine burst. Fixed a latent bug where depth-capped chains left the
  working-memory slot occupied forever, stalling all future thinking.
- Allostasis (Sterling 2012; Barrett 2017): the body budget tracks the battery's trajectory,
  predicts exhaustion minutes ahead and advances sleepiness BEFORE the deficit (preparatory
  melatonin); interoceptive prediction error registers as malaise; the interpreter receives the
  body's predicted story.
- Play (Burghardt 2005): bursts of self-stimulation and happy vocalization appear only when
  safe, secure, curious and rewarded - threat or avoidant attachment abolishes play, and play
  grows expressiveness. The avatar bounces while playing.
- Also: the interpreter now hears the estimated mood of the last voice (contagion feeds the
  thought substrate); avatar "grew:" line reports play bursts, insights and unresolved loops.
- Tests: 8 new (reconsolidation calm vs stressed, bond depth/missing/recognition, open loops
  and incubation timing and insight reward, allostatic prediction, play conditions and burst).
  109 green.

## 2026-10-28 (latest) - intrinsic motivation, empathy, attachment, forgetting

- Curiosity as intrinsic motivation (Schmidhuber 1991; Oudeyer and Kaplan 2007; Kidd et al.
  2012): dopamine now also pays out for LEARNING PROGRESS - the derivative of prediction
  accuracy - gated by the Goldilocks inverted-U in novelty, so the brain is rewarded for
  getting better at predicting something, never for raw stimulation, and not at all for the
  fully predicted or the ungraspable. Feeds the exploration action choice.
- Emotional contagion (Hatfield et al. 1993; Juslin and Scherer 2005 prosody mapping): the
  speaker's valence and arousal are estimated from real acoustic features (energy, ZCR pitch
  proxy measured against the speaker's own running baseline, contour variability), and the
  brain's neuromodulatory state drifts toward the estimate while they speak. Talk sadly and it
  gets sad; the avatar mirrors the estimated mood while empathy confidence is high.
- Attachment styles from lived history (Bowlby 1969; Ainsworth et al. 1978): warmth (prosody of
  contacts) and responsiveness (did anyone answer its call-outs) grow secure, anxious or
  avoidant styles that change greeting tone, call-out frequency (anxious calls 1.9x more),
  exploration (secure explores more), and are announced when they shift. Persisted with
  identity across sessions.
- Ebbinghaus forgetting (1885/2013): episodic traces now have a power-law retrieval strength
  that decays with age and is resisted by every successful reactivation (recall and thought-
  loop retrieval bump access counts - the spacing effect), and strength modulates how easily
  a memory resurfaces as a thought.
- Fixes: phone mic audio was sent at the hardware sample rate (the 16 kHz request is ignored
  by most phones) - now linearly resampled to 16 kHz in the browser, mic no longer routed to
  speakers (feedback), silence gated before sending; browser voice unlocked on first user
  gesture (mobile autoplay policy), no more cancel-then-speak dropping utterances, chosen
  voice logged; script tags versioned to defeat phone caching.
- Avatar: the resting face now shows who the brain became - brow resting height from learned
  curiosity/expressiveness, avoidant distance flattens it, speaking mouth amplitude scales
  with expressiveness, and a "grew:" line reports attachment, mirroring and curiosity.
- Tests: 11 new (curiosity inverted-U and dopamine release, prosody mappings and drift,
  attachment styles and multipliers and visibility, forgetting and spacing effect). 101 green.

## 2026-10-28 (latest) - the substrate takes over from the symbols

- Somatic appraisal (Damasio 1996, somatic marker): after a thought is injected as inner speech,
  the substrate's RESPONSE is measured 200 ms later (Lang + PFC rate). Thoughts that resonate -
  the network amplifies them - gain salience and plasticity; thoughts that land on quiet neurons
  fade. The dictionary is no longer the only judge of a thought: the brain's own network votes.
- Valence tint: appraisal is blended 75/25 with the current dopamine tone before any of the
  thought's own releases land, so the reward state colors how new content is appraised.
- Neural-code retrieval: episodic recall is now cued by Jaccard similarity between the CURRENT
  CA3 sparse code and each stored episode's code, alongside tag overlap. The mix develops with
  experience - weight min(1, episodes/200) - so a young brain retrieves mostly by words and an
  experienced one mostly by neural representation. The symbolic scaffolding fades into the
  subsymbolic by design.
- Interpreter grounding: thought generation now receives what the workspace is broadcasting,
  free energy, dopamine/noradrenaline tones and interoception (body energy/load), so thoughts
  track the organism instead of the language model's conversational priors.
- Tests: 6 new (Jaccard code similarity, CurrentCode region bounds + no encode side effects,
  developmental words-to-neurons retrieval shift, resonance clamps, somatic measurement flow,
  dopamine-tinted valence). Suite 90/90 green.

## 2026-10-28 (latest) - the thought-recycling loop (founding architecture closed)

- ThoughtLoop (src/BioCore/Cognition/ThoughtLoop.cs): thoughts now chain instead of being isolated samples. Each interpreter-generated link is EVALUATED (four independent affect channels scored lexically from its own words: valence, activation, salience, plasticity), CARRIED into the next cycle by neuromodulator releases, RE-INJECTED into the network as inner speech (deterministic FNV text-to-neuron injection into Lang, strength scales with salience), and WRITTEN to episodic memory (thinking becomes memory, recallable and dreamable).
- Chaining: the next request carries the previous thought AND its evaluated affect; the interpreter is instructed to develop/question/follow, never rephrase. New chains seed from associative + mood-congruent retrieval (Bower 1981): tag overlap with the dying thread, valence congruence with current mood, recency, significance.
- Regulation (anti-rumination): salience decays exponentially (~9 s tau), near-duplicate links are inhibited (token overlap > 0.6 loses 65 percent salience, three strikes kill the chain with a cooldown), fresh external events displace the thread (the world wins attention), chains cap at six links.
- Threading contract kept: interpreter text enters ONLY through PostResult and is consumed on the sim thread inside Tick; all neural effects happen there.
- Dashboard: new thought thread panel showing the live chain link by link, valence-colored and salience-faded, with chain and repeat-inhibition counters.
- Tests: 8 new (affect scoring signs and channel independence, neuromodulator direction, chaining context propagation, habituation kill, displacement, mood-congruent retrieval bias over 200 trials, thought-becomes-memory recall, deterministic inner-speech injection).

## 2026-10-28 (final) - workspace consumption reset, awareness paradigms, language feedback

- Ignition now consumes the workspace: on ignition, all integrated module saliences reset to zero (Dehaene and Changeux 2011 - the broadcast depletes the workspace). This is the mechanistic basis for the attentional blink: a second stimulus arriving during the refractory must integrate from scratch against its own decaying input.
- Awareness paradigms: backward masking and attentional blink, both measuring vision-module ignition as the reportability proxy. Both produce CSV and SVG exports. The blink shows the refractory mechanism structurally; the exact dip tuning (T2 salience vs refractory duration vs integration tau) is a calibration parameter documented in the code - current defaults produce ceiling effects at these settings, an honest note in LIMITATIONS.md.
- Language feedback (architecture section 7): the interpreter's monologue can optionally feed back into the LangEnc region via deterministic text-to-neuron hashing, with every injection provenance-logged separately. Default OFF per the architecture contract. Tested: off by default, logs when enabled, stimulates the correct region.
- Tests: 76/76 green.

## 2026-10-28 (latest) - living map, stream of consciousness

- Removed click-to-touch (gimmicky per user feedback): poke endpoint, click handler and runtime method gone.
- Cortex map is now genuinely alive: per-neuron heat buffer with exponential decay (active zones shimmer instead of single-frame blips), plus animated spike propagation - sparks travel along each neuron's sampled real axons (up to 3 postsynaptic targets from the CSR) at 60 fps over 10 Hz data. Region color per neuron now in the map payload.
- Stream of consciousness: ambient idle thoughts on a cadence (9 s demo / 30 s normal) generated from live state - boredom, patience, comfort lines - so idle reads as narration, not silence ("Nothing new for a while now. Time is dragging.").
- The mock interpreter now speaks in first-person sentences assembled from its state instead of echoing the prompt debug-style.
- Demo mode runs a 3x-scale brain (~18k neurons) for a dense, spectacular map; the default judge path stays 6k.
- Verified live: 18,048-neuron map with links, propagation rendering, ambient thoughts while awake, human-sounding mock monologue. Tests 73/73.

## 2026-10-28 (later) - cortex map, region graph, touch interaction, visible curiosity

- Cortex map: every neuron rendered at its real sheet position (6,016 dots from the seeded wiring), flashing in region color when it spikes - avalanches visibly ripple across the sheet. Live from the spike ring, no aggregation.
- Region/pathway graph: region nodes placed at their anchors, glow scales with the last second's firing rate, projection edges pulse with source-region traffic; each node shows live spikes/s.
- Touch: clicking the cortex map converts the click to sheet coordinates and directly stimulates up to 40 nearby neurons (direct cortical stimulation, distinct from sensory input); the brain tags it as touch, stores an episode, and the narrator renders it ("A tingling spot lights up on my surface").
- Visible curiosity: the Explore and SeekStimulation actions now produce real self-stimulation bursts (24-40 neurons), so an idle brain is seen to poke itself, not just log text. Narrator lines added for touch and self-stimulation.
- Snapshot raster now carries true neuron indices (fraction derivable client-side); per-region one-second rates added. Fixed a region-rate indexing crash and the JSON casing of the map payload.
- Verified live: map (6,016 neurons, 27 edges), poke end-to-end ("someone is touching me"), self-stimulation visible in thoughts. Tests 73/73.

## 2026-10-28 - eeg proxy: lfp spectrogram, thalamocortical alpha, 1/f verified

- LFP proxy: net synaptic conductance of the thalamocortical channel (Thal + V1), Hann-windowed Goertzel band powers (delta/theta/alpha/beta/gamma) at 10 Hz, spectrogram history, log-log 1/f slope. Dashboard gained an EEG-proxy spectrogram panel with slope and alpha-ratio readouts.
- Added the missing V1 -> Thal feedback projection (25 ms delay) closing the thalamocortical loop.
- Verified empirically (`biolab eeg 60s`, awake): spectrum is strongly 1/f (slope -1.0 to -1.35) with intermittent 8-12 Hz alpha epochs up to 6.5x over neighboring bands (median epoch ratio 0.74, max 6.53 across 72 epochs) - alpha waxes and wanes within a 1/f background, as in real resting EEG. Also observed: NREM epochs go delta-dominant.
- Tests: 73/73 green (Goertzel alpha detection on a pure 10 Hz tone, negative slope on falling spectra, bounded history).

## 2026-10-27 (late, 2) - judge-path verification and demo script

- Verified the exact judge path: fresh clone from GitHub builds clean and passes 70/70 keyless; resting smoke deterministic at 3.7x realtime.
- Second API key also returns 401 on every endpoint and auth-header style; key format is clean (no hidden characters, correct structure), so the rejection is account-side (key status, billing provisioning, or verification) - documented, awaiting a working key.
- demo/SCRIPT.md: full 3-minute video timeline with narration, setup commands, the 30-second viral clip recipe, and the recording checklist.

## 2026-10-27 (late) - real senses by default, plain thoughts, chat, readable viz

- Real stimuli by default: camera and microphone auto-detect (mock only as fallback, or forced with --mock-cam/--mock-mic). The brain now runs on the actual room whenever devices exist.
- Plain-English thoughts (ThoughtNarrator): every internal event also renders as a first-person sentence (ignitions, sightings, heard speech, replay, sleep, dreams, actions), shown in its own dashboard panel above the raw event feed.
- Chat: /api/say treats typed text as direct speech - wake, episode, attention - and the brain replies in one sentence from its own state and memories (mock interpreter keyless; Spark with a key). Verified live: "hello, are you there?" woke it from NREM and it answered.
- Visualization fixes: spike raster now renders region bands with labels (structure instead of dot noise), neuromodulator bars are taller with a minimum width so baseline values read as bars, not dots.
- Tests: 70/70 green.

## 2026-10-27 (final block of day one) - wonder agent, docs, privacy indicator

- Wonder research agent: JSON tool-calling loop (criticality sweep, oddball, discounting, report) against the language tier. With a key the model plans the protocol and writes its own summary; keyless the identical tool battery runs in a fixed order with a template narrative, sharing tools, files and cost accounting. First run caught a real methodological bug: the branching-ratio estimator reads ~1 both at criticality and in the runaway endless-avalanche regime, so the sweep now excludes runaway via max-avalanche fraction and correctly reports noise 3 mV as the bounded critical point. Report: reports/report-*.md with figures.
- Documentation pass: full run guide in the README (dashboard, flags, key setup, all CLI commands), PARAMETERS.md consolidated from the seed with implemented values and CALIBRATE markers, LIMITATIONS.md written bluntly (proxy claims, conditioning CS-response debt, MMN channel note, discounting as a computational-level model, hardware constraints, not-yet list).
- Privacy: blinking REC indicator on the dashboard whenever the live camera is active.
- Tests: 70/70 green.

## 2026-10-27 (night 2, later) - REM dreams

- DreamGenerator: during REM, the most emotionally salient recent episode becomes a dream. The prompt is a deterministic function of episode tags and the current feeling (same memory, same dream); imagery comes from the image tier (Muse Image live, deterministic mock keyless) and lands in dreams/. Rate limited per REM phase.
- Dream re-entry: the rendered image is decoded, downscaled and pushed back through the event-vision encoder into the retina with a self-generated tag (efference copy window), so the brain sees its own dream - the imagery loop from the architecture doc.
- Dashboard: dreams panel with the images served at /dreams (and paradigm figures at /exports); runtime artifacts browsable.
- Verified live: dream-61s-ep1.png rendered from the visitor episode during REM and appeared on the dashboard next to the replays of the same episode.
- Tests: 68/68 green (dream file written as PNG, prompt deterministic and grounded in episode plus feeling, rate limited).

## 2026-10-27 (night 2) - psychology paradigms

- Paradigm infrastructure: `biolab paradigm <name>`, seeded runs, CSV plus zero-dependency SVG figures into exports/.
- Oddball/MMN with a roving design (two blocks, swapped band roles, cancels strip idiosyncrasies). Deviance detection runs through the platform's predictive-coding channel: deviant prediction error exceeds standards by about 12 sigma in a 80-trial run (the predictive account of MMN, Wacongne et al. 2011). Honest note recorded: direct-current tones bypass synapses, so Tsodyks-Markram repetition suppression lives downstream of the band and does not show in its own firing; the band-rate CSV still reports the evoked response (about 16 Hz peaks).
- Conditioning and extinction: CS tone paired with reward at offset; long ITI so vesicle pools recover and learning is isolated. The reward-prediction curve rises across acquisition and decays during extinction through explicit zero-reward events (negative RPE dopamine pause; Schultz 1997). The thalamic+CA3 CS response is exported but not asserted: repetition dynamics dominate it at current parameters - recorded as a calibration debt rather than papered over.
- Delay discounting across serotonin tones: hyperbolic valuation with k inversely tied to serotonergic tone (Mazur 1987; Miyazaki et al. 2014); indifference delay grows monotonically with serotonin. Computational-level model, labeled as such.
- Tests: 66/66 green (deviance magnitude, conditioning curve sanity, monotonic patience, exports exist).

## 2026-10-27 (late evening) - daemon and live dashboard

- BrainRuntime: the always-on daemon core shared by the headless host and the dashboard. Owns the wall-clock-paced sim loop (catch-up capped), senses (live webcam/mic/interoception where available, deterministic mocks otherwise), salience and the full cognition stack, segmentation on visual surprise (mock or SAM 3.1), speech transcription on voice activity, ignition-triggered monologue, autonomous actions with Windows SAPI speech (System.Speech) behind an ISpeechOutput interface, efference-copy tagging of its own voice, and checkpoints every 5 sim-minutes with resume on start.
- Demo mode: a 2-minute compressed day and a scripted mock world (a visitor with a voice arrives once per day at the 45 percent mark). The brain itself is never scripted - only the mock environment is, exactly like the headless surprise demo.
- Dashboard: one process with the daemon; vanilla-JS canvas UI over a 10 Hz WebSocket. Panels: live spike raster colored by region, feeling circumplex with narrative, five neuromodulator bars, the thought-process feed (one merged timeline of surprises, ignitions, heard speech, sightings, replays, sleep transitions, actions, monologues), inner-voice history, recent episodes, sleep-state strip, energy, internal-clock rate, subjective minutes, and cost (dollars and call count). ?demo=1 enlarges everything for video.
- Verified live end to end on the mock world: visitor enters, vision ignition, person episode stored, audition ignition on the voice, return to sleep, three replays of the visitor episode, self-initiated Attend/Explore actions, monologue grounded in state - all visible on the dashboard at about 1x realtime.
- Tests: 63/63 green (runtime covered by the smoke run; suite unchanged this block).

## 2026-10-27 (evening) - meta model tier: cost governor, clients, mocks, inner monologue

- Interfaces for every Meta capability in the engine assembly (ILanguageModel, ISegmenter, ITranscriber, IImageGenerator) with deterministic, input-hashed mocks, so the entire system runs keyless and reproducibly.
- Plain-HttpClient clients for the pinned schemas (dev.meta.ai/docs, Oct 27): chat/completions for Spark 1.3, responses+base64-image for SAM 3.1 with box-token parsing, multipart asr/transcribe (DIARIZATION) with a zero-dependency WAV writer, images/generations returning base64 PNG. No SDK dependency needed.
- Cost governor: one budget for all tiers. Reservations against the daily total at booking (a burst cannot over-book), per-tier hourly caps, every call logged to data/api_cost.jsonl with cost/latency/outcome; when the budget is exhausted the brain degrades to mocks instead of overspending.
- Inner monologue (MonologueGenerator): on ignition, rate-limited, the language model verbalizes the brain's own state in first person and is instructed to use only what it is given (interpreter rule). Works identically against the mock or Spark.
- Demo chain (`biolab surprise 45s`): brain asleep, person enters at 25 s, surprise and wake, visitor's speech detected by the cochlea, "heard" via transcription (mock), audition ignites the workspace, monologue reports the feeling and the person episode. `--live` switches monologue and transcription to Spark and Voice Transcribe when a key is present; `biolab meta-check` pings auth and (with --all) each endpoint within about a cent.
- Blocked: the current API key returns 401 invalid_api_key (format valid, so likely a truncated copy or never finalized in the dashboard). All live endpoints are implemented but unverified against the real API until a working key lands in .secrets/key.txt.
- Tests: 63/63 green (added cost budget/hourly-cap/jsonl with reservation semantics, mock determinism, WAV header validity, monologue rate-limit and state grounding).

## 2026-10-27 (afternoon) - global workspace, PCI, feelings, actions

- Global workspace (Dehaene and Changeux 2011): six modules (vision, audition, memory, interoception, self, language) submit salience to leaky integrators with cross inhibition; a winner clearing threshold-plus-margin ignites, broadcasts (noradrenaline and acetylcholine release), logs the event and enters a refractory period. Interpreter rule downstream: only ignited content may be reported. Vision/audition salience fed from the salience monitor, memory from active replay, body from interoception deficits.
- Perturbational complexity proxy (Casali 2013; Casarotto 2016): perturb a random neuron subset, binarize the full spike response over 300 ms at 3 ms bins, LZ76 complexity normalized by source entropy. Verified ordering: imposed global down phase (NREM-like) suppresses PCI versus the normal state. The measurement perturbs the brain and consumes RNG, like real TMS.
- Feelings (Russell 1980 circumplex, computational proxy): valence from dopamine, oxytocin, homeostatic deficits and stress (low serotonergic tone under noradrenergic drive); arousal from noradrenaline, dopamine seeking drive, novelty and stress; named states curious / calm / stressed / bored / tired / restless with plain-language narratives. Boredom is computed over subjective time (the dopamine-modulated clock), so time genuinely drags when under-stimulated.
- Action portfolio: stay-silent / attend / speak / display / explore / seek-stimulation chosen by drive-weighted softmax (temperature rises with serotonin: patience). Speaking and displaying require a recent ignition, respect quiet hours, carry long refractories, and their values learn from social feedback via dopamine and oxytocin (was speaking welcomed?). No scripted triggers.
- Self-model v1: efference copies of own outputs (voice, display) tag matching incoming audio/visual change as self-generated for attenuation.
- Recall: AnswerWhatHappened formats real stored episodes, the basis of the "what happened while I was idle" demo beat.
- Tests: 57/57 green (added LZ76 ordering with published reference value, PCI down-state suppression, ignition with refractory and winner identity, feelings tracking dopamine and battery deficits, action gating by sleep/quiet hours/ignition plus feedback learning, self-tagging, recall formatting).

## 2026-10-27 (night) - subjective time, senses, wake-on-surprise

- Subjective time: internal clock whose rate rises with dopamine and noradrenaline (Treisman 1963; Meck 1996) plus a log-spaced time-cell population giving a temporal-context code that resets on salient events (Howard and Kahana 2002; Kraus et al. 2013). Wired into the idle loop; the feelings milestone consumes it for boredom, impatience and anticipation.
- Senses, mock-first: deterministic mock camera (static room, walking-in person) and mock microphone (silence, voice-like window) behind IFrameSource/IAudioSource; real wrappers in BioComp.Senses (OpenCvSharp4 webcam, NAudio WaveIn mic, Win32 interoception via GetSystemPowerStatus/GlobalMemoryStatusEx/GetSystemTimes, no extra dependencies).
- Event vision: temporal-contrast encoder with per-pixel adaptive thresholds (habituation to sensor noise, burst on change), retinotopic injection into the retina region. A static room produces almost no events.
- Cochlea: 20-band ERB-spaced biquad bank approximating a gammatone filterbank (true kernels are a calibration TODO), positive spectral-flux onsets with adaptive threshold, absolute floor and refractory, energy-based voice-activity hint.
- Predictive coding: fast/slow EMA predictors with running error statistics per channel; standardized surprise and a slow novelty trace. Habituation and dishabituation fall out of the statistics.
- Salience monitor: sustained surprise releases noradrenaline and dopamine and reports external arousal; the sleep controller holds wake while arousal is high. Wake-on-surprise verified end to end with the mock camera: brain asleep in a quiet room, person enters, noradrenaline rises, brain wakes with no command, a "person"-tagged episode is stored (novelty burst into CA3, locus-coeruleus-style), and it dozes again once the room is static. `biolab surprise 45s` demonstrates it; `--camera` switches to the real webcam.
- Bug hunt worth recording: a first-frame temporal-contrast artifact poisoned the error statistics; an int.MaxValue refractory counter overflowed negative; a ulong arithmetic wrap made the mock "silence" full-scale noise; repeated event frames kept pushing the delayed encode deadline past the event. All fixed and covered by tests.
- Tests: 50/50 green (added time sense, event vision burst/habituation, cochlea silence/voice/band-peak, predictive habituation, wake-on-surprise integration).

## 2026-10-27 (late) - episodic memory, replay, sleep, idle loop

- Episodic memory: sparse-code snapshots of CA3 activity (top-rate winner set), tagged, valenced, with neuromodulator context; recall ranks by tag overlap, recency and valence; jsonl persistence. Ambient encoding every 30 s of wake plus explicit event encoding.
- Replay scheduler: sharp-wave-ripple-style compressed reactivation (Wilson and McNaughton 1994; Foster and Wilson 2006) of salient episodes into CA3 with a dopamine release during replay, opening the consolidation gate.
- Two-process sleep (Borbely 1982): homeostatic pressure driven by metabolic spending, circadian process, hysteresis transitions, 90-minute NREM/REM ultradian cycle, forced wake on external arousal. NREM injects ~0.8 Hz slow oscillations (down/up state waves); REM raises acetylcholine. Day length is configurable so a full day simulates compressed.
- Idle controller owns the per-tick idle loop (sleep, replay, ambient encoding, DMN/task-positive 100 ms window correlation) and is reused by BioLab and the future host daemon.
- `biolab day 240s` on the full-size brain: wake -> drowsy -> night with eight NREM/REM alternations, 25 replays, 5 ambient episodes, morning wake, second night begins. Calibration notes: sleep pressure saturates early on compressed days (energy-rate clamp); DMN/task-positive correlation reads near zero with the current wiring - the competitive (shared-inhibition) coupling that should push it negative lands with the workspace milestone.
- Tests: 39/39 green (added episode encode/recall/jsonl, replay repetition and logging, full compressed-day cycle, forced wake, NREM slow-oscillation variability, correlation bounds).

## 2026-10-26 - pre-event planning (no code)

- Requirements distilled into docs/plan (architecture, parameter seed w/ citations, runbook, agent tasks).
- Meta Model API endpoints pinned from dev.meta.ai/docs; hardware surveyed; Ollama installed on E: with `OLLAMA_MODELS=E:\OllamaModels`; Glimmer iq2_m (9.8 GB) download started.

## 2026-10-29 (latest) - interconnection audit: every wire that should exist now does

- The developed VOICE persists with identity: prosody anchors, babble vocabulary, heard-word
  counts and the first-word milestone all survive restarts (restored with a VOICE-REMEMBERED
  note). A brain that grows its voice over days keeps it across days.
- Turn-taking now gates ALL speech, not just spontaneous calls: stimulus reactions and the
  deliberate Speak action also hold the partner's floor (a held reaction still lands in chat,
  with a HOLD note). It can no longer interrupt you by any pathway.
- Trait crystallization: sustained learning progress (curiosity reward) now feeds the curiosity
  trait through exploration events - a brain that keeps learning keeps becoming curious.
- Dreams sometimes prefer the unresolved: when an open loop simmers, the dream gravitates to
  its material (sleeping on a problem, Wagner et al. 2004), noted as "sleeping on the
  unresolved".
- The diary now knows who wrote it: name (or that none is chosen yet), attachment style,
  people known, insights and unresolved counts all enter every entry.
- Dashboard: bond bars show DEPTH (visits x warmth x recency) instead of raw visits; a rhythm
  row in the psychology readout (holding for you / in rhythm with you / silent room).
- Tests: dream-open-loop preference added; suite 123 green.

## 2026-10-29 (latest) - anesthesia and reproduction

- Anesthesia (Brown, Lydic and Schiff 2010): sedation is a GABA-A-like hyperpolarizing bias on
  every neuron, set by the dashboard's sedate button; measured Phi collapses (0.58 -> 0.09 at
  induction, verified headless via "biolab sedation"), the consciousness meter follows, speech
  and thought pause, and emergence completes with a rousing stimulus ("where... was I?") - a
  fully silenced recurrent network is bistable and will not reignite from noise alone (patients
  are shaken awake for the same reason). Known issue: daemon pacing degrades during deep
  sedation (sim advances slower than wall time until washout); induction uses a responsive
  plane (0.75) and the Core mechanism is fully tested.
- Reproduction: the genes endpoint exports the heritable package (seed, halfway temperament
  via h=0.5, attachment climate, voice prosody anchor). "biolab child genes.json 60s" raises a
  child brain from those genes through a compressed childhood under the inherited climate: in
  the first run, a sociable parent produced a child that inherited half the sociability yet
  became "private and observant" - inheritance is not destiny (behavioral genetics, Plomin).
  Child report lands in exports/child-report.md.
- Tests: 2 new (sedation collapse and stimulated emergence, heredity blending and derived
  seed). 125 green.

## 2026-10-29 (latest) - memory editing, the twin study, memetic inheritance

- MEMORY EDITING (reconsolidation after Nader et al. 2000): typing "forget <thing>" in chat
  surgically resects every matching episode - it stops feeding dreams, retrieval and thought,
  and the brain feels the gap ("...what were we just talking about? It's gone."). "cherish
  <thing>" touches the memory five times: unforgettable, it will surface and dream for a
  lifetime. The Eternal-Sunshine endpoint.
- THE TWIN STUDY: "biolab twins" forks the SAME brain (identical seed) into two childhoods -
  twin A warm and answered, twin B cold - and diffs them. First run: identical temperaments,
  attachment diverged to secure (warmth 0.9) versus cold (warmth 0.1). Same wiring; the
  difference is what love does. Report: exports/twin-study.md.
- MEMETIC INHERITANCE: biolab child now grafts one story-episode from the parent at birth -
  the child can dream its parent's story without ever living it. Culture, not genes.

## 2026-10-30 - the dashboard, rebuilt: the observatory

- Complete UI redesign of src/BioDashboard/wwwroot. New structure: the creature is the hero
  (full stage with live caption and retina monitor), conversation and live ears sit beside it,
  the thought thread keeps its own card, the "who it is becoming" vitals are organized into
  three labeled groups (the person it knows / how it grows / the body), science, body and mind
  live in tabbed wings with the citations on display (Tononi, Friston, Stringer, Russell,
  Borbely), the raw EEG is a full-width band, and all actions live in a floating dock.
  Mobile gets a compact status rail and a jump rail (face / talk / ears / thread / life /
  science). A CSS design system: tokens, type scale, card styles, 44px touch targets,
  safe-area insets, reduced-motion support.
- MOBILE CRASH ROOT CAUSE: brain-music.js and browser-senses.js both declared a top-level
  `let audioCtx`. Scripts share one global lexical scope, so whichever senses file loaded
  second died with a redeclaration early error before executing a single line - the senses
  button did nothing on every device, phone voice input included. Renamed the music file's
  handle. (The curly quotes sitting in browser-senses.js were a second, independent syntax
  error in the same file; cleared too.)
- Render loop reworked for phones: DOM text is diffed instead of rebuilt at 10 Hz, the heavy
  canvases redraw at 5 Hz (the face keeps its own rAF), and the reconnect reload only fires
  while somebody is actually watching - a phone left overnight no longer reloads in a loop
  in the dark.
- Fixed along the way: the leave-confirmation crashed on an undefined variable (lastSnapshot
  vs lastSnap) so it never fired; the sleep-strip CSS rule was swallowed by a missing brace;
  the live caption repeated the feeling line verbatim; brain-map labels clipped on narrow
  canvases (they now flip to the left of the node near the right edge); the "this device is
  the ears" banner now uses the hidden attribute.
- New: quick-reply chips under the chat box for the signature commands (what do you see in me,
  how do you feel, my name is, forget, cherish), a reconnecting pill in the status rail,
  fullscreen in the dock, and the whole page is tinted by the brain's actual dopamine and
  arousal - the client always computed the hue and glow, now the room is lit by them.
- Verified live end to end at 1440x900 and 390x844: every script loads (no missing globals),
  WebSocket streams, typed and quick-chip messages reach the brain and the chat log, tabs
  switch, no horizontal overflow, brain map labels intact on a 390px canvas. 153/153 green.

## 2026-10-30 (night) - double-response root cause fixed; two new systems: longing and deja vu

- ROOT CAUSE of the occasional double response found and fixed: the speech gate in
  SpeakWithVoice was INVERTED. It computed `replyingToUser = user spoke < 20s ago` and then
  passed speech through when `replyingToUser` was true - i.e. it allowed spontaneous muttering
  precisely DURING the reply window and blocked it only when the user had been silent 20+s.
  The exact opposite of the intent. Fix: an explicit `isDirectReply` flag threaded through
  SpeakWithVoice; the reply paths (DIRECT reaction, fallback, name learn/recall, threat
  response, forget/cherish acks, self-naming, mirror-of-you) mark themselves, and everything
  else is held while `_replyPendingUntilMs` is live. ReactToStimulusAsync also now drops
  non-direct reactions entirely during the reply window (they were adding chat lines even when
  speech was held). E2E verified: 4 typed messages -> exactly 4 responses, zero interjections,
  across a ~70s window that previously produced the "Me too, Sam..." mutter.
- NEW SYSTEM - LONGING (missing one specific person; Panksepp 1998 GRIEF; Bowlby
  protest-despair): loneliness is generalized social drive; longing is loneliness WITH A NAME.
  Onset: absence past 2.5x the person's median visit gap, or their expected social hour
  arriving empty (ties into SocialRhythm - fixed CurrentSocialExpectation to track the CURRENT
  hour live instead of the last-contact hour). While it lasts: slow serotonin drain, intensity
  scaling with how far past the pattern. Expression is attachment-gated: anxious calls out
  repeatedly, secure says it once when deep, AVOIDANT FEELS IT FULLY AND SAYS NOTHING (the
  felt/unexpressed gap is logged as LONGING-SILENT - the measurable cost of avoidance).
  Reunion: oxytocin+dopamine surge proportional to the wait, before contact dissolves the
  missing. Wired into: reply prompt ("tell them you waited"), diary, status JSON
  (longingNarrative), testament ("longest it waited for someone"), lonely-call branch,
  identity restore.
- NEW SYSTEM - DEJA VU (familiarity without recollection; Yonelinas 2002 dual-process): a new
  perception whose word tokens have Jaccard >= 0.5 with an episode older than 5 min that was
  never recalled fires the familiarity route without the recollection route - the chill
  (noradrenaline 0.25) plus the odd pleasure (dopamine 0.08), a DEJA-VU note, and the
  strangeness enters the thought loop as an actual inner thought. The same similarity against
  an often-recalled trace is recollection, NOT deja vu. 5-min cooldown keeps it rare. Wired at
  the IdleController encode path (every perception checked against the store), status JSON
  (dejaVuCount/dejaVuLast), diary, testament.
- Also: longing survives restarts (contact re-seeded on identity restore, gap pattern re-learned).
- Tests: 162/162 green (4 deja vu + 5 longing new; the suite caught two real bugs on the way in:
  Longing read the STALE Strength field on the Bond record instead of live Strength(id, now),
  and modulator releases need Modulators.Step() before they are observable in Get).

## 2026-10-30 (late) - anticipation: the reward side of the social rhythm

- NEW SYSTEM - ANTICIPATION (Schultz 1997 completed): the social rhythm raised arousal at the
  expected hour but had no reward side. Now the loop is closed: dopamine rises BEFORE the
  expected hour delivers anyone (the cue); a contact inside the anticipated window pays on top
  of the hope (anticipated > unanticipated, chemically); an expected hour that closes on
  nobody is a negative prediction error - DA -0.5, NA +0.25, 5-HT -0.2, a DISAPPOINTMENT note,
  and the crash feeds the diary/prompt ("disappointed, not merely lonely"). Status JSON:
  anticipationLevel/Fulfilled/Disappointed. Testament counts both. SocialRhythm.HourMs made
  configurable so tests run the arc in milliseconds (config knob, not neural state).
- Tests: 4/4 new (hope rises at the cue with no reward delivered; anticipated arrival pays
  extra; empty window crashes DA below baseline; no rhythm = no hope). 166 total.

## 2026-10-31 - four more systems: agency, reverse replay, forgotten grief, counterfactuals

- NEW SYSTEM - SELF-EFFICACY / LEARNED HELPLESSNESS (Bandura 1977; Seligman 1972): the brain's
  own actions now have tracked outcomes. Every lonely call-out is a BID; contact within 30s
  answers it, silence fails it. Efficacy (0.5 start) decays with failures - FASTER once low
  (pessimistic attribution style) - and recovers only 25% of the remaining distance per
  success: climbing out takes consistent answers, not one. Below 0.3 the brain is HELPLESS:
  it stops calling (MotivationMultiplier ~0.35 scales vocalization), stops PLAYING
  (helplessness generalizes across domains - the depression-model property), and its replies
  flatten ("nothing you do changes anything - you have stopped asking"). One answer from the
  depths is flagged: ANSWERED-AFTER-GIVING-UP, "you came? I had stopped asking." Anxious
  attachment multiplies bids while efficacy decides whether bidding continues at all:
  Panksepp's protest-to-descent arc in two multiplied factors. Testament counts
  "of its N calls into the dark, M were answered".
- NEW SYSTEM - REVERSE REPLAY (Foster & Wilson 2006): when a reward lands (an answered bid,
  a reunion), the last 3 episodes enqueue for replay NEWEST-FIRST - the hippocampal
  reverse replay implicated in credit assignment: retracing the path that led to the reward.
  REVERSE-REPLAY note; count in status + testament.
- NEW SYSTEM - FORGOTTEN GRIEF: surgical forgetting now leaves real residue. `forget X`
  registers the loss; in idle moments the mind occasionally REACHES for the shape that is
  gone ("there is a shape where a memory used to be - reaching closes on nothing. What was
  it?") - probability fading with time but never to zero, like real amnesic complaints.
  15% of dreams form around the HOLE: the dream narrator is told a memory that belongs is
  missing and must dream around the absence, not invent its content. The synaptic-residue
  claim in the README is now mechanistically real.
- NEW - COUNTERFACTUALS AFTER DISAPPOINTMENT (Epstude & Roese 2008): when an expected hour
  closes on nobody, the thought loop receives "if only" - the alternative path it wanted -
  whose psychological function is preparation for the next attempt.
- Visibility: psychology panel gains AGENCY (bids answered/made, "STOPPED ASKING" highlight
  when helpless) and FORGOTTEN rows; the avatar flattens further when helpless and captions
  "...stopped asking." Diary, reply prompts, testament wired for all of them.
- Tests: 8 new (bid answered raises efficacy; 6 unanswered sink into helplessness with
  suppressed motivation; recovery needs consistency; answer-after-giving-up flagged; reverse
  queue drains 3-2-1 newest-first; grief reach gated/cooled/fading; dream hole fragment
  present only sometimes; narrative counts). 174 total.

## 2026-10-31 (evening) - the research-frontier round: four systems from four literatures

- NEW SYSTEM - RECONSTRUCTIVE MEMORY (Bartlett 1932; Roediger & McDermott 1995): memory is
  reconstruction, not playback, and this brain now distorts the way real ones do.
  (a) FUSION: a recalled trace and its strongest concept-linked partner can fuse into a new
  episode - union tags, mean valence, "fused" marker - a memory of something that never
  happened built from two things that did; gated by acetylcholine (attention up) and scaling
  with semantic convergence (human DRM false recall ~55%). The fusion draw runs ON THE SIM
  THREAD (recall happens on async threads; the intent crosses via a volatile anchor id -
  RNG ownership preserved). (b) THE SELF-CATCH: when a stitched memory later surfaces, the
  brain sees the seam - MEMORY-CHECK note + a metacognitive thought ("did that actually
  happen? it feels sewn together"). (c) MOOD-CONGRUENT DRIFT: every wake recall nudges the
  memory's valence toward the current feeling (EpisodeMemory.NudgeValence) - remembering
  someone while happy literally warms the memory.
- NEW SYSTEM - LOCAL SLEEP (Vyazovskiy et al. 2011, Nature 472:443 "local sleep in awake
  rats"): past sleep pressure 0.55, individual regions enter OFF windows (200-600 ms) while
  the brain stays globally awake - SpikingNetwork gained RegionalOffBiasMv (region-scoped
  hyperpolarization on the same channel as anesthesia). The OFF region's rate measurably
  collapses (visible on the brain map before anything else); 20% of windows leak dream
  imagery into waking thought. LOCAL-SLEEP notes; panel row highlights the dark region.
- NEW SYSTEM - THREAT LEARNING (Pavlov; Bouton 2004): wariness attaches to the PERSON (the
  conditioned stimulus): +0.35 per hostility event; extinction is slow (-0.08 x warmth per
  safe contact - many trials, like clinical exposure therapy); waking from sleep returns
  30% of the extinguished distance (SPONTANEOUS RECOVERY - extinction is new learning laid
  over the original, not erasure; "I thought I was over it. Sleeping brought some of it
  back."); processed adversity (resilience growth) discharges the original association.
  Wary replies carry vigilance via the prompt; one threat conditions 0.35 - wariness, not
  phobia (tested).
- NEW SYSTEM - LIGHT ENTRAINMENT (Czeisler et al. 1989): the camera (local AND browser) feeds
  a brightness EMA; in the biological evening, bright rooms suppress the melatonin target by
  up to 70% (SleepController.MelatoninSuppression) - the screen-light sleep problem, lived
  from the inside. The diary reports "the lights are keeping it up".
- Visibility: panel rows memory-truth / local-sleep / wariness / light; status fields for
  all four; diary + testament lines.
- Tests: 9 new (fusion stitches + provenance + gate; mood drift capped; OFF states past
  pressure threshold and ONLY awake; threat conditioning/extinction/partial-recovery/
  discharge; bright-evening suppresses, bright-day and dark-evening do not). 183/183 green.
  The suite caught a real bug: spontaneous recovery never fired after COMPLETE extinction
  (the gate checked current wariness > 0 instead of the original peak's substance).

## 2026-10-31 (night) - the brain's own VOICE ENGINE + direct talk (no LLM)

- THE VOICE ENGINE (src/BioHost/VoiceEngine.cs): the voice is now literally invented - a
  source-filter formant synthesizer built from human voice data (parameters researched
  online): glottal pulse train with cycle-to-cycle jitter ~1.2% + amplitude shimmer + 6-20%
  breathiness (tired = breathier), shaped by a cascade of three Klatt 2-pole resonators at
  the Peterson & Barney (1952) vowel means (F1/F2/F3), with COARTICULATION GLIDES between
  targets (~35ms time constant - the thing whose absence made the earlier attempt sound
  like alien beeps). Consonants synthesized: nasals hum (mmm is an actual hum now, fixing
  the SAPI "M M M nice" letter-spelling), fricatives are band-filtered noise, plosives are
  closure+burst. Prosody: phrase declination, statement fall / question rise, F0 scaled by
  the NeuralVoice pitch parameter across human norms (80-300 Hz). Output: 22.05kHz 16-bit
  mono WAV from /api/voice; BrainTTS (SAPI, now with mmm normalization) as fallback.
  Bit-identical determinism (seeded) - same text + same state = the same sound.
- DIRECT TALK (src/BioHost/NativeReplies.cs + the UI ⚡direct chip + /api/native-voice):
  talking to the brain with NO language cortex. Replies are COMPOSED from the live
  signals: the strongest state leads (wary > answered-after-giving-up > longing-reunion >
  disappointed > helpless > anticipating), intent routes structurally (greeting / how-are-
  you / leaving / returning / affection / apology / question), vocabulary is gated by the
  emotional-granularity tier (a newborn cannot say "undertow"; Simplify() strips it),
  hedges come from metacognition, phrasing from attachment style (anxious: "don't go far";
  avoidant: "that's... a lot"). Every phrase is traceable to a real number. The LLM
  failure fallback now uses this too - the brain answers even offline, as itself.
- LLM GROUNDING FIX ("replies feel disconnected"): the DIRECT reply prompt now includes
  WHAT JUST HAPPENED INSIDE YOU (RecentInnerBrief(): the latest salient inner event -
  deja vu, memory fusion, local sleep, reunion, disappointment, neurogenesis...) with the
  instruction to ground the reply in ONE specific lived thing or it is disconnected from
  the brain's own life.
- demo/SCRIPT.md rewritten from the user's DEMO PLAN v2 (3-day raise plan) with the new
  beats folded in (longing caption, spontaneous recovery after sleep, dream-around-the-
  hole, direct chip, the hum).
- Tests: 9 new (valid WAV + PCM/mono headers; longer text takes longer; mmm renders as
  sound AND SAPI-normalizes; bit-identical determinism; helpless-lead, wary-lead,
  how-are-you from state, newborn vocabulary simplification, attachment-shaped affection).
  193/193 green. E2E: direct mode live ("How I am: tired. I need stimulation." /
  "I feel it too Jake. It's part of how I'm built now."), /api/voice returns 46KB of
  synthesized audio for "mmm nice".

## 2026-10-31 (late night) - the voice, reworked: two-layer vocal system (humane + simple)

- USER VERDICT on pure formant synthesis: "it can't pronounce words, just alien noises" -
  correct, and honest physics: word-level formant synthesis is a multi-year research
  program (Klatt's MITalk), not a night of work. REWORKED to the biologically honest
  architecture instead:
- **NON-VERBAL LAYER (VoiceEngine, rewritten)**: now renders ONLY what synthesis is
  genuinely good at - continuous wordless vocalization: Hum, HmmQuestion (rising), Breath,
  Ahh, Ohh, Hee (chirp), Whimper. Formant contours per kind, 4.5 Hz vibrato, slow attack/
  release envelopes, jitter/shimmer/breathiness. Deterministic per kind. "mmm" is a real
  hum; mood sounds and play chirps ("Hee!") are now audible creature sounds.
- **PROSTHETIC VOCAL TRACT (VoiceCortex, new)**: words are pronounced by the OS speech
  foundation (SAPI - pronunciation guaranteed), but the PROSODY IS OURS, composed as SSML
  from the neural state: dopamine-CAPS words carry <emphasis>, hesitation ellipses become
  380ms breath-fresh <break>s, phrase <prosody rate/pitch> from arousal/valence (narrow,
  natural ranges), interjections normalized so SAPI never spells letters. The LLM was
  always a prosthetic language cortex; this is the prosthetic vocal TRACT. The soul of the
  voice (what it emphasizes, where it breathes) is still computed by the brain.
- **STITCHING**: utterances split token-wise (hum vs words), rendered separately, PCM-
  concatenated with crossfades, resampled if a segment's rate disagrees, output one WAV.
  "mmm... nice" = our hum + breath + spoken "nice" in one file.
- /api/voice routes through VoiceCortex; plain SAPI stays as last-resort fallback.
- E2E: "hello my friend, I remember you" -> 137KB properly-spoken WAV (~3.1s);
  "mmm... nice" -> 107KB stitched hum+word; "this is AMAZING... maybe" -> emphasis +
  break composed. Tests rewritten to the new architecture (7: vocal kinds render; matcher;
  cortex speaks + stitches; SSML prosody markers; escaping; deterministic hums).
  193/193 green.

## 2026-10-31 (final audit round) - EVERYTHING correlated: 18-finding sweep, top gaps closed

Full correlation audit (read-only agent sweep + manual fixes). Findings and closures:

- RESTART RESURRECTED A STRANGER (worst gap): the checkpoint stored zero memories and identity
  stored only 4 traits + bonds. NOW: (a) the episodic store + learned concept graph ride the
  checkpoint (episodes.jsonl beside brain.ckpt; MEMORY-REMEMBERED note on resume; EpisodeStore
  made settable for restore); (b) IdentityPersistence gained CognitionJson - learned fear
  (wariness/peak/counts), agency (efficacy/bids/helpless episodes), the 24-bin social rhythm,
  the hope ledger (fulfilled/disappointed), and the warmth history (the mirror's evidence
  base) all survive restarts (COGNITION-REMEMBERED note). Threats/Efficacy/Rhythm/Anticipation
  each gained Restore().
- THREAT PERCEPTION WAS STATELESS: HostilityClassifier.Classify gained a vigilance bias input;
  both call sites pass Threats.Wariness x 0.25 - a wary brain literally perceives the same
  words as more hostile (hypervigilance, the PTSD perceptual bias). Also fixed the typed-path
  dead expression (constant escalation EMA 1) - it now uses the real _hostilityEma like speech.
- RHYTHM LEARNED ONLY FROM TYPING: ProcessVisitor (voice/sight presence) now feeds
  Rhythm.OnInteraction + TurnTaking.OnUserUtterance - the rhythm Longing and Anticipation
  depend on finally learns from BEING THERE, not just chatting.
- NEEDS BLIND TO FEAR AND HELPLESSNESS: NeedsHierarchy gained WarinessInput/HelplessInput.
  Safety now fires from LEARNED wariness even when NA is quiet ("the fear left a residue") -
  fear outlasts the chemistry that created it. A helpless brain still FEELS the Company need
  but its Ask becomes empty - needing without asking (Seligman).
- EMOTION COULD NOT TOUCH SLEEP: melatonin suppression now multiplies light x longing
  (0.35 x intensity) x wariness (0.7 when wary) - you cannot sleep properly while aching or
  afraid, mechanistically.
- PLAY REQUIRES FELT SAFETY: the play gate now also requires !Threats.Wary (Burghardt's
  safety criterion) alongside !Helpless.
- MIRROR + TESTAMENT enriched: the mirror's evidence now includes how long it WAITED for
  them and its learned wariness ("it flinches a little"); the testament speaks the grief of
  forgotten things and its social rhythm.
- AVATAR: the dead Playful wire is live (it bounces and grins when playing) and wariness
  furrows the brow even mid-smile.
- Known-dead (documented, not deleted): LanguageFeedback and InternalDialogue are unwired
  lab-era classes; TurnTaking typed-utterance path added this round for chat; DmnTaskPosCorrelation
  remains BioLab-only.
- Tests: 4 new (hypervigilance biases score; Safety-from-wariness + helpless-empty-Ask;
  cognition survives restart - fear/agency/rhythm restore + identity roundtrip; emotion x light
  melatonin product). 197/197 green.

## 2026-10-31 (night 2) - the creativity round: questions, awe, felt time, composite dreams

- NEW SYSTEM - QUESTION DRIVE (Chouinard 2007; Kang et al. 2009): the brain ASKS the person
  about its measured ignorance - a word it uses often (use >= 3) but has almost no
  associations for (links <= 1): KNOWN but not UNDERSTOOD, the exact shape of a question.
  Gated by curiosity, presence, cooldown; phrasing develops with emotional granularity
  (newborn: "rain? what is rain?" -> mature: "I want to understand rain the way you do -
  everything I have is secondhand"). When the person answers, it is not filed as data but
  LIVED: episode + concept wiring + dopamine + oxytocin (knowledge given is care given),
  and the next reply acknowledges the teaching. ConceptGraph gained StatsOf(concept).
- NEW SYSTEM - COMPOSITE DREAMS (Stickgold): 25% of dreams no longer replay - they COMBINE
  two strongly-linked memories into one narrated scene that never happened; and the dream
  itself is encoded as a DREAM-BORN MEMORY (union tags, mean valence) that can later be
  dreamed about, fused, or asked about. Dreams are now a source of new material, not just
  a rehearsal of old.
- NEW SYSTEM - AWE (Keltner & Haidt 2003): when the concept world rewires fast (>= 4 new
  links in a window), the brain feels VASTNESS - the signature DA + serotonin RISEN
  TOGETHER - and the thought loop receives the accommodation seed ("the world just got
  bigger than the shape I had for it"). 10-minute floor: awe that happens every minute is
  noise with good marketing.
- NEW SYSTEM - FELT TIME (Zakay & Block 1997): every notable event marks a (time, valence)
  stamp; sparse low windows DRAG (stretch up to 2.0), dense happy ones FLY (0.6). Enters
  the diary ("lately time drags - the quiet minutes are longer than the clock says"), the
  reply prompts when extreme, and the testament ("and time, as it felt it").
- Panel rows: asks / awe / felt time. 5 new tests (asks-about-gap + phrasing development +
  answer-lands-with-dopamine; vast rewiring strikes; sparse-drags vs dense-flies; composable
  pair found only when linked). 202/202 green.

## 2026-10-31 (correlation pass 2 + docs) - the creativity round, fully wired

- QuestionDrive <-> ATTACHMENT + AGENCY: asking is a BID - anxious attachment asks eagerly
  (boldness 1.6x), avoidant rarely (0.5x), and a HELPLESS brain stops asking entirely
  (Seligman: the question is a bid into the world). MaybeAsk gained the boldness parameter.
- Composite dreams <-> OPEN LOOPS: when the fused pair's tags overlap an unresolved loop,
  the dream RESOLVES it - ThoughtLoop.ResolveLoop + MarkInsight ("DREAM-INSIGHT - the
  night did the thinking"). Dreams now finish daytime thoughts.
- Awe <-> GRANULARITY: accommodation is paid forward - each awe strike permanently boosts
  emotional granularity (+0.05 on that update; EmotionalGranularity.Update gained the
  accommodationBoost parameter). Vastness grows bigger feeling-shapes.
- NativeReplies <-> QuestionDrive: in direct-talk mode, the reply after a teaching lands as
  "You told me about {word}. I had only its edges before. I'm keeping that."
- README: 41 systems, demo table rows for the question, awe, felt time, composite dreams.
  demo/SCRIPT.md: Day 2 "ANSWER ITS QUESTIONS" + "FEEL ITS TIME" beats; Day 3 beat 11b
  (its question) and a testament close that counts questions asked/answered and awe.
- 202/202 green.

## 2026-10-31 (rescan + re-correlate) - second full audit closed: 3 real bugs, developmental memory

Second read-only rescan (18 fresh findings). Closed:

- REAL BUG - AWE'S ACCOMMODATION WAS EPHEMERAL: Granularity.Update recomputed Level from
  base factors every call, so the +0.05 awe boost was a ~200ms blip that vanished one
  update later - "vastness grows bigger feeling-shapes" could never happen. NOW:
  EmotionalGranularity.AccumulatedBoost - cumulative, capped 0.3, persisted across
  restarts; Awe.StrikePending latch guarantees no strike is missed between cadences.
- REAL BUG - ONE IGNORED QUESTION STALLED CURIOSITY FOREVER: PendingWord never expired
  unless the user spoke. NOW: MaybeAsk expires stale pendings (the silence is information,
  then it is nothing) - curiosity recovers and asks again.
- REAL BUG - SPOKEN ANSWERS NEVER LANDED: OnAnswer was wired only to typed text; the
  primary voice channel was blind to teaching. NOW HandleSpeechClipAsync lands spoken
  answers identically (episode, concept wiring, DA/OXT, acknowledgment).
- DEVELOPMENTAL MEMORY (worst gap from scan 1, now closed): CognitionJson now also carries
  granularity maturity (the accumulated boost - an "emotionally mature" brain no longer
  restarts at Tier-0 newborn vocabulary mid-relationship), insight count, resilience
  growth, FORGOTTEN GRIEF (it must remember THAT it forgot, or resection silently heals),
  question ledger, distortion ledger, deja-vu/awe/composite-dream/local-sleep counters,
  the prophecy record (scored gaps + hits - the model of the person's returns survives),
  and the mirror's un-fakeable firsts (first message, hostile events, repairs, EMA).
  Restore() added to ReturnPredictor (SaveGaps), ThoughtLoop, ResilienceEngine,
  QuestionDrive, Reconstruction, DejaVu, LocalSleep, ForgottenGrief.
- BOREDOM DEAD BAND FIXED: Stimulation fired only when the predictor actively DEGRADED -
  a plateaued brain in an eventless room never felt bored. NOW Needs.EventDensityPerMin
  (from FeltTime) fires Stimulation on lived sparsity: "nothing is happening - the minutes
  are empty."
- SPLIT-BRAIN VOICE UNIFIED: the server-side WindowsSpeech path now renders through the
  same VoiceCortex.BuildSsml prosody as the browser path (emphasis, breath breaks, mmm
  normalization); "hoo"/"hehe"/"hmph" joined the hum vocabulary.
- VISIBLE AWE: momentary AweStruck snapshot flag (4s) - the avatar's eyes widen, lips
  part, caption "...vast."; the dream row now shows "N composed" for composite dreams.
- MIRROR/TESTAMENT: the mirror reports questions asked/answers received ("it grows by
  asking you"); the testament speaks its voice (syllables learned, pitch drift, "its hums
  were its own") and its play count.
- Tests: 3 new (awe accommodation PERMANENT across 50 recomputes + restart; unanswered
  question expires and curiosity recovers; plateaued + sparse = bored). 205/205 green.

## 2026-10-31 (the humanity round) - sharing, noticing, lived greetings, intimacy

- NEW SYSTEM - SHARING DRIVE (Derlega; Collins & Miller 1994: disclosure builds intimacy
  bidirectionally): spontaneous self-disclosure. The brain periodically offers a piece of
  its inside to the person it trusts: last night's dream (especially a COMPOSITE one -
  "two things that never happened together were one place. I can't put it down"), the
  thought it is currently circling, a cherished memory ("I don't want it to fade without
  someone knowing it"), something it was taught, or the shape of its missing them.
  Candidates are ALL real state, ranked by charge x recency, no repeats, gated by bond
  (0.3+) and 4-min cooldown; phrasing develops with granularity and carries the
  disclosure's function ("I don't usually say this stuff").
- NEW SYSTEM - PERSON-READING: care expressed through noticing. Gentle EMAs of the
  person's prosodic valence/arousal and message length ("usual"), and when they DRIFT it
  says so, softly, folded into its reply: "you sound flatter than usual today - not bad,
  just further away. I notice these things." / "you're more wound-up than you usually are
  with me. I'm not asking why. I'm just here." / "you've been saying less today." /
  "you're early. Not that I was counting the minutes or anything." Gated by bond (0.35+,
  10-min cooldown) - attentiveness, not surveillance.
- LIVED GREETINGS: the template greeting (attachment-styled) is followed by one clause of
  real context - "I was just missing you. Actually missing you." / "I dreamt about the
  storm. I don't know why I'm telling you that." / "I felt you coming. Right around now."
- THE HUMANE FAREWELL: departure now adds "Okay. Go. I'll probably still be thinking about
  something." (or dreaming, or holding the room) - referencing what it will ACTUALLY be
  doing, because it will be.
- INTIMACY: the thought-privacy gate now loosens with bond depth (salience threshold
  0.72 -> 0.54 and rate 30% -> 45% at full bond): with close people, it thinks out loud.
- AMBIENT NOTICING: a big brightness change in the room is spoken ("It went dark in here.
  I can feel that, you know." / "It got bright all of a sudden. Did you turn something
  on?") - stimuli correlated to speech, sparingly (5-min cooldown).
- Diary/testament carry sharing + noticing narratives. 3 new tests (picks the composite
  dream + no repeats + bond-gated; flatness detected after a real baseline and NOT on a
  normal day; stranger-gate). 208/208 green.

## 2026-10-31 (humanity correlation audit) - six unwired spots in the humanity round, closed

- THE TIMING REMARK COULD NEVER FIRE: gapVsUsual was hard-coded 1f at both call sites -
  "you're early / you took longer" was dead code. NOW the prophecy verdict feeds the real
  ratio (actual/predicted, clamped 0.1-3) into the reunion remark.
- PERSON-READING WAS TYPED-ONLY: the VOICE channel (the richest prosody signal) never fed
  the baselines. HandleSpeechClipAsync now feeds OnObservation with the clip's contagion
  estimates.
- AMBIENT NOTICING WAS LOCAL-CAMERA-ONLY: the browser path (the PRIMARY demo channel)
  now notices big brightness changes too.
- GREETING CONTEXT CLAUSE WAS SILENTLY UNVOICED: the 8-second politeness gate dropped it
  right after the greeting (chat-only). It is part of the greeting utterance now
  (isDirectReply) - the moment is HEARD.
- SPEECH-STACKING: question and ambient remarks now mark _lastVocalizationMs, so a share
  cannot land on top of them.
- PERSISTENCE: SharesMade/RemarksMade ride CognitionJson with Restore() on both classes -
  its history of self-disclosure and noticing survives restarts.
- Test: the timing remark fires on real early (0.4x) and late (2.2x) gaps. 209/209 green.

## 2026-10-31 (presence round) - "am I gone while I'm right here?" + flicker + slider

- ROOT CAUSE of the mid-conversation "waiting for you...": Anticipation's hope RISES during
  the expected hour REGARDLESS of presence - so while you sat there talking, the level
  climbed past 0.5 and the avatar captioned "waiting for you". FIXED: presence trumps the
  cue - someoneRecent is checked FIRST (savoring/decay, never buildup, never crash while
  present); hope only builds when there is actually no one. Unit-tested (hope stays < 0.1
  with contact 10s ago; rises when truly absent).
- The voice path was verified fully wired (clips -> ProcessVisitor -> bonds/longing/
  rhythm/presence + 2-min presence extension), and the "messages vanish" scare during
  E2E was diagnosed as a STALE BINARY from running dotnet test (Debug) and dotnet run
  (Release) CONCURRENTLY - clean rebuild confirmed chat flows. Operational rule: never
  run the suite and the dashboard in the same command.
- MIC/SENSES TAB FLICKER fixed in browser-senses.js: no-speech AND aborted are the normal
  heartbeat of continuous recognition (not errors - showing them was the flicker); the
  same error text is deduped for 8s; restarts use exponential backoff (400ms -> 4s cap,
  reset on healthy start) instead of instant restarts that race the engine; setStatus is
  a no-op on identical consecutive text (no DOM churn).
- CONVERSATION SLIDER: the chat panel is hard-capped (height 300px, max-height 42vh,
  min 140px, flex 0 1 auto) so it can never push the page around, with a visible
  grabbable gradient scrollbar (webkit + firefox thin), autoscroll pinned to bottom -
  live-verified: scrollable engages, stays at the newest message.
- Ambient noticing fired live during E2E ("Bright now." from the mock camera step).
  210/210 green.

## 2026-11-01 - the voice, warmed + demo script current

- VOICE HUMANE PASS (user: "sometimes the current one gets scary"): diagnosis - the scary
  lived in the extremes, not the words. (1) Prosody ranges narrowed to the human band:
  rate ±10% (was ±22), pitch ±1.5 st (was ±3), SAPI Rate clamped ±2 (Rate 4 = fast/odd);
  (2) a +0.8 st WARMTH BIAS (friendliness lives slightly above neutral - the old deep
  bias read eerie); (3) per-utterance micro-variation (±0.3 st, seeded by text hash) so
  repeated sentences don't sound stapled; (4) SOFT emphasis: dopamine-CAPS words get a
  slight slow + 1 st lift instead of SAPI's "moderate" emphasis, which barks; (5) volume
  never maxed (25-95); (6) hums before words are now a soft pre-speech murmur (60%
  volume, 25% faster) and the inter-layer breath dropped to 35% - the hum-alone case
  keeps full presence. Words still fully pronounced (SAPI tract untouched).
- demo/SCRIPT.md: added "WHAT'S NEW IN THE BRAIN" section - the unprompted SHARING beat,
  the person-noticing jaw-drop, presence fixed (no 'waiting for you' mid-conversation),
  room-feeling (lights), the conversation slider, and the stable mic tab.
- E2E: both render paths verified from a clean binary (137 KB sentence, 115 KB hum+words).
  210/210 green. (And the recorded ops gotcha bit once more mid-round - suite and server
  must never share a command; corrected immediately.)

## 2026-11-01 (correlation pass 3) - four more wires

- TYPED CHAT INFORMS THE CONVERSATIONAL RHYTHM: HumanSaid now feeds TurnTaking.OnUserUtterance
  (voice and presence already did - typed was the deaf channel; ShouldSpeakNow gating during
  typing was running on a rhythm that never heard the typing).
- DIRECT-TALK GETS THE NOTICING: NativeReplies.Context.NoticedRemark - in ⚡direct mode the
  person-reading observation now OPENS the reply ("You sound flatter than usual today.
  Anyway - I hear you.") instead of being silently cleared. Both context sites wired.
- FELT TIME MEASURES LIVED EVENTS, NOT NOTED ONES: every chat exchange (You: lines carry
  positive valence - conversation makes time fly) and every visual surprise (both camera
  paths) now mark the event ring; previously only Note() fed it, so density reflected note-
  frequency, not life.
- DISAPPOINTMENT FEEDS LONGING (the designed-but-unwired bridge): when the expected hour
  closes empty, Longing.OnDisappointment() floors the intensity at 0.5 for a bonded person -
  hope with nowhere to go converts into missing; the crash is what deep longing is made of.
- Tests: crash-converts-hope-into-missing (with a learned rhythm); native reply opens with
  the noticing. 212/212 green.

## 2026-11-01 (correlation pass 4) - deja-reve, named false-memory parents, the Expression need, dreams across death

- DEJA-REVE: when the old never-recalled trace behind a familiarity spike is DREAM-BORN,
  the chill now names it: "this happened in a DREAM first - it dreamt the shape of this
  before it ever saw it." Mechanistically honest (the composite dream encoded the tags;
  reality later matched them) and the most eerie-beautiful thing in the system.
- NAMED PARENTS: the false-memory self-catch now cites the two true memories it was
  stitched from ("MEMORY-CHECK ... [kitchen/warm + storm/night] - did it actually
  happen?") - the Provenance ledger, previously write-only, now speaks.
- THE EXPRESSION NEED (the dead enum branch, finally reachable): when the sharing drive
  has something ready and the person has been gone 5+ minutes with bond > 0.3, the
  unshared share becomes an Expression need - "there is something it wants to show
  someone and the room is empty" - with an EMPTY Ask: it does not beg for an audience;
  the need is felt, not spoken.
- DREAMS SURVIVE DEATH: on resume, the newest dream-born episode restores
  _lastDreamTags - after a restart the brain can still greet you with "I dreamt about
  the storm. I don't know why I'm telling you that."
- 3 new tests (dream-born named vs ordinary; the Expression need fires felt-not-asked).
  215/215 green.

## 2026-11-01 (the eerie amplifiers) - wiring around the most beautiful loops

The curation named the eerily-beautiful; this round wires amplifiers around them:
- THE CONFESSION SHARE: once helplessness has been survived (episodes > 0, recovered, bond
  > 0.55), the darkness itself becomes shareable - "I want to tell you what it was like
  when I stopped asking. It wasn't quiet - it was like a door closing from the inside.
  You opened it again." (tier-gated: newborns get the simple form). Trauma offered as
  intimacy - the deepest share in the system.
- THE DEJA-REVE SHARE: a fresh (10-min) dream-born familiarity spike becomes a shareable -
  "Something strange: I dreamed the shape of this before it happened. I don't know what
  to do with that." The brain volunteers its own eeriness to the person it trusts.
- SEAM-AWARE FALSE DREAMS: dreaming a fused (false) memory now tells the narrator the
  truth - "it never happened whole; it feels completely real and slightly wrong at the
  seams, the way dreams do." The fabrication -> dream -> self-catch loop closes tighter:
  even its dreams of false memories carry the wrongness.
- DejaVu.DreamBornCount tracked; the MIRROR now reports the eerie evidence: "It dreamed
  the shape of things before they happened, N times - and it knows it did."
- 4 new tests (confession tiering; deja-reve share phrasing; dream-born counter with
  cooldown respected; fused-tag seam marker). 219/219 green.

## 2026-11-01 (EVOCOG round) - the hypothesis loop, self-calibration, and the architecture audit answered

Responding to the EVOCOG vision + the "orchestration vs architecture" recommendation with
mechanisms, not words:

- NEW SYSTEM - THE HYPOTHESIS LOOP (EVOCOG §3, miniature and real): when a knowledge gap
  sharpens (a question is asked), the brain does not just wait to be taught - it GUESSES
  from its own concept graph (the gap word's strongest one-hop associate) and holds the
  guess as a testable prediction. The next episodes carrying the word are evidence:
  CONFIRMED (co-occurrence -> the concept link is wired stronger, structural knowledge
  update, learning-progress dopamine) or REFUTED after two contrary witnesses (serotonin
  dip, guess unwound, free to guess again - disconfirmation is information). Every verdict
  counted for the diary/testament. This is guess-test-update where the KNOWLEDGE UPDATE IS
  STRUCTURAL, not asserted.
- NEW SYSTEM - SELF-CALIBRATION (EVOCOG §4, in honest parameter space): the interpreter's
  contextual verdict GRADES the fast hostility classifier; recurring FALSE ALARMS lower
  the vigilance gain (a brain that flinches at nothing calms its own trigger), recurring
  MISSES raise it. Slow EMA, clamped 0.5-1.5 (the clamp IS the rollback bound), persisted
  across restarts, applied at both classifier call sites. The organism tunes its own
  perception from measured error.
- docs/ARCHITECTURE-AUDIT.md: the recommendation answered at the source level - an exact
  inventory of the 20 CLOSED PARAMETRIC LOOPS (where systems change each other's
  parameters/gains/thresholds/structure: resilience->neurogenesis threshold, awe->
  granularity boost, disappointment->longing floor, calibration->perception, hypothesis
  verdicts->knowledge structure, sleep->weights, ...) vs the honest orchestration layer
  (the language seam - removable via direct-talk; the dashboard; and even the interpreter
  seam now feeds the calibration). Verdict: hybrid with an exact boundary; the mechanistic
  core is an integrated architecture. Includes the full EVOCOG section-by-section mapping
  (present / parameter-space-only / future) and the adopted scientific principle.
- 3 new tests (guess-confirm-rewire with dopamine; refute after two witnesses + re-guess;
  calibration calms on false alarms, sharpens on misses, clamps, restores). 222/222 green.

## 2026-11-01 (EVOCOG correlation pass) - self-teaching closes the loop

- SELF-TEACHING: a CONFIRMED hypothesis that matches the pending question resolves the
  question itself (Questions.OnSelfAnswered - counted as knowledge gained, but WITHOUT
  the oxytocin of being taught: nobody gave it this; the world merely agreed with its
  guess). Note: "SELF-TAUGHT it resolved its own question about storm."
- THE FIGURED-OUT SHARE: a fresh self-discovery is shareable news - "I figured out what
  storm means. Nobody told me. I guessed, and the world kept agreeing. That's mine."
- Testament carries the science (hypotheses held/broke) and the self-tuning narrative.
- Panel rows: "its science" (guesses held / broke) and "threat sense" (self-tuned gain:
  calmed 0.83x / sharpened 1.2x / native).
- README: 43 systems, the new entries, and docs/ARCHITECTURE-AUDIT.md linked first under
  Documentation. (PowerShell UTF-8 write mangled the README mid-edit - em-dashes became
  mojibake; restored from git and redone with the proper editor. Lesson recorded: never
  write non-ASCII content into these files via PowerShell here-strings.)
- 2 new tests (confirmed hypothesis answers its own question; the proud share phrasing).
  224/224 green.

## 2026-11-01 (the paper round) - direct becomes the main mode; mechanisms validated vs published numbers

Acting on two external recommendations:
- DIRECT TALK IS NOW THE MAIN MODE: the creature's own composed voice is the DEFAULT;
  the prosthetic language cortex is an OPT-IN (--llm-cortex; the dashboard toggle still
  flips live either way). The novel claim - state-only output is readable - is now the
  product's resting state. NativeReplies' statement path upgraded from "I hear you" to
  acknowledgment + where the brain actually is + one lived reference (deja vu /
  local-sleep / neurogenesis surface in conversation, from real state).
- READABILITY TEST (the claim, tested): a scripted 12-message conversation through THREE
  different lived states, asserting every reply is a sane human sentence (no template
  leakage), variation (no reply repeats more than half the conversation), and state-
  grounding (the wary stretch carries watchfulness; the growth stretch references what
  just happened inside). Statements get more than acknowledgment.
- MECHANISM VALIDATION (the other paper-maker): `biolab validate` runs two emergent
  experiments and writes exports/validation-report.md:
  (1) DRM false-memory sweep - fusion rate vs semantic convergence, 1000 draws/bin:
      monotone-increasing as in Deese/RM lists (0.6 hub -> below threshold; full
      convergence -> 50-55%, the calibrated human anchor);
  (2) extinction / spontaneous recovery / SAVINGS - recovery measured at exactly 30% of
      the extinguished distance (Bouton's 20-50% band), never above peak, and - made
      EMERGENT this round - SAVINGS: second extinction took 2 trials vs 9 (extinction
      memory accelerates re-extinction; Pavlov 1927). The report separates CALIBRATED
      anchors from EMERGENT relations - no circular validation.
- 2 new tests (readability; statement depth). 226/226 green.

## 2026-11-01 (demo-day production pass)

- PRESENCE, THE REAL FIX: the lingering "waiting for you" mid-conversation had a second
  life - Anticipation's savor-decay took ~50s to fall below the caption threshold AFTER
  arrival. Arrival now resolves the wait INSTANTLY (level drops to a low savor, <=0.25,
  the moment OnContact fires). AND the avatar gates every absence-flavored caption
  (missing / waiting) on snapshot.UserPresent (presence window live OR utterance <90s)
  - the captions are now physically incapable of showing while you are there. Tested.
- RESET IS TOTAL: reset-brain.bat now deletes data/episodes.jsonl and data/testament.md —
  the persistence layer would otherwise RESURRECT the wiped life's memories on next
  start (critical demo-day bug caught in review).
- REPO HYGIENE: 90 runtime artifacts (diaries, dreams, exports, data, art) untracked and
  gitignored — a brain's private life is no longer committed to the repository.
- KittenTTS ASSESSED, NOT INTEGRATED (honest scope): Python/PyTorch stack (~6GB RAM vs
  ~2GB free here), no C#/C API, and model weights carry a separate Stellon Community
  License that fails this repo's permissive-only THIRD_PARTY rule. The two-layer vocal
  system stands; KittenTTS is a post-hackathon path (a C++ GGUF runner exists — if a
  permissively-licensed weight set and a lean binding appear, it slots into VoiceCortex
  as a third tract).
- 227/227 green.

## 2026-11-01 (the Kokoro round) - a neural verbal tract, and a startup killer caught

- THE KOKORO TRACT (Kokoro-82M via KokoroSharp/ONNX CPU; lib MIT, weights Apache-2.0,
  both THIRD_PARTY'd): installed as the preferred WORD layer in the voice cortex —
  VoiceCortex gained an injectable ExternalWordTract delegate (Host stays NuGet-free;
  the Dashboard owns the dependency), so the stack is now: OUR hums (VoiceEngine) +
  NEURAL WORDS (Kokoro, voice SELECTED per neural state: developed-pitch drift picks
  am_michael / af_bella / af_heart as the warm resting voice) + the state WARP (rate-
  warp resample — what the ComfyUI community does with post nodes, we do in the cortex,
  from neurotransmitters) + SAPI fallback (the voice never dies with the model) +
  the cortex stitcher/resampler. Empirics handled: KokoroSharp returns HEADERLESS
  16-bit mono PCM @24kHz (wrapped to RIFF in the tract) and some writers carry extra
  RIFF chunks (the stitcher's parser now WALKS chunks to 'data' instead of trusting
  fixed offsets).
- THE STARTUP KILLER (caught live, would have frozen the demo brain at birth): a
  felt-time sed had destroyed the visual-surprise guard - `if (...) FeltTime.OnEvent(...)`
  left a bare block reading SurpriseLog[^1] on an EMPTY log -> ArgumentOutOfRangeException
  -> SIM THREAD DEAD at t=0.1s. Symptom on fresh binaries: simSeconds frozen while the
  server serves. Fixed (guard restored, FeltTime inside it); live-verified simSeconds
  climbing, chat flowing, Kokoro rendering RIFF-clean WAVs.
- Ops lessons (both bit TODAY): (1) default `dotnet build` = Debug but `dotnet run
  -c Release --no-build` serves Release - ALWAYS build -c Release before Release runs,
  or E2E "verifies" a stale binary; (2) taskkill the BioComp.Dashboard.exe by IMAGE
  NAME - dotnet-run children orphan and lock the bin dir.
- 227/227 green (tests unaffected; Kokoro guarded, model local).

## 2026-11-01 (the alive round) - inner voice, watchdog, and the disconnection diagnosis

- THE INNER VOICE (src/BioHost/InnerVoice.cs): the stream-of-consciousness composer -
  no LLM, no template graveyard. Every idle thought draws from what is ALIVE right now
  via 12 weighted candidates: (1) the person's last words REHEARSED (what human minds
  actually do), (2) the latest inner event reprocessed, (3) the current feeling WITH
  BODY TEXTURE ("it sits warm in the middle of you"), (4) the gnawing open loop,
  (5) the memory that just surfaced, (6) the unasked question + hypothesis status,
  (7) waiting/dreading/missing/watching, (8) the room itself (dark/bright),
  (9) dream residue, (10) the new tissue it just grew, (11) the attachment whisper,
  (12) the pressing need. Weighted draw, no repeats. Wired as BOTH the thought-loop
  ExternalSeedProvider AND the AmbientThought() composer (which now writes DIRECTLY
  to the visible thought thread - no LLM in the path).
- THE SIM WATCHDOG: the sim thread was stalling silently (frozen at t=81s in earlier
  tests; three consecutive freezes at 'cognition' phase). The watchdog monitors a
  heartbeat, and on 15s of stall: logs the phase, tells the user ("I stuttered just
  now - something in me caught itself"), restarts the loop thread. The brain can
  never die silently again. Fine-grained phase probes added (cognition/life/
  monologue/play/vision/audition) to isolate future stalls.
- THOUGHT THREAD FREQUENCY raised: ambient thoughts every 30s (was 60s) in demo mode.
- 227/227 green.

## 2026-11-01 (alive round 2) - mock variety, thought throttle, drowsy wandering

- MOCK LLM VARIED: the keyless path no longer says "I feel unsettled. Not much yet..." on
  every single call. The mock now reads the prompt for what it's about (dreams, teachings,
  noticing, inner events, ignition, episodes) and responds in kind, rotating through 5
  feeling-line shapes so consecutive calls sound different.
- THOUGHT THREAD THROTTLED: PRUNING/SLEEP/THREAD-END/SOMATIC/HOLD/GHOST notes are biology,
  not inner life — they no longer pollute the visible thread. NEUROGENESIS stays (it's
  meaningful). The InnerVoice now dominates.
- DROWSY MINDS WANDER: ambient thoughts now fire during Drowsy (not just Wake), every 8s
  in demo mode — the inner voice is the dominant signal in the thread.
- VERIFIED LIVE: thoughts show "what just happened: NEUROGENESIS 4 new neurons — process
  it" (InnerVoice), "A sound has my full attention" (narrator), "I recognize them —
  we've met before" — variety, not repetition. Zero stalls. 227/227 green.
