// SCROLLING EEG — the brain's raw neural signal in real-time.
// Like a hospital EEG machine. Not a spectrogram (frequency) but the actual
// voltage trace scrolling across the screen. This is what people picture
// when they hear "brain activity" — now it's live and real.

"use strict";

let eegCtx = null;
let eegCanvas = null;

function initEEG() {
  eegCanvas = document.getElementById("eeg");
  if (!eegCanvas) return;
  eegCtx = eegCanvas.getContext("2d");
}

function drawEEG(s) {
  if (!eegCanvas || !eegCtx) return;
  const rect = eegCanvas.getBoundingClientRect();
  if (rect.width === 0) return;
  const DPR = window.devicePixelRatio || 1;
  const w = Math.round(rect.width * DPR);
  const h = Math.round(rect.height * DPR);
  if (eegCanvas.width !== w || eegCanvas.height !== h) {
    eegCanvas.width = w; eegCanvas.height = h;
  }
  eegCtx.setTransform(DPR, 0, 0, DPR, 0, 0);
  const W = rect.width, H = rect.height;

  // background: dark with subtle grid
  eegCtx.fillStyle = "#06080c";
  eegCtx.fillRect(0, 0, W, H);

  // grid lines (like medical EEG paper)
  eegCtx.strokeStyle = "#111820";
  eegCtx.lineWidth = 1;
  for (let x = 0; x < W; x += 40) {
    eegCtx.beginPath(); eegCtx.moveTo(x, 0); eegCtx.lineTo(x, H); eegCtx.stroke();
  }
  for (let y = 0; y < H; y += 30) {
    eegCtx.beginPath(); eegCtx.moveTo(0, y); eegCtx.lineTo(W, y); eegCtx.stroke();
  }

  const signal = s.LfpSignal;
  if (!signal || signal.length < 10) return;

  // find min/max for normalization
  let min = Infinity, max = -Infinity;
  for (const v of signal) {
    if (v < min) min = v;
    if (v > max) max = v;
  }
  const range = Math.max(max - min, 1e-9);
  const midY = H / 2;
  const amplitude = H * 0.4;

  // draw multiple traces (simulate multi-electrode view)
  const traces = 4;
  const traceH = H / traces;

  for (let t = 0; t < traces; t++) {
    const offset = t * traceH + traceH / 2;
    const scale = amplitude / traces * 0.8;

    // signal color varies by trace
    const colors = ["#3ddbb8", "#5ba8e0", "#a78bfa", "#f0b45c"];
    eegCtx.strokeStyle = colors[t];
    eegCtx.lineWidth = 1.5;
    eegCtx.beginPath();

    const step = Math.max(1, Math.floor(signal.length / W));
    for (let i = 0; i < signal.length; i += step) {
      const x = (i / signal.length) * W;
      const v = signal[i];
      const y = offset - ((v - min) / range - 0.5) * 2 * scale;
      if (i === 0) eegCtx.moveTo(x, y);
      else eegCtx.lineTo(x, y);
    }
    eegCtx.stroke();
  }

  // right edge glow (the "now" moment)
  const glow = eegCtx.createLinearGradient(W - 60, 0, W, 0);
  glow.addColorStop(0, "rgba(61, 219, 184, 0)");
  glow.addColorStop(1, "rgba(61, 219, 184, 0.1)");
  eegCtx.fillStyle = glow;
  eegCtx.fillRect(W - 60, 0, 60, H);

  // state label
  eegCtx.font = "600 10px 'JetBrains Mono', monospace";
  eegCtx.fillStyle = "rgba(139, 150, 168, 0.6)";
  const stateStr = s.SleepState === "NREM" ? "deep sleep" : s.SleepState === "REM" ? "dreaming (REM)" : s.Feeling || "";
  eegCtx.fillText(`${s.MeanRateHz.toFixed(1)} Hz · ${stateStr}`, 8, 14);
}
