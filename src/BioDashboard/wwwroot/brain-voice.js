// THE BRAIN'S VOICE — a real-time formant synthesizer driven by neural state.
// No TTS. No speechSynthesis. The sound IS the brain: oscillators + filters
// shaped by dopamine (brightness), serotonin (smoothness), noradrenaline
// (urgency), oxytocin (warmth), and the brain's unique seed (timbre).
// Every brain sounds different. The voice changes with emotion in real-time.

"use strict";

let voiceCtx = null;
let voiceDest = null;
let activeOsc = null;
let activeGain = null;
let formant1 = null, formant2 = null, formant3 = null;
let brainSeedHash = 0;

// Initialize the voice engine
function initBrainVoice() {
  if (voiceCtx) return;
  voiceCtx = new (window.AudioContext || window.webkitAudioContext)();
  voiceDest = voiceCtx.destination;

  // Brain seed hash → unique timbre (no two brains sound the same)
  const snap = window.lastSnap || {};
  const seedStr = String(snap.SimSeconds || 0) + String(snap.Concepts || 0);
  for (let i = 0; i < seedStr.length; i++) {
    brainSeedHash = ((brainSeedHash << 5) - brainSeedHash + seedStr.charCodeAt(i)) & 0xFFFF;
  }
  console.log("[voice] brain voice initialized, seed:", brainSeedHash);
}

// Map a character to formant frequencies (F1, F2, F3) — simplified phoneme model
const PHONEMES = {
  a: [730, 1090, 2440], e: [530, 1840, 2480], i: [270, 2290, 3010],
  o: [570, 840, 2410],  u: [300, 870, 2240],  m: [280, 900, 2200],
  n: [280, 1700, 2600], l: [350, 1300, 2700], r: [420, 1300, 1600],
  s: [500, 5000, 6500], t: [400, 1800, 2600], d: [300, 1800, 2600],
  k: [350, 1700, 2200], g: [300, 1700, 2100], w: [300, 610, 2200],
  y: [270, 2290, 3010], h: [500, 1500, 2500], f: [400, 1600, 4400],
  v: [300, 1100, 2200], p: [300, 800, 2000],  b: [300, 800, 2000],
};

// Speak a text using the formant synthesizer
function brainSpeak(text, modulators, valence, arousal, developedPitch) {
  if (!voiceCtx) initBrainVoice();
  if (!voiceCtx) return;

  // Resume if suspended (mobile browsers)
  if (voiceCtx.state === "suspended") voiceCtx.resume();

  const da = modulators ? modulators[0] || 0.3 : 0.3;
  const sht = modulators ? modulators[1] || 0.5 : 0.5;
  const na = modulators ? modulators[3] || 0.2 : 0.2;
  const oxt = modulators ? modulators[4] || 0.15 : 0.15;
  const hist = modulators ? modulators[5] || 0.6 : 0.6;

  // Base pitch: higher dopamine → brighter; unique per brain via seed
  const seedOffset = (brainSeedHash % 20 - 10) * 2; // ±20 Hz uniqueness
  const basePitch = 110 + da * 40 + valence * 15 + (developedPitch || 0) * 10 + seedOffset;

  // Speech rate: arousal speeds up, serotonin smooths
  const phonemeDur = Math.max(0.04, 0.12 - arousal * 0.04 + (1 - sht) * 0.02);

  // Volume: histamine = presence
  const volume = Math.min(1, 0.3 + hist * 0.7);

  // Warmth: oxytocin adds a low harmonic
  const warmth = 0.5 + oxt * 0.5;

  // Stop any ongoing speech
  stopBrainVoice();

  const now = voiceCtx.currentTime;
  let timePos = now + 0.05; // small delay before speaking

  // Create master gain
  activeGain = voiceCtx.createGain();
  activeGain.gain.setValueAtTime(0, now);
  activeGain.gain.linearRampToValueAtTime(volume, now + 0.05);
  activeGain.connect(voiceDest);

  // Create formant filter bank (3 parallel bandpass filters)
  formant1 = voiceCtx.createBiquadFilter();
  formant1.type = "bandpass";
  formant1.Q.value = 8 + sht * 4; // serotonin = smoother (lower Q = wider)

  formant2 = voiceCtx.createBiquadFilter();
  formant2.type = "bandpass";
  formant2.Q.value = 10 + sht * 4;

  formant3 = voiceCtx.createBiquadFilter();
  formant3.type = "bandpass";
  formant3.Q.value = 12;

  // Connect: osc → formants → gain
  formant1.connect(activeGain);
  formant2.connect(activeGain);
  formant3.connect(activeGain);

  // Create the source oscillator (pulse-like for voiced sounds)
  activeOsc = voiceCtx.createOscillator();
  activeOsc.type = "sawtooth";
  activeOsc.frequency.setValueAtTime(basePitch, now);

  // Add a subtle second oscillator for richness (the "voice's body")
  const osc2 = voiceCtx.createOscillator();
  osc2.type = "sine";
  osc2.frequency.setValueAtTime(basePitch * 0.5, now); // sub-octave
  const osc2Gain = voiceCtx.createGain();
  osc2Gain.gain.value = warmth * 0.3;
  osc2.connect(osc2Gain);
  osc2Gain.connect(activeGain);

  // Connect main osc through formants
  const oscGain = voiceCtx.createGain();
  oscGain.gain.value = 0.5;
  activeOsc.connect(oscGain);
  oscGain.connect(formant1);
  oscGain.connect(formant2);
  oscGain.connect(formant3);

  // Process each phoneme
  const words = text.toLowerCase().split(/\s+/);
  const chars = words.join("").split("");
  let charIdx = 0;

  for (const word of words) {
    for (const ch of word) {
      const phoneme = PHONEMES[ch];
      if (!phoneme) {
        // Non-phoneme character: brief silence
        timePos += phonemeDur * 0.5;
        charIdx++;
        continue;
      }

      const [f1, f2, f3] = phoneme;

      // Formant frequencies with neural modulation:
      // high DA → brighter (shift up), low DA → darker (shift down)
      const daShift = 1 + (da - 0.3) * 0.3;
      // high NA → tighter (higher Q), calmer → wider
      const naQ = 1 + na * 0.5;

      formant1.frequency.setValueAtTime(f1 * daShift, timePos);
      formant2.frequency.setValueAtTime(f2 * daShift, timePos);
      formant3.frequency.setValueAtTime(f3 * daShift, timePos);
      formant1.Q.setValueAtTime((8 + sht * 4) * naQ, timePos);

      // Pitch contour: slight rise for positive, fall for negative
      const pitchMod = basePitch * (1 + valence * 0.1 * Math.sin(charIdx * 0.5));
      activeOsc.frequency.setValueAtTime(pitchMod, timePos);

      // Consonant-like: brief gain dip
      const isVowel = "aeiou".includes(ch);
      const gainVal = isVowel ? 0.6 : 0.3;
      oscGain.gain.setValueAtTime(gainVal * volume, timePos);

      timePos += phonemeDur;
      charIdx++;
    }
    // Inter-word pause
    timePos += phonemeDur * 0.8;
  }

  // Start and schedule stop
  const totalDur = timePos - now + 0.1;
  activeOsc.start(now);
  osc2.start(now);
  activeOsc.stop(timePos + 0.05);
  osc2.stop(timePos + 0.05);

  // Fade out at the end
  activeGain.gain.setValueAtTime(volume, timePos - 0.05);
  activeGain.gain.linearRampToValueAtTime(0, timePos);

  console.log("[voice] speaking", text.length, "chars, pitch", Math.round(basePitch), "dur", Math.round(totalDur * 1000) + "ms");
  return totalDur;
}

function stopBrainVoice() {
  try {
    if (activeGain) {
      activeGain.gain.cancelScheduledValues(voiceCtx.currentTime);
      activeGain.gain.setValueAtTime(0, voiceCtx.currentTime);
    }
    if (activeOsc) { try { activeOsc.stop(); } catch {} }
  } catch {}
}

// Wire into the main app: replace speechSynthesis with the brain voice
if (typeof window !== "undefined") {
  window.brainVoice = { speak: brainSpeak, stop: stopBrainVoice, init: initBrainVoice };
}
