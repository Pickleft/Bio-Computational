// NOBEL-LEVEL VISUALIZATIONS — theoretical neuroscience brought to life.
// These have never been seen in real-time for a working brain.

"use strict";

function drawPhiGauge(s) {
  const c = document.getElementById("phi-gauge");
  if (!c) return;
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d1017";
  ctx.fillRect(0, 0, w, h);

  const phi = s.Phi || 0;
  const cx = w / 2, cy = h * 0.42, r = Math.min(w, h) * 0.28;

  // full circle gauge
  const fullCircle = Math.PI * 2;
  ctx.strokeStyle = "#1c222e";
  ctx.lineWidth = 14;
  ctx.lineCap = "round";
  ctx.beginPath();
  ctx.arc(cx, cy, r, 0, fullCircle);
  ctx.stroke();

  // Phi arc — the "consciousness dial"
  const phiColor = phi > 0.5 ? "#3ddbb8" : phi > 0.2 ? "#f0b45c" : "#f07080";
  ctx.strokeStyle = phiColor;
  ctx.lineWidth = 14;
  ctx.beginPath();
  ctx.arc(cx, cy, r, -Math.PI / 2, -Math.PI / 2 + fullCircle * phi);
  ctx.stroke();

  // glow effect
  if (phi > 0.3) {
    ctx.shadowColor = phiColor;
    ctx.shadowBlur = 20;
    ctx.strokeStyle = phiColor + "40";
    ctx.lineWidth = 18;
    ctx.beginPath();
    ctx.arc(cx, cy, r, -Math.PI / 2, -Math.PI / 2 + fullCircle * phi);
    ctx.stroke();
    ctx.shadowBlur = 0;
  }

  // Φ value
  ctx.fillStyle = phiColor;
  ctx.font = "700 28px 'Inter', sans-serif";
  ctx.textAlign = "center";
  ctx.fillText(`Φ ${phi.toFixed(2)}`, cx, cy + 8);
  ctx.fillStyle = "#5a6474";
  ctx.font = "500 10px 'JetBrains Mono', monospace";
  ctx.fillText("integrated information", cx, cy + r * 0.5);
  ctx.fillText(s.PhiStateLabel || "", cx, cy + r * 0.5 + 14);

  // Φ history sparkline
  const hist = s.PhiHistory || [];
  if (hist.length > 2) {
    ctx.strokeStyle = phiColor + "66";
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    for (let i = 0; i < hist.length; i++) {
      const x = (i / hist.length) * w;
      const y = h - 5 - hist[i] * 20;
      if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
    }
    ctx.stroke();
  }
  ctx.textAlign = "left";
}

function drawFreeEnergy(s) {
  const c = document.getElementById("free-energy");
  if (!c) return;
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d1017";
  ctx.fillRect(0, 0, w, h);

  const hist = s.FreeEnergyHistory || [];
  if (hist.length < 2) return;

  // find max for scaling
  let max = 0.1;
  for (const d of hist) max = Math.max(max, d[1]);

  const midY = h / 2;

  // free energy curve (the main event)
  ctx.beginPath();
  for (let i = 0; i < hist.length; i++) {
    const x = (i / hist.length) * w;
    const y = midY - (hist[i][1] / max) * midY * 0.8;
    if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
  }
  ctx.strokeStyle = "#a78bfa";
  ctx.lineWidth = 2;
  ctx.stroke();

  // fill under curve
  ctx.lineTo(w, midY);
  ctx.lineTo(0, midY);
  ctx.closePath();
  const grad = ctx.createLinearGradient(0, 0, 0, h);
  grad.addColorStop(0, "rgba(167, 139, 250, 0.2)");
  grad.addColorStop(1, "rgba(167, 139, 250, 0)");
  ctx.fillStyle = grad;
  ctx.fill();

  // prediction error component (red overlay)
  ctx.beginPath();
  for (let i = 0; i < hist.length; i++) {
    const x = (i / hist.length) * w;
    const y = midY - (hist[i][2] / max) * midY * 0.8;
    if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
  }
  ctx.strokeStyle = "#f0708066";
  ctx.lineWidth = 1;
  ctx.stroke();

  // labels
  ctx.font = "600 10px 'JetBrains Mono', monospace";
  ctx.fillStyle = "#a78bfa";
  ctx.fillText(`F = ${s.FreeEnergyValue.toFixed(2)}`, 8, 14);
  ctx.fillStyle = "#f0708088";
  ctx.fillText(`error = ${s.FreeEnergyError.toFixed(2)}`, 8, 28);
  ctx.fillStyle = "#5a6474";
  ctx.fillText("free energy (Friston 2010)", 8, h - 6);
}

function drawManifold(s) {
  const c = document.getElementById("manifold");
  if (!c) return;
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d1017";
  ctx.fillRect(0, 0, w, h);

  const pts = s.ManifoldProjection || [];
  if (pts.length < 2) return;

  // find bounds
  let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
  for (const p of pts) {
    if (p[0] < minX) minX = p[0]; if (p[0] > maxX) maxX = p[0];
    if (p[1] < minY) minY = p[1]; if (p[1] > maxY) maxY = p[1];
  }
  const rangeX = Math.max(maxX - minX, 1e-6);
  const rangeY = Math.max(maxY - minY, 1e-6);
  const scale = Math.min(w / rangeX, h / rangeY) * 0.8;
  const cx = w / 2, cy = h / 2;

  // draw trajectory as a glowing path
  for (let i = 1; i < pts.length; i++) {
    const x1 = cx + (pts[i-1][0] - (minX + maxX) / 2) * scale;
    const y1 = cy + (pts[i-1][1] - (minY + maxY) / 2) * scale;
    const x2 = cx + (pts[i][0] - (minX + maxX) / 2) * scale;
    const y2 = cy + (pts[i][1] - (minY + maxY) / 2) * scale;

    const alpha = (i / pts.length) * 0.8; // more recent = brighter
    ctx.strokeStyle = `rgba(92, 168, 224, ${alpha})`;
    ctx.lineWidth = 1 + (i / pts.length) * 2;
    ctx.beginPath();
    ctx.moveTo(x1, y1);
    ctx.lineTo(x2, y2);
    ctx.stroke();
  }

  // current position (bright dot)
  const lastX = cx + (pts[pts.length-1][0] - (minX + maxX) / 2) * scale;
  const lastY = cy + (pts[pts.length-1][1] - (minY + maxY) / 2) * scale;
  ctx.fillStyle = "#5ba8e0";
  ctx.shadowColor = "#5ba8e0";
  ctx.shadowBlur = 10;
  ctx.beginPath();
  ctx.arc(lastX, lastY, 4, 0, Math.PI * 2);
  ctx.fill();
  ctx.shadowBlur = 0;

  // labels
  ctx.font = "600 10px 'JetBrains Mono', monospace";
  ctx.fillStyle = "#5ba8e0";
  ctx.fillText(`dim = ${s.ManifoldDimensionality.toFixed(1)}`, 8, 14);
  ctx.fillStyle = "#5a6474";
  ctx.fillText("neural manifold (Stringer 2019)", 8, h - 6);
}
