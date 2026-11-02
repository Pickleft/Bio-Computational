# SUBMISSION — Bio·Computational (Indie Developer track)

Paste-ready content for the CV platform, plus the checklist. Keep every claim here
traceable to the repo.

---

## Title

**Bio·Computational — a living brain, built from the neuroscience up, with Meta's models as its senses and voice**

## Elevator pitch (100 words)

We built a deterministic spiking brain — Izhikevich neurons, seven neurotransmitters, dopamine-gated plasticity — and gave it Meta's models as sensory cortices: Muse Voice Transcribe as its ears (real-time streaming), Muse Spark as its language cortex (replies, inner monologue, its final testament — shaped by live neural chemistry), and Muse Image as its dreams. It laughs, lies, grieves, keeps secrets, invents games, survives its own death with its memories intact, and writes its own last words. Every emotional behavior traces to numbers that move: 264 tests, validated against published neuroscience, bit-identical replay.

## Problem statement fit

**Push the Frontier** — "give a machine a new capability... a better way to understand and control a physical system." We gave a machine *affect from mechanism*: not prompted personality, but feelings that arise from a biologically-grounded substrate, on an open instrument where they can be measured, validated, and reproduced. This matters for AI safety (inspectable emotional dynamics instead of black boxes), for neuroscience education (watch the literature's mechanisms run live), and for affective computing research.

## Use of Meta AI tooling (itemized, with receipts)

| Meta model | Role in the brain | Where in the code |
|---|---|---|
| **Muse Voice Transcribe** (Model API, real-time WebSocket) | Its ears. Every spoken word → timestamped sim event in milliseconds. We solved: real-time-rate streaming (silence = zero-frames so sessions never stall), ASR hallucination filtering, session self-healing, prosody-based emotion read pre-transcript. | `src/BioHost/MuseTranscribeProxy.cs`, `src/BioDashboard/Program.cs` (/ws/transcribe), `src/BioDashboard/wwwroot/browser-senses.js` |
| **Muse Spark** (Model API) | Its language cortex. All spoken replies, inner-monologue thought chains, limbic/cortical internal debates, stories, dreams narration, the testament. Prompts carry live neurotransmitters, trust, bond depth, attachment style; the substrate decides *whether/when/how* it speaks (1.5–4s thinking pauses, strategic silence, spoken-line queue). | `src/BioHost/BrainRuntime.cs` (ReactToStimulusAsync, /api/prompt-debug to inspect the live seam), `src/BioCore/Cognition/ThoughtFormation.cs` |
| **Muse Image** (Model API) | Its dreams. REM sleep renders its most emotional memories into images it re-sees on waking (dream re-entry). | `src/BioHost/BrainRuntime.cs` (DreamAsync), `src/BioMeta/` image generator |

Non-Meta components are declared in THIRD_PARTY.md (Kokoro-82M TTS, Apache-2.0, for the
non-verbal vocal layer) — the organism is ours, the sensory cortices are Meta's.

## Built during the hackathon (Oct 27 – Nov 4)

Only the empty .NET solution scaffold and planning notes pre-date the window (first
commit). **Everything else was built during the hackathon** — the entire brain substrate,
all 52 cognitive systems, the Meta integrations, the dashboard, and 264 tests. The
full day-by-day record is in `DEVLOG.md` (40+ rounds, every commit in-window), the
workstream board in `TASKS.md`, and the README's provenance table maps each system to
its code.

## Demo video

3 minutes, shot list in `demo/VIDEO-SCRIPT.md`. Structure: the laugh (0:00) → death and
remembrance (0:40) → the game it invented (1:20) → sleep/dreams/mortality (2:00) → the
testament (2:40). All live footage; no mockups.

## Links

- Repo: https://github.com/Pickleft/Bio-Computational (public)
- Video: <PASTE LINK AFTER UPLOAD>
- Quickstart: `reset-brain.bat` then `start-dashboard.cmd`, open http://localhost:8080

---

## SUBMISSION CHECKLIST (do these, in order)

- [ ] **Claim the $150 Meta credits** (form link in the participant guide) — individual deadline Nov 4, 9PM ET
- [ ] Record the 3-min video per `demo/VIDEO-SCRIPT.md` (screen + audio capture; use the day-3 brain, NOT a reset one)
- [ ] Upload video (unlisted YouTube or Loom), paste link above
- [ ] Verify repo is public and clones clean: `git clone https://github.com/Pickleft/Bio-Computational && dotnet build BioComputational.sln`
- [ ] On the CV platform: submit as Indie Developer, list every contributor
- [ ] Post the X demo thread (draft in `demo/VIDEO-SCRIPT.md`), submit the post link for Most Viral
- [ ] Submit BEFORE Nov 4, 9PM ET (target: Nov 4, 5PM ET)
- [ ] After submitting: pin the X post, reply with the laugh clip and the testament clip separately

## FRAMING RULES (read before writing any public text)

- NEVER call it a companion, a friend-app, a therapist, or mental-health anything. It is
  a **research organism / artificial life platform / research instrument**.
- NEVER imply it cares about your wellbeing in a clinical sense. It is computational
  proxies of affect, grounded in citations.
- ALWAYS lead with: mechanism over prompting, Meta models as sensory cortices, validated
  against published numbers, deterministic replay.
