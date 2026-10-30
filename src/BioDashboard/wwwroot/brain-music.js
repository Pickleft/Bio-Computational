// Brain music: neurotransmitter levels become audible composition.
// DA = melody (higher DA = more notes, ascending patterns)
// 5-HT = harmony (higher serotonin = more consonant intervals)
// NA = tempo (higher noradrenaline = faster rhythm)
// ACh = timbre (higher acetylcholine = brighter, more complex waveforms)
// Histamine = volume (wakefulness = louder, sleep = quieter)

"use strict";

let musicCtx = null;
let isPlaying = false;
let masterGain = null;
let oscillators = [];

function initAudio() {
  if (musicCtx) return;
  musicCtx = new (window.AudioContext || window.webkitAudioContext)();
  masterGain = musicCtx.createGain();
  masterGain.gain.value = 0.15;
  masterGain.connect(musicCtx.destination);
}

// musical scale (pentatonic for consonance)
const SCALE = [220, 247.5, 275, 330, 371.25, 440, 495, 550]; // A minor pentatonic

function noteForDA(da) {
  // DA 0-1.5 maps to scale index 0-7
  const idx = Math.floor(da * SCALE.length / 1.5) % SCALE.length;
  return SCALE[idx];
}

function playTone(freq, duration, type, gainVal) {
  if (!musicCtx || !isPlaying) return;
  const osc = musicCtx.createOscillator();
  const gain = musicCtx.createGain();
  osc.type = type;
  osc.frequency.value = freq;
  gain.gain.setValueAtTime(0, musicCtx.currentTime);
  gain.gain.linearRampToValueAtTime(gainVal, musicCtx.currentTime + 0.05);
  gain.gain.exponentialRampToValueAtTime(0.001, musicCtx.currentTime + duration);
  osc.connect(gain);
  gain.connect(masterGain);
  osc.start(musicCtx.currentTime);
  osc.stop(musicCtx.currentTime + duration);
}

let lastNoteTime = 0;
let noteInterval = 500; // ms between notes, adjusted by NA

function brainMusicTick(s) {
  if (!isPlaying || !s) return;

  const da = s.Modulators[0] || 0.3;
  const sht = s.Modulators[1] || 0.5;
  const na = s.Modulators[3] || 0.2;
  const ach = s.Modulators[2] || 0.3;
  const hist = s.Modulators[5] || 0.6;

  // tempo: NA drives note interval (high NA = fast = 200ms, low = 1000ms)
  noteInterval = Math.max(150, 1000 - na * 600);

  // volume: histamine (wakefulness) controls loudness
  if (masterGain) masterGain.gain.value = 0.02 + hist * 0.13;

  const now = performance.now();
  if (now - lastNoteTime < noteInterval) return;
  lastNoteTime = now;

  // melody from DA
  const melodyNote = noteForDA(da);
  const timbre = ach > 0.5 ? "triangle" : "sine"; // ACh = brightness
  playTone(melodyNote, noteInterval * 0.8, timbre, 0.3);

  // harmony from serotonin: consonant (same note lower octave) or dissonant (tritone)
  if (sht > 0.5 && Math.random() < 0.6) {
    // consonant: perfect fifth below
    playTone(melodyNote * 0.667, noteInterval * 1.2, "sine", 0.15);
  } else if (sht < 0.3 && Math.random() < 0.4) {
    // dissonant: tritone
    playTone(melodyNote * 1.414, noteInterval * 0.5, "sawtooth", 0.08);
  }

  // occasional deep bass when sleep pressure is high
  if (s.SleepPressure > 0.5 && Math.random() < 0.2) {
    playTone(110, 800, "sine", 0.1);
  }
}

// toggle button
function toggleBrainMusic() {
  if (!musicCtx) initAudio();
  if (musicCtx.state === "suspended") musicCtx.resume();
  isPlaying = !isPlaying;
  const btn = document.getElementById("music-toggle");
  btn.textContent = isPlaying ? "◉ playing" : "♪ listen";
  btn.classList.toggle("active", isPlaying);
}
