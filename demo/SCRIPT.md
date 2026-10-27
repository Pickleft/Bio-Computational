# Demo video script (3 minutes)

Recording: OBS on the demo machine (the one with webcam + mic). 1920x1080, 30 fps.
Capture system audio (TTS voice) + mic (the human). Practice the run twice before
recording. One brain, one screen, one story.

Setup before hitting record:

```
dotnet run --project src/BioDashboard -- --urls=http://localhost:8080 --demo --voice --mock-cam --mock-mic
```

Open http://localhost:8080/?demo=1 . Let it run until the brain is asleep (top-left
pill shows NREM, roughly 15-20 s in demo mode; the visitor arrives each cycle at the
45 percent mark of the 2-minute demo day - time your start so it is asleep first).

## Timeline

**0:00-0:15 hook.** Dashboard in idle/sleep, raster drifting. Say:
"Every AI you've used blinks out of existence between requests. This one doesn't. It's
a spiking brain that runs continuously - it idles, it remembers, and right now it's
asleep. Watch the thought feed."

**0:15-0:45 idle life.** Point at the panels: spike raster per region ("this is a
6,000-neuron network at criticality - avalanches, not noise"), the plain-thoughts
panel ("I'm sinking into deep sleep... I'm dreaming now"), modulator bars, the
feeling dot. Mention: "All deterministic, seeded, reproducible - pure C#."

**0:45-1:30 wake-on-surprise.** The visitor enters (mock world in rehearsal; a real
person walking in for the live take with --camera --mic). Read the feed as it fires:
"Someone walks into the room" -> SURPRISE -> SIGHT person -> HEARD "is anyone there"
-> IGNITION -> "I'm awake again" - no command given, nothing scripted in the brain.
If the monologue panel shows a line, read it out.

**1:30-2:00 memory and dreams.** Episodes panel: the visitor became a memory. Replay
entries: "A memory comes back". Sleep returns, then the dreams panel gets an image:
"it dreams about the person it met - and the dream re-enters its own visual field."

**2:00-2:20 talk to it.** Type in the chat box: "what happened while you were away?"
It answers from its real episode store. (In rehearsal this is the mock interpreter;
with --live-meta + key it's Spark - record whichever is working at record time.)

**2:20-2:45 the science.** Show exports: mmn.svg ("deviant tones produce prediction
errors 12 sigma above standards"), conditioning_predictor.svg ("reward prediction
rises with pairing, decays in extinction - Schultz's curve"), the Wonder report
(reports/report-*.md): "an agent runs the experiments and writes this itself."

**2:45-3:00 close.** "Deterministic, keyless by default, 70 tests, fully open source -
link below. The code is on GitHub. Bio-Computational."

## Viral clip (30 s, separate recording)

Brain asleep -> person walks in (real webcam take) -> "I'm awake again" appears ->
it speaks (--voice). Cut on the moment it reacts. Caption: "it woke up when I walked
in. nobody pressed anything."

## Checklist

- [ ] OBS scene saved; audio levels checked (TTS audible, mic not clipping)
- [ ] Run rehearsed twice; visitor timing known
- [ ] --live-meta only if the key is verified working (else stay mock, do not apologize for it)
- [ ] Repo link in the description + video description mentions the hackathon
- [ ] Export 1080p; check the thought-feed text is legible at full screen (use ?demo=1)
