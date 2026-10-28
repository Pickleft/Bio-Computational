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

    // ---- AUDIO: capture PCM and send to server ----
    audioContext = new AudioContext({ sampleRate: 16000 });
    const source = audioContext.createMediaStreamSource(senseStream);

    // Use ScriptProcessorNode for PCM access (deprecated but widely supported)
    audioProcessor = audioContext.createScriptProcessor(2048, 1, 1);
    audioProcessor.onaudioprocess = (e) => {
      if (!senseActive) return;
      const input = e.inputBuffer.getChannelData(0);
      // Convert float32 [-1,1] to int16
      const pcm = new Int16Array(input.length);
      for (let i = 0; i < input.length; i++) {
        pcm[i] = Math.max(-32768, Math.min(32767, input[i] * 32767));
      }
      // Send to server as binary
      fetch("/api/browser-audio", {
        method: "POST",
        headers: { "Content-Type": "application/octet-stream" },
        body: pcm.buffer,
      }).catch(() => {});
    };
    source.connect(audioProcessor);
    audioProcessor.connect(audioContext.destination); // needed for processing

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
