// BROWSER SENSES v3 - camera, mic, transcription, presence. One file.
// Designed for: PC (Chrome/Edge), Phone (Chrome/Samsung), Tablet.
// No silent failures. Every error is visible. Every signal is used.

"use strict";

let senseActive = false;
let _lastRms = 0;
// CLEAN AUDIO FOR THE ASR: a slow DC-bias tracker (some mics sit off zero and
// waste headroom) and a gentle AGC that lifts quiet speech to a healthy
// level — streaming ASR mishears quiet audio far more than loud audio.
let _dcEma = 0;
let _agcGain = 1;
// PUSH-TO-TALK: audio is SENT only while held (button or SPACE). The level
// bar always shows the live mic; only sending is gated. talkDown revives the
// session on demand — dead idle sessions cost nothing.
let _talkReleasedAt = 0;
window.__talkHeld = false;
function talkDown() {
  if (window.__talkHeld || !senseActive) return;
  window.__talkHeld = true;
  const b = document.getElementById("holdtalk");
  if (b) { b.style.background = "rgba(61,219,184,.35)"; b.textContent = "LISTENING — SPEAK"; }
  // session may have died while idle: revive it now so the first word lands
  if (!transcribeWs || transcribeWs.readyState !== WebSocket.OPEN) {
    try { connectTranscribeWs(); } catch (e) {}
  }
}
function talkUp() {
  if (!window.__talkHeld) return;
  window.__talkHeld = false;
  _talkReleasedAt = Date.now();
  const b = document.getElementById("holdtalk");
  if (b) { b.style.background = "rgba(61,219,184,.08)"; b.textContent = "HOLD TO TALK \u00a0(or hold SPACE)"; }
}
window.addEventListener("DOMContentLoaded", () => {
  const b = document.getElementById("holdtalk");
  if (!b) return;
  b.addEventListener("pointerdown", (e) => { e.preventDefault(); talkDown(); });
  b.addEventListener("pointerup", talkUp);
  b.addEventListener("pointerleave", talkUp);
  b.addEventListener("pointercancel", talkUp);
});
window.addEventListener("keydown", (e) => {
  if (e.code !== "Space" || e.repeat) return;
  const t = e.target;
  if (t && (t.tagName === "INPUT" || t.tagName === "TEXTAREA" || t.isContentEditable)) return;
  e.preventDefault();
  talkDown();
});
window.addEventListener("keyup", (e) => { if (e.code === "Space") talkUp(); });
window.addEventListener("blur", talkUp);
let _levelRafStarted = false;
function _renderLevelBar() {
  if (!_levelRafStarted) {
    _levelRafStarted = true;
    const render = () => {
      const bar = document.getElementById("audio-level");
      if (bar) bar.style.width = Math.min(100, _lastRms * 400) + "%";
      const vadEl = document.getElementById("audio-vad");
      if (vadEl) vadEl.textContent = _lastRms > 0.01 ? "VOICE" : "quiet";
      requestAnimationFrame(render);
    };
    requestAnimationFrame(render);
  }
}
let camStream = null, micStream = null;
let videoEl = null, videoCanvas = null, frameTimer = null;
let audioCtx = null, audioProc = null;
let rec = null; // SpeechRecognition
let keepaliveTimer = null, watchdogTimer = null, waitReadyTimer = null, senderTimer = null;

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

    // === METHOD 1: CAPTURE — AudioWorklet (audio thread) with ScriptProcessor fallback ===
    // ScriptProcessor runs its callback on the MAIN thread: dashboard rendering
    // jank or a backgrounded tab throttles audio delivery below real-time and
    // Meta kills the session ("ingress slower than real-time"). AudioWorklet
    // runs on the dedicated audio thread — immune to both.
    let lastProcessTime = Date.now();
    let pendingSmp = 0;          // 24kHz samples buffered, waiting to be sent
    let pcmBuffer = [];          // queued Int16Array frames
    let lastSend = 0;

    // BATCHED audio upload: AudioWorklet fires ~375 frames/sec — a fetch per
    // frame floods the connection pool and kills transcription. Batch voiced
    // PCM and POST every ~100ms.
    let _batchPcm = [];
    let _lastBatchPost = 0;
    let _lastVoicedAt = 0;

    const handleAudioFrame = (input) => {
      lastProcessTime = Date.now();
      // measure level for local display — cached, rendered by rAF (raw level,
      // before any gain: the bar shows what the MIC hears, not what we send)
      let sum = 0, mean = 0;
      for (let i = 0; i < input.length; i++) { sum += input[i] * input[i]; mean += input[i]; }
      mean /= input.length;
      const rms = Math.sqrt(sum / input.length);
      _lastRms = rms;
      _renderLevelBar();

      // DC bias: track slowly (time constant ~seconds — a fast tracker would
      // eat the speech fundamental, which lives at 80-250 Hz)
      _dcEma = _dcEma * 0.9995 + mean * 0.0005;

      // AGC: adapt gain ONLY on voiced frames (silence must not crank the
      // gain to max), target rms ~0.07, never below 1 (we lift, not squash),
      // cap at 6x (lifting noise 20x would just amplify ghosts)
      if (rms > 0.004) {
        const desired = Math.min(6, Math.max(1, 0.07 / rms));
        _agcGain = _agcGain * 0.9 + desired * 0.1;
      }

      // DUAL-RATE RESAMPLE (DC-removed and gain-lifted) — ALWAYS fill the
      // buffer with the true signal. Gating the fill on rms spliced FAKE
      // ZEROS into quiet syllables mid-speech, and streaming ASR reads that
      // as sentence boundaries: chopped fragments, dropped words, and
      // repeated-phrase loops. Real silence is fine; fabricated silence is
      // poison.
      // 24 kHz goes to Meta (Muse's full recognition bandwidth — 16k throws
      // away the top half of speech); 16 kHz goes to the brain's cochlea
      // (it expects that rate).
      const makePcm = (rate) => {
        const ratio = audioCtx.sampleRate / rate;
        const outLen = Math.max(1, Math.floor(input.length / ratio));
        const out = new Int16Array(outLen);
        for (let i = 0; i < outLen; i++) {
          const pos = i * ratio;
          const i0 = Math.floor(pos), frac = pos - i0;
          let v = input[i0] + (input[i0 + 1] - input[i0]) * frac;
          v = (v - _dcEma) * _agcGain;
          if (v > 1) v = 1; else if (v < -1) v = -1;
          out[i] = Math.round(v * 32767);
        }
        return out;
      };
      const pcm = makePcm(24000);
      // PUSH-TO-TALK: audio is SENT only while the button (or spacebar) is
      // held, plus a 1.2s trailing window so end-of-speech can fire. The
      // level bar stays live (you can see the mic hearing you) — only the
      // sending is gated. Idle sessions die and that's fine: talkDown
      // reconnects on demand.
      const sending = window.__talkHeld === true || Date.now() - _talkReleasedAt < 1200;
      if (sending) {
        if (rms > 0.004) {
          // BATCH: collect voiced PCM, POST every 100ms (not per-frame) — this
          // feeds the brain's audition (16 kHz); silence buys nothing there
          _lastVoicedAt = Date.now();
          _batchPcm.push(makePcm(16000));
        } else if (_lastVoicedAt > 0 && Date.now() - _lastVoicedAt < 1200) {
          // TRAILING SILENCE FLUSH: the server's VAD can only END an utterance
          // when silence ARRIVES. Without these zero frames after speech stops,
          // the capture never closes and the batch path never transcribes.
          _batchPcm.push(new Int16Array(160));   // 10ms of true silence
        }
        if (_batchPcm.length > 0 && Date.now() - _lastBatchPost > 100) {
          _lastBatchPost = Date.now();
          const total = _batchPcm.reduce((a, p) => a + p.length, 0);
          const merged = new Int16Array(total);
          let off = 0;
          for (const p of _batchPcm) { merged.set(p, off); off += p.length; }
          _batchPcm = [];
          fetch("/api/browser-audio", {
            method: "POST",
            headers: { "Content-Type": "application/octet-stream" },
            body: merged.buffer,
          }).catch(() => {});
        }
        pcmBuffer.push(pcm);
        pendingSmp += pcm.length;
      } else {
        pcmBuffer = []; pendingSmp = 0;   // not holding: nothing to send
      }
      if (pendingSmp > 24000 * 3) { pcmBuffer.splice(0, pcmBuffer.length - 8); pendingSmp = pcmBuffer.reduce((a, p) => a + p.length, 0); }
    };

    // PACED SENDER: merges small AudioWorklet frames (~2.6ms each) into
    // ~100ms chunks before sending to Meta. Micro-fragments can't be
    // transcribed; 100ms is the sweet spot for streaming ASR (steady ingress,
    // clean endpointing). Skips stale audio when the backlog exceeds 2s
    // (live > complete). While the BRAIN is speaking, sends zeros instead —
    // the mic would otherwise pick the brain's own voice off the speakers and
    // Meta would transcribe it (echo ghosts). Zeros keep the session clock.
    senderTimer = setInterval(() => {
      if (!senseActive) return;
      if (Date.now() - lastProcessTime > 500) _lastRms *= 0.8;
      const now = Date.now();
      if (now - lastSend < 95) return;
      lastSend = now;

      // skip stale audio if backlog is huge (>2s at 24kHz)
      if (pendingSmp > 48000) {
        let drop = 0;
        while (pcmBuffer.length > 1 && drop < pendingSmp - 36000) {
          const f = pcmBuffer.shift();
          drop += f.length;
          pendingSmp -= f.length;
        }
        if (drop > 0) console.log("[senses] backlog trim: skipped " + (drop / 24).toFixed(0) + "ms");
      }

      // merge ALL pending frames into ONE chunk and send it
      if (pcmBuffer.length > 0 && transcribeWs && transcribeWs.readyState === WebSocket.OPEN) {
        const total = pcmBuffer.reduce((a, p) => a + p.length, 0);
        const merged = new Int16Array(total);
        let off = 0;
        for (const p of pcmBuffer) { merged.set(p, off); off += p.length; }
        pcmBuffer = [];
        pendingSmp = 0;
        if (window.brainBrowserSpeaking === true) {
          // ECHO DUCK: the brain is talking — send silence, not its own voice
          transcribeWs.send(new Int16Array(total).buffer);
        } else {
          transcribeWs.send(merged.buffer);
        }
      } else if (pcmBuffer.length > 0) {
        // WS not open — clear the buffer (stale audio is useless)
        pcmBuffer = [];
        pendingSmp = 0;
      }
    }, 50);

    const workletCode = `
      class StreamProcessor extends AudioWorkletProcessor {
        constructor() { super(); this._frames = 0; }
        process(inputs) {
          const ch = inputs[0] && inputs[0][0];
          if (ch) {
            this._frames++;
            if (this._frames === 1) this.port.postMessage({ first: true });
            this.port.postMessage(ch.slice(0));
          }
          return true;
        }
      }
      registerProcessor('stream-processor', StreamProcessor);`;
    let captureReady = false;
    const audioSink = audioCtx.createGain();
    audioSink.gain.value = 0;
    audioSink.connect(audioCtx.destination);

    const onFirstFrame = () => console.log("[senses] ★ FIRST AUDIO FRAME captured — pipeline live");

    if (audioCtx.audioWorklet) {
      const blobUrl = URL.createObjectURL(new Blob([workletCode], { type: "application/javascript" }));
      audioCtx.audioWorklet.addModule(blobUrl).then(() => {
        const node = new AudioWorkletNode(audioCtx, "stream-processor");
        let firstLogged = false;
        node.port.onmessage = (e) => {
          if (e.data && e.data.first) { console.log("[senses] ★ worklet first frame"); onFirstFrame(); return; }
          if (!senseActive) return;
          if (!firstLogged) { firstLogged = true; console.log("[senses] ★ first audio data received"); }
          try { handleAudioFrame(new Float32Array(e.data)); }
          catch (err) { console.error("[senses] handleAudioFrame error:", err); }
        };
        // CRITICAL: connect BOTH input AND output — a floating node may not process
        src.connect(node);
        node.connect(audioSink);
        captureReady = true;
        console.log("[senses] ★ AudioWorklet capture live (audio thread — jank-immune)");
      }).catch((e) => {
        console.error("[senses] AudioWorklet failed, falling back to ScriptProcessor:", e);
        installScriptProcessor();
      });
    }
    if (!audioCtx.audioWorklet) installScriptProcessor();

    function installScriptProcessor() {
      if (captureReady) return;
      console.log("[senses] using ScriptProcessor fallback (main thread)");
      audioProc = audioCtx.createScriptProcessor(2048, 1, 1);
      let firstLogged = false;
      audioProc.onaudioprocess = (e) => {
        if (!senseActive) return;
        if (!firstLogged) { firstLogged = true; console.log("[senses] ★ first ScriptProcessor frame"); }
        try { handleAudioFrame(e.inputBuffer.getChannelData(0)); }
        catch (err) { console.error("[senses] handleAudioFrame error:", err); }
      };
      src.connect(audioProc);
      audioProc.connect(audioSink);
    }

    // WATCHDOG: with AudioWorklet, audio keeps flowing even when the main
    // thread stalls — but the SENDER runs on the main thread; if it stalled
    // long enough for Meta to kill the session, recycle it.
    // NOTE: the ears are NOT leader-gated. Leadership gates the VOICE (one
    // tab speaks); a follower tab with dead ears was the "not being
    // transcribed" bug — each tab streams its OWN mic, and the server
    // dedups duplicate transcripts.
    watchdogTimer = setInterval(() => {
      if (!senseActive || !audioCtx) return;
      // recycle ONLY while holding the talk button: with push-to-talk, idle
      // gaps between holds are normal and sessions dying idle is fine —
      // talkDown revives on demand. A stall WHILE HOLDING is a real stall.
      if (Date.now() - lastProcessTime > 5000 && window.__talkHeld === true) {
        console.log("[senses] audio stalled while holding — recycling ASR session");
        lastProcessTime = Date.now();
        if (transcribeWs) {
          try { transcribeWs.onclose = null; transcribeWs.close(); } catch (e) {}
          transcribeWs = null;
        }
        setTimeout(connectTranscribeWs, 500);
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
// ANY tab with senses ON streams — the ears are never leader-gated (each tab
// has its own mic; the server dedups duplicate transcripts).
let transcribeWs = null;
let wsAudioQueue = [];

function connectTranscribeWs() {
  // RE-ENTRANCY GUARD: a reconnect already in flight (CONNECTING) or a live
  // socket means do nothing — stacked reconnects multiplied sessions and
  // churned "session closed" spam when the server was briefly sick
  if (transcribeWs && transcribeWs.readyState === WebSocket.OPEN) {
    console.log("[transcribe-ws] already open — skipping reconnect");
    return;
  }
  if (transcribeWs && transcribeWs.readyState === WebSocket.CONNECTING) {
    // a connect that hangs forever (server starved mid-handshake) would block
    // every future reconnect: force it closed after 10s and retry
    const hung = transcribeWs;
    setTimeout(() => {
      if (transcribeWs === hung && hung.readyState === WebSocket.CONNECTING) {
        console.log("[transcribe-ws] connect hung >10s — forcing close");
        try { hung.onclose = null; hung.close(); } catch (e) {}
        if (transcribeWs === hung) transcribeWs = null;
        if (senseActive) setTimeout(connectTranscribeWs, 300);
      }
    }, 10000);
    return;
  }
  const fin = document.getElementById("live-final");
  const int = document.getElementById("live-interim");
  const dot = document.getElementById("live-dot");
  const setStatus = (msg, color) => {
    if (fin) { fin.textContent = msg; fin.style.color = color || "#8fa3ba"; }
    console.log("[transcribe-ws]", msg);
  };

  try {
    const proto = location.protocol === "https:" ? "wss:" : "ws:";
    // declare our rate so the server connects to Meta with ZERO sniff delay
    transcribeWs = new WebSocket(`${proto}//${location.host}/ws/transcribe?hz=24000`);
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
      if (evt.type === "info" && evt.text) {
        // server info: real-time may not be available, batch fallback active
        setStatus(evt.text, "#f0b45c");
        if (dot) { dot.style.color = "#f0b45c"; dot.classList.remove("on"); }
        return; // don't close: the WebSocket stays open for future reconnection
      }
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
    // normal close (an idle session ending between holds is EXPECTED with
    // push-to-talk): reconnect quietly, no scary status
    if (senseActive) {
      setStatus("(ears refreshed — hold to talk)", "#3ddbb8");
      setTimeout(connectTranscribeWs, 500);
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
  if (senderTimer) clearInterval(senderTimer);
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
