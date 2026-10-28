// BROWSER SENSES: use this device's camera and microphone as the brain's eyes and ears.
// Works on ANY device with a browser (PC, phone, tablet) — the brain processes
// whatever this device sees and hears, regardless of where the brain is running.

"use strict";

let senseStream = null;
let audioContext = null;
let audioProcessor = null;
let videoElement = null;
let videoCanvas = null;
let senseActive = false;
let frameTimer = null;

async function enableBrowserSenses() {
  const btn = document.getElementById("sense-toggle");
  if (senseActive) {
    disableBrowserSenses();
    return;
  }

  try {
    btn.textContent = "⏳ requesting…";
    senseStream = await navigator.mediaDevices.getUserMedia({
      video: { width: 320, height: 320, facingMode: "user" },
      audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }
    });

    // ---- VIDEO: capture 64x64 grayscale frames ----
    videoElement = document.createElement("video");
    videoElement.srcObject = senseStream;
    videoElement.muted = true;
    videoElement.playsInline = true;
    await videoElement.play();

    videoCanvas = document.createElement("canvas");
    videoCanvas.width = 64;
    videoCanvas.height = 64;

    // send frames at ~10fps
    frameTimer = setInterval(sendVideoFrame, 100);

    // ---- AUDIO: capture PCM, resample to 16 kHz, send to server ----
    // The sampleRate option is only a REQUEST: most phones run the context at
    // hardware rate (44.1/48 kHz). If we sent that raw, the server would hear
    // chipmunk-speed audio and nothing would transcribe. So we ALWAYS read
    // audioContext.sampleRate and linearly resample to 16000 here.
    audioContext = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 16000 });
    const source = audioContext.createMediaStreamSource(senseStream);

    // Use ScriptProcessorNode for PCM access (deprecated but widely supported)
    audioProcessor = audioContext.createScriptProcessor(2048, 1, 1);
    audioProcessor.onaudioprocess = (e) => {
      if (!senseActive) return;
      const input = e.inputBuffer.getChannelData(0);

      // resample input -> 16 kHz with linear interpolation
      const nativeRate = audioContext.sampleRate; // actual rate, not the request
      let out = input;
      if (Math.abs(nativeRate - 16000) > 1) {
        const ratio = nativeRate / 16000;
        const outLen = Math.max(1, Math.floor(input.length / ratio));
        const resampled = new Float32Array(outLen);
        for (let i = 0; i < outLen; i++) {
          const pos = i * ratio;
          const i0 = Math.floor(pos);
          const frac = pos - i0;
          const a = input[i0] ?? 0;
          const b = input[i0 + 1] ?? a;
          resampled[i] = a + (b - a) * frac;
        }
        out = resampled;
      }

      // energy gate: skip sending digital silence (saves phone battery and
      // server work); the server's VAD handles the rest
      let energy = 0;
      for (let i = 0; i < out.length; i++) energy += out[i] * out[i];
      const rms = Math.sqrt(energy / out.length);
      if (rms < 0.0025) return;

      // Convert float32 [-1,1] to int16
      const pcm = new Int16Array(out.length);
      for (let i = 0; i < out.length; i++) {
        pcm[i] = Math.max(-32768, Math.min(32767, Math.round(out[i] * 32767)));
      }
      // Send to server as binary
      fetch("/api/browser-audio", {
        method: "POST",
        headers: { "Content-Type": "application/octet-stream" },
        body: pcm.buffer,
      }).catch(() => {});
    };
    source.connect(audioProcessor);
    // do NOT route the mic to the destination (that is speaker feedback):
    // the processor still fires when connected through a zero-gain node
    const sink = audioContext.createGain();
    sink.gain.value = 0;
    audioProcessor.connect(sink);
    sink.connect(audioContext.destination);

    senseActive = true;
    btn.textContent = "👁 senses ON";
    btn.classList.add("active");
    console.log("Browser senses enabled");
  } catch (err) {
    console.error("getUserMedia failed:", err);
    btn.textContent = "👁 senses";
    if (err.name === "NotAllowedError") {
      alert("Camera/mic permission denied. Please allow access in your browser settings.");
    } else if (err.name === "NotFoundError") {
      alert("No camera or microphone found on this device.");
    } else {
      alert("Could not access camera/mic: " + err.message);
    }
  }
}

function disableBrowserSenses() {
  senseActive = false;
  if (frameTimer) clearInterval(frameTimer);
  if (audioProcessor) { audioProcessor.disconnect(); audioProcessor = null; }
  if (audioContext) { audioContext.close(); audioContext = null; }
  if (senseStream) { senseStream.getTracks().forEach(t => t.stop()); senseStream = null; }
  if (videoElement) { videoElement.srcObject = null; videoElement = null; }
  const btn = document.getElementById("sense-toggle");
  btn.textContent = "👁 senses";
  btn.classList.remove("active");
  console.log("Browser senses disabled");
}

function sendVideoFrame() {
  if (!senseActive || !videoElement || !videoCanvas) return;
  const ctx = videoCanvas.getContext("2d");
  ctx.drawImage(videoElement, 0, 0, 64, 64);
  const imageData = ctx.getImageData(0, 0, 64, 64);

  // convert RGBA to grayscale (luminance)
  const gray = new Uint8Array(64 * 64);
  const d = imageData.data;
  for (let i = 0, j = 0; i < d.length; i += 4, j++) {
    gray[j] = Math.round(0.299 * d[i] + 0.587 * d[i+1] + 0.114 * d[i+2]);
  }

  // Send to server
  fetch("/api/browser-frame", {
    method: "POST",
    headers: { "Content-Type": "application/octet-stream" },
    body: gray.buffer,
  }).catch(() => {});
}
