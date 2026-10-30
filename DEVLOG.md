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
