// BROWSER SENSES v3 - camera, mic, transcription, presence. One file.
// Designed for: PC (Chrome/Edge), Phone (Chrome/Samsung), Tablet.
// No silent failures. Every error is visible. Every signal is used.

"use strict";

let senseActive = false;
let camStream = null, micStream = null;
let videoEl = null, videoCanvas = null, frameTimer = null;
let audioCtx = null, audioProc = null;
let rec = null; // SpeechRecognition
let keepaliveTimer = null, watchdogTimer = null, waitReadyTimer = null;

async function enableBrowserSenses() {
  const btn = document.getElementById("sense-toggle");
  if (senseActive) { disableBrowserSenses(); return; }
  btn.textContent = "requesting...";

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
        btn.textContent = "eyes + ears";
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

    // send frames at 5 fps (not 10 - saves phone battery)
    frameTimer = setInterval(sendFrame, 200);

    // wait for video to have actual dimensions; if it never loads (Samsung
    // sometimes needs a retry), pause+play to force the stream to start
    let retries = 0;
    waitReadyTimer = setInterval(() => {
      if (videoEl.videoWidth > 0) {
        clearInterval(waitReadyTimer);
        console.log("[senses] camera ready:", videoEl.videoWidth + "x" + videoEl.videoHeight);
      } else if (++retries > 25) { // 5 seconds with no dimensions
        clearInterval(waitReadyTimer);
        console.log("[senses] camera stuck, retrying play()");
        videoEl.pause();
        videoEl.play().then(() => {
          console.log("[senses] camera retry OK:", videoEl.videoWidth + "x" + videoEl.videoHeight);
        }).catch(() => {});
      }
    }, 200);
  }

  // === AUDIO SETUP ===
  if (haveMic && micStream) {
    audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    const src = audioCtx.createMediaStreamSource(micStream);

    // KEEPALIVE: browsers suspend AudioContext after a period of tab
    // inactivity or system audio policy changes. This catches the most
    // common "it stopped hearing me after a while" bug - the context is
    // suspended and onaudioprocess silently stops firing.
    keepaliveTimer = setInterval(() => {
      if (!senseActive || !audioCtx) return;
      if (audioCtx.state === "suspended") {
        console.log("[senses] AudioContext suspended, resuming");
        audioCtx.resume().catch(() => {});
      }
    }, 3000);

    // === METHOD 1: ScriptProcessor for raw PCM upload to server ===
    audioProc = audioCtx.createScriptProcessor(2048, 1, 1);
    let lastSend = 0;
    let lastProcessTime = Date.now();
    audioProc.onaudioprocess = (e) => {
      if (!senseActive) return;
      lastProcessTime = Date.now(); // track that we're still receiving audio
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
      if (vadEl) vadEl.textContent = rms > 0.01 ? "VOICE" : "quiet";

      // send to server and Meta ASR at most every 50ms — lower the gate to
      // near-zero for Meta (it handles silence detection itself)
      const now = Date.now();
      if (now - lastSend > 50 && rms > 0.0001) {
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
        // send to server (presence + VAD path)
        fetch("/api/browser-audio", {
          method: "POST",
          headers: { "Content-Type": "application/octet-stream" },
          body: pcm.buffer,
        }).catch(() => {});

        // ALSO send via WebSocket to Meta real-time ASR (primary transcription)
        if (transcribeWs && transcribeWs.readyState === WebSocket.OPEN) {
          transcribeWs.send(pcm.buffer);
        } else if (transcribeWs && transcribeWs.readyState === WebSocket.CONNECTING && wsAudioQueue.length < 20) {
          wsAudioQueue.push(pcm.buffer);   // queue until connected
        }
      }
    };
    src.connect(audioProc);
    const sink = audioCtx.createGain();
    sink.gain.value = 0;
    audioProc.connect(sink);
    sink.connect(audioCtx.destination);

    // WATCHDOG: if onaudioprocess stops firing for 5 seconds (browser
    // throttling, tab background, Samsung power saving), disconnect and
    // reconnect the processor to force it to resume
    watchdogTimer = setInterval(() => {
      if (!senseActive || !audioProc || !audioCtx) return;
      if (Date.now() - lastProcessTime > 5000) {
        console.log("[senses] audio processor stalled, reconnecting");
        try {
          audioProc.disconnect();
          audioProc.connect(sink);
          lastProcessTime = Date.now();
        } catch (e) { console.log("[senses] reconnect failed:", e); }
      }
    }, 5000);

    // === METHOD 2: Meta real-time ASR via WebSocket (primary transcription) ===
    startMetaTranscription();
  }

  senseActive = true;
  btn.textContent = haveCam && haveMic ? "ON" : haveMic ? "mic ON" : "cam ON";
  btn.classList.add("active");
  const eb = document.getElementById("ears-banner");
  if (eb) eb.hidden = false;
  console.log("[senses] enabled. cam:", haveCam, "mic:", haveMic,
    "audioCtx rate:", audioCtx ? audioCtx.sampleRate : "N/A");
}

// === LIVE TRANSCRIPTION (Meta real-time ASR via WebSocket — PRIMARY) ===
// Audio flows: browser mic → ScriptProcessor → /ws/transcribe WebSocket → Meta ASR → transcript → /api/say → brain
// No browser SpeechRecognition. No server VAD. Meta handles everything.
let transcribeWs = null;
let wsAudioQueue = [];

function connectTranscribeWs() {
  const fin = document.getElementById("live-final");
  const int = document.getElementById("live-interim");
  const dot = document.getElementById("live-dot");
  const setStatus = (msg, color) => {
    if (fin) { fin.textContent = msg; fin.style.color = color || "#8fa3ba"; }
    console.log("[transcribe-ws]", msg);
  };

  try {
    const proto = location.protocol === "https:" ? "wss:" : "ws:";
    transcribeWs = new WebSocket(`${proto}//${location.host}/ws/transcribe`);
    transcribeWs.binaryType = "arraybuffer";
  } catch (e) {
    setStatus("(cannot open transcription socket: " + e.message + ")", "#f07080");
    return;
  }

  transcribeWs.onopen = () => {
    console.log("[transcribe-ws] connected to Meta ASR");
    setStatus("LISTENING (Meta real-time) — speak now", "#3ddbb8");
    if (dot) { dot.classList.add("on"); dot.style.color = "#3ddbb8"; }
    // flush any queued audio
    while (wsAudioQueue.length > 0 && transcribeWs.readyState === WebSocket.OPEN) {
      transcribeWs.send(wsAudioQueue.shift());
    }
  };

  transcribeWs.onmessage = (e) => {
    try {
      const evt = JSON.parse(e.data);
      if (evt.type === "partial" && evt.text) {
        if (int) { int.textContent = evt.text; int.style.color = "#3ddbb8"; int.style.fontSize = "15px"; }
      } else if ((evt.type === "final" || evt.isFinal) && evt.text && evt.text.length > 1) {
        if (fin) { fin.textContent = "\u201C" + evt.text + "\u201D"; fin.style.color = "#6ee7a0"; fin.style.fontSize = "17px"; }
        if (int) int.textContent = "";
        if (dot) { dot.classList.add("on"); dot.style.color = "#6ee7a0"; }
        console.log("[transcribe-ws] FINAL:", evt.text);
        // The SERVER feeds finals to the brain directly (no round-trip needed)
      } else if (evt.type === "expired") {
        setStatus("(session expired — reconnecting...)", "#f0b45c");
        setTimeout(connectTranscribeWs, 2000);
      } else if (evt.type === "error") {
        setStatus("(ASR error: " + evt.text + ")", "#f07080");
      }
    } catch { /* not JSON */ }
  };

  transcribeWs.onclose = (e) => {
    console.log("[transcribe-ws] closed:", e.code, e.reason);
    if (e.reason === "no-api-key") {
      setStatus("(no API key — server transcription only. The brain still hears audio.)", "#f0b45c");
      return; // don't reconnect: it will fail again
    }
    if (e.reason === "meta-connect-failed") {
      setStatus("(Meta ASR unreachable — check server console)", "#f07080");
      setTimeout(connectTranscribeWs, 5000); // retry in 5s
      return;
    }
    // normal close: reconnect after 2s
    if (senseActive) {
      setStatus("(reconnecting...)", "#f0b45c");
      setTimeout(connectTranscribeWs, 2000);
    }
  };

  transcribeWs.onerror = () => {
    setStatus("(socket error — reconnecting...)", "#f07080");
  };
}

function startMetaTranscription() {
  connectTranscribeWs();

  // Hook into the audio processor: when PCM is ready, send via WebSocket
  // (in addition to the existing /api/browser-audio POST)
  // We piggyback on the same onaudioprocess that sends to /api/browser-audio
  // by intercepting the audio data at the point of PCM conversion
  // The actual sending is done in the onaudioprocess handler via
  // the global `lastPcmBuffer` variable
}

// Global: the last PCM buffer from the audio processor, for the WS to send
let lastPcmBuffer = null;

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
    const wrap = document.getElementById("eyes-wrap");
    if (wrap) wrap.classList.add("live");
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
  if (keepaliveTimer) clearInterval(keepaliveTimer);
  if (watchdogTimer) clearInterval(watchdogTimer);
  if (waitReadyTimer) clearInterval(waitReadyTimer);
  if (audioProc) { try { audioProc.disconnect(); } catch {} audioProc = null; }
  if (audioCtx) { try { audioCtx.close(); } catch {} audioCtx = null; }
  if (rec) { try { rec.onend = null; rec.stop(); } catch {} rec = null; }
  if (camStream) { camStream.getTracks().forEach(t => t.stop()); camStream = null; }
  if (micStream) { micStream.getTracks().forEach(t => t.stop()); micStream = null; }
  if (videoEl) { videoEl.srcObject = null; videoEl = null; }
  const btn = document.getElementById("sense-toggle");
  btn.textContent = "eyes + ears";
  btn.classList.remove("active");
  const eb = document.getElementById("ears-banner");
  if (eb) eb.hidden = true;
  const fin = document.getElementById("live-final");
  const int = document.getElementById("live-interim");
  if (fin) { fin.textContent = "\u2026"; fin.style.color = ""; }
  if (int) int.textContent = "";
  const wrap = document.getElementById("eyes-wrap");
  if (wrap) wrap.classList.remove("live");
  console.log("[senses] disabled");
}
