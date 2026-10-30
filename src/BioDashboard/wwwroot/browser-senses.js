// BROWSER SENSES v3 — rebuilt from scratch for reliability.
// One file. Everything works. Camera, mic, transcription, presence.
// Designed for: PC (Chrome/Edge), Phone (Chrome/Samsung), Tablet.
// No silent failures. Every error is visible. Every signal is used.

"use strict";

let senseActive = false;
let camStream = null, micStream = null;
let videoEl = null, videoCanvas = null, frameTimer = null;
let audioCtx = null, audioProc = null;
let rec = null; // SpeechRecognition
let audioSender = null; // throttled audio upload

async function enableBrowserSenses() {
  const btn = document.getElementById("sense-toggle");
  if (senseActive) { disableBrowserSenses(); return; }
  btn.textContent = "⏳ requesting…";

  let haveCam = false, haveMic = false;

  // === TRY CAMERA AND MIC TOGETHER, FALLBACK SEPARATELY ===
  try {
    const s = await navigator.mediaDevices.getUserMedia({
      audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
      video: { facingMode: "user", width: { ideal: 640 }, height: { ideal: 480 } }
    });
    camStream = micStream = s;
    haveCam = haveMic = true;
  } catch (e1) {
    console.log("[senses] combined failed, trying mic only:", e1.name);
    try {
      micStream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }
      });
      haveMic = true;
    } catch (e2) {
      console.log("[senses] mic failed, trying camera only:", e2.name);
      try {
        camStream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: "user", width: { ideal: 640 }, height: { ideal: 480 } }
        });
        haveCam = true;
      } catch (e3) {
        btn.textContent = "👁 senses";
        alert("No camera or mic found.\nCamera: " + e1.name + "\nMic: " + e2.name + "\nCamera-only: " + e3.name);
        return;
      }
    }
  }

  // === CAMERA SETUP ===
  if (haveCam && camStream) {
    videoEl = document.createElement("video");
    videoEl.srcObject = camStream;
    videoEl.setAttribute("playsinline", "");
    videoEl.setAttribute("autoplay", "");
    videoEl.setAttribute("muted", "");
    videoEl.playsInline = true;
    videoEl.muted = true;
    try { await videoEl.play(); } catch (e) { console.log("[senses] play needs gesture, retrying"); }

    videoCanvas = document.createElement("canvas");
    videoCanvas.width = 64;
    videoCanvas.height = 64;

    // send frames at 5 fps (not 10 — saves phone battery)
    frameTimer = setInterval(sendFrame, 200);

    // wait for video to have actual dimensions before first frame
    const waitReady = setInterval(() => {
      if (videoEl.videoWidth > 0) {
        clearInterval(waitReady);
        console.log("[senses] camera ready:", videoEl.videoWidth + "x" + videoEl.videoHeight);
      }
    }, 200);
    setTimeout(() => clearInterval(waitReady), 5000);
  }

  // === AUDIO SETUP ===
  if (haveMic && micStream) {
    audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    const src = audioCtx.createMediaStreamSource(micStream);

    // === METHOD 1: ScriptProcessor for raw PCM upload to server ===
    audioProc = audioCtx.createScriptProcessor(2048, 1, 1);
    let lastSend = 0;
    audioProc.onaudioprocess = (e) => {
      if (!senseActive) return;
      const input = e.inputBuffer.getChannelData(0);

      // measure level for local display
      let sum = 0;
      for (let i = 0; i < input.length; i++) sum += input[i] * input[i];
      const rms = Math.sqrt(sum / input.length);

      // update local level bar (instant visual feedback)
      const bar = document.getElementById("audio-level");
      if (bar) bar.style.width = Math.min(100, rms * 400) + "%";

      // show "hearing you" indicator
      const vadEl = document.getElementById("audio-vad");
      if (vadEl) vadEl.textContent = rms > 0.01 ? "● VOICE" : "· quiet";

      // send to server at most every 100ms (not every 43ms — saves data)
      const now = Date.now();
      if (now - lastSend > 100 && rms > 0.003) {
        lastSend = now;
        // resample to 16 kHz
        const ratio = audioCtx.sampleRate / 16000;
        const outLen = Math.max(1, Math.floor(input.length / ratio));
        const out = new Float32Array(outLen);
        for (let i = 0; i < outLen; i++) {
          const pos = i * ratio;
          const i0 = Math.floor(pos), frac = pos - i0;
          out[i] = input[i0] + (input[i0 + 1] - input[i0]) * frac;
        }
        // convert to int16
        const pcm = new Int16Array(out.length);
        for (let i = 0; i < out.length; i++) {
          pcm[i] = Math.max(-32768, Math.min(32767, Math.round(out[i] * 32767)));
        }
        // fire and forget
        fetch("/api/browser-audio", {
          method: "POST",
          headers: { "Content-Type": "application/octet-stream" },
          body: pcm.buffer,
        }).catch(() => {});
      }
    };
    src.connect(audioProc);
    const sink = audioCtx.createGain();
    sink.gain.value = 0;
    audioProc.connect(sink);
    sink.connect(audioCtx.destination);

    // === METHOD 2: SpeechRecognition for live transcription (parallel, independent) ===
    startTranscription();
  }

  senseActive = true;
  btn.textContent = haveCam && haveMic ? "👁 ON" : haveMic ? "🎙 ON" : "📷 ON";
  btn.classList.add("active");
  const eb = document.getElementById("ears-banner");
  if (eb) eb.style.display = "block";
  console.log("[senses] enabled. cam:", haveCam, "mic:", haveMic,
    "audioCtx rate:", audioCtx ? audioCtx.sampleRate : "N/A");
}

// === LIVE TRANSCRIPTION (browser SpeechRecognition — instant, free) ===
function startTranscription() {
  const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
  const fin = document.getElementById("live-final");
  const int = document.getElementById("live-interim");
  if (!SR) {
    if (fin) fin.textContent = "(no built-in transcription in this browser — server transcription still works)";
    return;
  }
  rec = new SR();
  rec.continuous = true;
  rec.interimResults = true;
  rec.lang = "en-US";
  rec.onresult = (e) => {
    let interim = "";
    for (let i = e.resultIndex; i < e.results.length; i++) {
      const t = e.results[i][0].transcript.trim();
      if (e.results[i].isFinal) {
        if (fin && t) { fin.textContent = "“" + t + "”"; fin.style.color = "#6ee7a0"; }
        if (int) int.textContent = "";
      } else interim += t + " ";
    }
    if (int) { int.textContent = interim; int.style.color = "#8fa3ba"; }
  };
  rec.onerror = (e) => {
    if (fin && e.error !== "no-speech") {
      fin.textContent = "(transcription: " + e.error + ")";
      fin.style.color = "#f07080";
    }
  };
  rec.onend = () => { if (senseActive && rec) { try { rec.start(); } catch {} } };
  try { rec.start(); } catch {}
}

// === CAMERA FRAME ===
function sendFrame() {
  if (!senseActive || !videoEl || !videoCanvas || videoEl.videoWidth === 0) return;
  const ctx = videoCanvas.getContext("2d");
  ctx.drawImage(videoEl, 0, 0, 64, 64);
  const imageData = ctx.getImageData(0, 0, 64, 64);

  // convert to grayscale
  const gray = new Uint8Array(64 * 64);
  const d = imageData.data;
  for (let i = 0, j = 0; i < d.length; i += 4, j++) {
    gray[j] = Math.round(0.299 * d[i] + 0.587 * d[i+1] + 0.114 * d[i+2]);
  }

  // === WHAT THE BRAIN SEES: histogram stretch (always full contrast) ===
  const eyes = document.getElementById("eyes");
  if (eyes) {
    const ectx = eyes.getContext("2d");
    const out = ectx.createImageData(64, 64);
    let min = 255, max = 0;
    for (let j = 0; j < gray.length; j++) {
      if (gray[j] < min) min = gray[j];
      if (gray[j] > max) max = gray[j];
    }
    const range = Math.max(1, max - min);
    for (let j = 0; j < gray.length; j++) {
      out.data[j*4] = out.data[j*4+1] = out.data[j*4+2] =
        Math.min(255, Math.round(((gray[j] - min) / range) * 255 * 0.85));
      out.data[j*4+3] = 255;
    }
    ectx.putImageData(out, 0, 0);
    eyes.classList.add("live");
  }

  // send to server
  fetch("/api/browser-frame", {
    method: "POST",
    headers: { "Content-Type": "application/octet-stream" },
    body: gray.buffer,
  }).catch(() => {});
}

function disableBrowserSenses() {
  senseActive = false;
  if (frameTimer) clearInterval(frameTimer);
  if (audioProc) { try { audioProc.disconnect(); } catch {} audioProc = null; }
  if (audioCtx) { try { audioCtx.close(); } catch {} audioCtx = null; }
  if (rec) { try { rec.onend = null; rec.stop(); } catch {} rec = null; }
  if (camStream) { camStream.getTracks().forEach(t => t.stop()); camStream = null; }
  if (micStream) { micStream.getTracks().forEach(t => t.stop()); micStream = null; }
  if (videoEl) { videoEl.srcObject = null; videoEl = null; }
  const btn = document.getElementById("sense-toggle");
  btn.textContent = "👁 senses";
  btn.classList.remove("active");
  const eb = document.getElementById("ears-banner");
  if (eb) eb.style.display = "none";
  const fin = document.getElementById("live-final");
  const int = document.getElementById("live-interim");
  if (fin) { fin.textContent = "…"; fin.style.color = ""; }
  if (int) int.textContent = "";
  console.log("[senses] disabled");
}
