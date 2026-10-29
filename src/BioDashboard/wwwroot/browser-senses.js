// BROWSER SENSES: use this device's camera and microphone as the brain's eyes and ears.
// Works on ANY device with a browser (PC, phone, tablet) — the brain processes
// whatever this device sees and hears, regardless of where the brain is running.

"use strict";

let senseStream = null;   // combined stream we own
let camStream = null;     // camera-only when mic unavailable
let micStream = null;     // mic-only when camera unavailable
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

  btn.textContent = "⏳ requesting…";
  // Ask for camera and mic SEPARATELY: requesting them together fails
  // entirely when either device is missing (a PC with no camera would lose
  // its working microphone too). Whichever exists gets used.
  let haveCam = false, haveMic = false;
  try {
    senseStream = await navigator.mediaDevices.getUserMedia({
      video: { width: 320, height: 320, facingMode: "user" },
      audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }
    });
    haveCam = haveMic = true;
  } catch (bothFailed) {
    try {
      micStream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }
      });
      haveMic = true;
      console.log("[senses] no camera available - running on microphone only");
    } catch (micFailed) {
      try {
        camStream = await navigator.mediaDevices.getUserMedia({
          video: { width: 320, height: 320, facingMode: "user" }
        });
        haveCam = true;
        console.log("[senses] no microphone available - running on camera only");
      } catch (camFailed) {
        btn.textContent = "👁 senses";
        alert("No camera or microphone available on this device.\n\n" +
              "Camera error: " + camFailed.name + "\nMicrophone error: " + micFailed.name);
        return;
      }
    }
  }
  const stream = senseStream ?? micStream ?? camStream;
  const audioTracks = stream.getAudioTracks();
  const videoTracks = stream.getVideoTracks();

  if (videoTracks.length > 0) {
    // ---- VIDEO: capture 64x64 grayscale frames ----
    videoElement = document.createElement("video");
    videoElement.srcObject = new MediaStream(videoTracks);
    videoElement.muted = true;
    videoElement.playsInline = true;
    await videoElement.play();

    videoCanvas = document.createElement("canvas");
    videoCanvas.width = 64;
    videoCanvas.height = 64;

    // send frames at ~10fps
    frameTimer = setInterval(sendVideoFrame, 100);
  }

  if (audioTracks.length > 0) {
    // ---- AUDIO: capture PCM, resample to 16 kHz, send to server ----
    // The sampleRate option is only a REQUEST: most phones run the context at
    // hardware rate (44.1/48 kHz). If we sent that raw, the server would hear
    // chipmunk-speed audio and nothing would transcribe. So we ALWAYS read
    // audioContext.sampleRate and linearly resample to 16000 here.
    audioContext = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 16000 });
    const source = audioContext.createMediaStreamSource(new MediaStream(audioTracks));

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
      if (rms < 0.006) return; // anti-hallucination: near-silence invents words

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
  }

  senseActive = true;
  btn.textContent = haveCam && haveMic ? "👁 senses ON" : haveMic ? "🎙 mic ON" : "📷 cam ON";
  btn.classList.add("active");
  console.log("Browser senses enabled (cam:", haveCam, "mic:", haveMic, ")");
  startLiveTranscription();
}

// ---- LIVE TRANSCRIPTION: streaming words as you speak via the browser's own
// speech recognition (Chrome/Edge). Instant, free, visible proof of hearing.
let liveRec = null;
function startLiveTranscription() {
  const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (!SR) return;
  liveRec = new SR();
  liveRec.continuous = true;
  liveRec.interimResults = true;
  liveRec.lang = "en-US";
  liveRec.onresult = (e) => {
    const dot = document.getElementById("live-dot");
    if (dot) dot.classList.add("on");
    let interim = "";
    for (let i = e.resultIndex; i < e.results.length; i++) {
      const t = e.results[i][0].transcript.trim();
      if (e.results[i].isFinal) {
        const fin = document.getElementById("live-final");
        if (fin && t) fin.textContent = "“" + t + "”";
        document.getElementById("live-interim").textContent = "";
      } else interim += t + " ";
    }
    const int = document.getElementById("live-interim");
    if (int) int.textContent = interim;
  };
  liveRec.onend = () => { if (senseActive && liveRec) { try { liveRec.start(); } catch { } } };
  try { liveRec.start(); } catch { }
}

function disableBrowserSenses() {
  senseActive = false;
  if (liveRec) { try { liveRec.onend = null; liveRec.stop(); } catch { } liveRec = null; }
  const dotEl = document.getElementById("live-dot");
  if (dotEl) dotEl.classList.remove("on");
  const finEl = document.getElementById("live-final");
  const intEl = document.getElementById("live-interim");
  if (finEl) finEl.textContent = "…";
  if (intEl) intEl.textContent = "";
  if (frameTimer) clearInterval(frameTimer);
  if (audioProcessor) { audioProcessor.disconnect(); audioProcessor = null; }
  if (audioContext) { audioContext.close(); audioContext = null; }
  if (senseStream) { senseStream.getTracks().forEach(t => t.stop()); senseStream = null; }
  if (micStream) { micStream.getTracks().forEach(t => t.stop()); micStream = null; }
  if (camStream) { camStream.getTracks().forEach(t => t.stop()); camStream = null; }
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

  // WHAT THE BRAIN SEES: paint the exact 64x64 grayscale frame being sent,
  // so you can look at the world through its eyes
  const eyes = document.getElementById("eyes");
  if (eyes) {
    const ectx = eyes.getContext("2d");
    const out = ectx.createImageData(64, 64);
    for (let j = 0; j < gray.length; j++) {
      out.data[j * 4] = out.data[j * 4 + 1] = out.data[j * 4 + 2] = gray[j];
      out.data[j * 4 + 3] = 255;
    }
    ectx.putImageData(out, 0, 0);
    eyes.classList.add("live");
  }

  // Send to server
  fetch("/api/browser-frame", {
    method: "POST",
    headers: { "Content-Type": "application/octet-stream" },
    body: gray.buffer,
  }).catch(() => {});
}
