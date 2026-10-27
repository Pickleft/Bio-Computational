// Bio-Computational dashboard: vanilla JS, one WebSocket at 10 Hz.
"use strict";

if (new URLSearchParams(location.search).has("demo")) document.body.classList.add("demo");

const $ = (id) => document.getElementById(id);
const regionColors = ["#46e0c0", "#5aa9e0", "#9d7ce0", "#e0a546", "#e05a6a", "#6ae08d",
  "#e07db1", "#8ecfe0", "#cfe08d", "#a0a0ff", "#e0d05a", "#7d8fa8"];

const ws = new WebSocket((location.protocol === "https:" ? "wss://" : "ws://") + location.host + "/ws");
ws.onmessage = (ev) => { lastSnapshot = JSON.parse(ev.data); render(lastSnapshot); };
ws.onclose = () => { $("state").textContent = "disconnected"; setTimeout(() => location.reload(), 2000); };

// ---- cortex map state (static payload fetched once) ----
let MAP = null;        // { neurons, names, anchors, edges, regions, links }
let lastSnapshot = null;
let heatBuf = null;

fetch("/api/map").then((r) => r.json()).then((m) => {
  MAP = m;
  drawCortexBase();
  heatBuf = new Float32Array(m.neurons.length / 2);
  requestAnimationFrame(animateCortex);
});

function mapExtent() {
  let maxX = 1, maxY = 1;
  for (let i = 0; i < MAP.neurons.length; i += 2) {
    if (MAP.neurons[i] > maxX) maxX = MAP.neurons[i];
    if (MAP.neurons[i + 1] > maxY) maxY = MAP.neurons[i + 1];
  }
  return [maxX, maxY];
}

function toCanvas(idx, c) {
  const [mx, my] = mapExtent();
  return [
    30 + (MAP.neurons[idx * 2] / mx) * (c.width - 60),
    c.height - 20 - (MAP.neurons[idx * 2 + 1] / my) * (c.height - 40),
  ];
}

let baseCanvas = null;
function drawCortexBase() {
  baseCanvas = document.createElement("canvas");
  baseCanvas.width = $("cortex").width;
  baseCanvas.height = $("cortex").height;
  const ctx = baseCanvas.getContext("2d");
  ctx.fillStyle = "#070a10";
  ctx.fillRect(0, 0, baseCanvas.width, baseCanvas.height);
  for (let i = 0; i < MAP.neurons.length / 2; i++) {
    const [x, y] = toCanvas(i, baseCanvas);
    ctx.fillStyle = "rgba(70, 90, 120, 0.30)";
    ctx.fillRect(x, y, 1.4, 1.4);
  }
}

// rAF loop: smooth glow decay + travelling spikes along sampled axons
function animateCortex() {
  const c = $("cortex");
  if (!MAP || !baseCanvas) { requestAnimationFrame(animateCortex); return; }
  const ctx = c.getContext("2d");
  ctx.drawImage(baseCanvas, 0, 0);
  const n = MAP.neurons.length / 2;

  // ingest new spikes from the latest snapshot
  if (lastSnapshot) {
    const seen = lastSnapshot.Raster || [];
    for (const p of seen) {
      const idx = p[1] | 0;
      if (idx >= 0 && idx < n && heatBuf[idx] < 0.95) heatBuf[idx] = 1;
    }
  }

  // glow pass: neurons shimmer where recently active
  for (let i = 0; i < n; i++) {
    if (heatBuf[i] <= 0.04) { heatBuf[i] = 0; continue; }
    const [x, y] = toCanvas(i, c);
    const h = heatBuf[i];
    ctx.globalAlpha = h;
    ctx.fillStyle = regionColors[MAP.regions[i] % regionColors.length];
    const size = 1.6 + 3.2 * h;
    ctx.fillRect(x - size / 2, y - size / 2, size, size);
    heatBuf[i] *= 0.90;
  }

  // propagation pass: sparks travel from firing neurons to sampled axon targets
  if (lastSnapshot) {
    const now = Math.max(1, lastSnapshot.SimSeconds);
    const seen = lastSnapshot.Raster || [];
    let drawn = 0;
    for (const p of seen) {
      if (drawn > 220) break;
      const [tSec, neuronIdx] = p;
      const age = (now - tSec) / 0.35;          // travel time ~0.35 sim-sec
      if (age < 0 || age > 1) continue;
      const from = neuronIdx | 0;
      if (from * 3 + 2 >= MAP.links.length) continue;
      for (let k = 0; k < 3; k++) {
        const to = MAP.links[from * 3 + k];
        if (to < 0) continue;
        const [x1, y1] = toCanvas(from, c);
        const [x2, y2] = toCanvas(to, c);
        const px = x1 + (x2 - x1) * age;
        const py = y1 + (y2 - y1) * age;
        ctx.globalAlpha = (1 - age) * 0.9;
        ctx.fillStyle = regionColors[MAP.regions[from] % regionColors.length];
        ctx.fillRect(px, py, 1.8, 1.8);
        drawn++;
      }
    }
  }
  ctx.globalAlpha = 1;
  requestAnimationFrame(animateCortex);
}

function drawCortex(s) {
  // static in rAF loop; only caption refresh here
  const c = $("cortex"), ctx = c.getContext("2d");
  if (!MAP) return;
  ctx.fillStyle = "#33445c";
  ctx.font = "10px Consolas";
  ctx.fillText((s.Raster || []).length + " recent spikes", 36, 14);
}

function drawRegionGraph(s) {
  const c = $("regions"), ctx = c.getContext("2d");
  if (!MAP) return;
  ctx.fillStyle = "#070a10";
  ctx.fillRect(0, 0, c.width, c.height);
  const [mx, my] = mapExtent();
  const rates = s.RegionRates || [];
  const maxRate = Math.max(0.05, ...rates);
  const P = (i) => {
    const [ax, ay] = MAP.anchors[i];
    return [
      60 + (ax / mx) * (c.width - 120) + (mx / 2.5) * 6,   // spread anchors apart a little
      c.height - 50 - (ay / my) * (c.height - 100) - (my / 2.5) * 4,
    ];
  };
  // edges: opacity by source region rate (traffic)
  for (const [from, to, w] of MAP.edges) {
    const [x1, y1] = P(from), [x2, y2] = P(to);
    const traffic = rates[from] / maxRate;
    ctx.strokeStyle = `rgba(70, 224, 192, ${0.05 + 0.5 * traffic})`;
    ctx.lineWidth = 0.6 + Math.min(2, w / 15);
    ctx.beginPath();
    ctx.moveTo(x1, y1);
    ctx.lineTo(x2, y2);
    ctx.stroke();
  }
  // nodes: glow by rate
  ctx.font = "11px Consolas";
  for (let i = 0; i < MAP.names.length; i++) {
    const [x, y] = P(i);
    const rate = rates[i] || 0;
    const glow = rate / maxRate;
    const r = 8 + 16 * glow;
    const grad = ctx.createRadialGradient(x, y, 1, x, y, r);
    grad.addColorStop(0, regionColors[i % regionColors.length]);
    grad.addColorStop(1, "rgba(0,0,0,0)");
    ctx.globalAlpha = 0.35 + 0.65 * glow;
    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fill();
    ctx.globalAlpha = 1;
    ctx.fillStyle = "#c9d6e8";
    ctx.fillText(MAP.names[i], x + 12, y + 3);
    ctx.fillStyle = "#5c6c84";
    ctx.fillText((rate * 1000).toFixed(0) + " sp/s", x + 12, y + 15);
  }
}

function render(s) {
  $("clock").textContent = "t=" + s.SimSeconds.toFixed(0) + "s (" + s.RealtimeFactor.toFixed(2) + "x)";
  $("state").textContent = s.SleepState;
  $("feeling").textContent = s.Feeling;
  $("rate").textContent = s.MeanRateHz.toFixed(2) + " Hz";
  $("cost").textContent = "$" + s.SpentUsd.toFixed(3) + " / " + s.ApiCalls + " calls";
  $("senses").textContent = (s.CameraLive ? "cam:live" : "cam:mock") + " " +
    (s.MicLive ? "mic:live" : "mic:mock") + " " + (s.MetaLive ? "meta:live" : "meta:mock");
  $("rec").classList.toggle("on", !!s.CameraLive);
  $("feeling-narrative").textContent = s.FeelingNarrative;
  $("energy").textContent = s.Energy.toFixed(0);
  $("clockrate").textContent = s.ClockRate.toFixed(2) + "x";
  $("subj").textContent = s.SubjectiveMinutes.toFixed(1) + " min";
  $("pressure").textContent = s.SleepPressure.toFixed(2);
  $("ignitions").textContent = s.Ignitions;
  $("surprises").textContent = s.Surprises;
  $("replays").textContent = s.Replays;
  $("meta-badge").textContent = "meta tier: " + (s.MetaLive ? "live" : "mock");

  drawCortex(s);
  drawRegionGraph(s);
  drawRaster(s);
  drawSpectrogram(s);
  drawCircumplex(s);
  drawSleep(s);
  drawMods(s);
  drawDreams(s);
  drawChat(s);

  $("lfp-slope").textContent = s.LfpSlope.toFixed(2);
  $("alpha-ratio").textContent = s.AlphaRatio.toFixed(2);

  setFeed($("plain"), s.PlainThoughts);
  setFeed($("thought"), s.Thought);
  setFeed($("monologue"), s.Monologues);
  setFeed($("episodes"), s.RecentEpisodes);
}

function drawDreams(s) {
  const wrap = $("dreams");
  const dreams = s.Dreams || [];
  if (dreams.length === wrap.childElementCount && dreams.length > 0 &&
      (wrap.lastElementChild && wrap.lastElementChild.title === dreams[dreams.length - 1])) return;
  wrap.innerHTML = "";
  for (const d of dreams) {
    const div = document.createElement("div");
    if (d.endsWith(".mock.png") || d.includes("mock")) {
      div.className = "mock";
      div.textContent = "dream (mock tier)";
    } else {
      const img = document.createElement("img");
      img.src = "/dreams/" + d;
      img.title = d;
      div.appendChild(img);
    }
    div.title = d;
    wrap.appendChild(div);
  }
}

function drawChat(s) {
  const log = $("chat");
  const msgs = s.Chat || [];
  const last = msgs.length ? msgs[msgs.length - 1] : "";
  if (log.childElementCount === msgs.length && log.lastElementChild &&
      log.lastElementChild.dataset.raw === last) return;
  log.innerHTML = "";
  for (const m of msgs) {
    const div = document.createElement("div");
    const you = m.startsWith("You:");
    div.className = "msg " + (you ? "you" : "brain");
    div.dataset.raw = m;
    const who = document.createElement("div");
    who.className = "who";
    who.textContent = you ? "you" : "brain";
    div.appendChild(who);
    div.appendChild(document.createTextNode(you ? m.slice(5) : m.slice(7)));
    log.appendChild(div);
  }
  log.scrollTop = log.scrollHeight;
}

$("chatform").addEventListener("submit", async (ev) => {
  ev.preventDefault();
  const box = $("chatbox");
  const text = box.value.trim();
  if (!text) return;
  box.value = "";
  try {
    await fetch("/api/say", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text }),
    });
  } catch (e) { console.warn("say failed", e); }
});

function setFeed(ul, lines) {
  if (!lines || lines.length === 0) return;
  ul.innerHTML = "";
  for (let i = lines.length - 1; i >= 0; i--) {
    const li = document.createElement("li");
    const m = lines[i].match(/^\[([^\]]+)\]\s*(.*)$/);
    if (m) {
      const t = document.createElement("span");
      t.className = "t";
      t.textContent = m[1];
      li.appendChild(t);
      li.appendChild(document.createTextNode(m[2]));
    } else li.textContent = lines[i];
    ul.appendChild(li);
  }
}

function totalNeurons(s) {
  const regions = s.Regions || [];
  return regions.reduce((a, r) => a + r[1], 0) || 1;
}

function drawRaster(s) {
  const c = $("raster"), ctx = c.getContext("2d");
  ctx.fillStyle = "#070a10";
  ctx.fillRect(0, 0, c.width, c.height);
  const now = Math.max(1, s.SimSeconds);
  const windowSec = 12;
  const t0 = Math.max(0, now - windowSec);
  const labelW = 46;

  // region bands with labels; neuron fraction maps inside its band
  const regions = s.Regions || [];
  const total = regions.reduce((a, r) => a + r[1], 0) || 1;
  let y0 = 0;
  ctx.font = "9px Consolas";
  for (let i = 0; i < regions.length; i++) {
    const h = (regions[i][1] / total) * c.height;
    if (i % 2 === 1) {
      ctx.fillStyle = "#0a0f17";
      ctx.fillRect(labelW, y0, c.width - labelW, h);
    }
    ctx.strokeStyle = "#141c28";
    ctx.beginPath();
    ctx.moveTo(labelW, y0);
    ctx.lineTo(c.width, y0);
    ctx.stroke();
    ctx.fillStyle = "#4d5c73";
    ctx.fillText(regions[i][0].slice(0, 6), 4, y0 + Math.min(h, 10));
    regions[i]._y0 = y0;
    regions[i]._h = h;
    y0 += h;
  }

  for (const p of s.Raster || []) {
    const [tSec, neuronIdx, region] = p;
    if (tSec < t0) continue;
    const x = labelW + ((tSec - t0) / windowSec) * (c.width - labelW);
    const r = regions[region];
    if (!r || !r._h) continue;
    const frac = neuronIdx / totalNeurons(s);
    // frac is global neuron fraction; re-map into this region's band
    const before = regions.slice(0, region).reduce((a, q) => a + q[1], 0);
    const inBand = (frac * totalNeurons(s) - before) / r[1];
    const yy = r._y0 + (1 - Math.min(0.999, Math.max(0, inBand))) * r._h;
    ctx.fillStyle = regionColors[region % regionColors.length];
    ctx.fillRect(x, yy, 1.4, 1.4);
  }
  ctx.fillStyle = "#33445c";
  ctx.font = "10px Consolas";
  ctx.fillText("last " + windowSec + "s   |   " + (s.Raster || []).length + " spikes", labelW + 8, 12);
}

function drawSpectrogram(s) {
  const c = $("spectrogram"), ctx = c.getContext("2d");
  ctx.fillStyle = "#070a10";
  ctx.fillRect(0, 0, c.width, c.height);
  const bands = ["delta 1-4", "theta 4-8", "alpha 8-12", "beta 12-30", "gamma 30-80"];
  const hist = s.LfpHistory || [];
  if (hist.length === 0) return;

  // color scale from max power in view
  let max = 1e-9;
  for (const col of hist) for (const v of col) if (v > max) max = v;

  const labelW = 86;
  const bh = (c.height - 8) / 5;
  ctx.font = "10px Consolas";
  for (let col = 0; col < hist.length; col++)
  {
    const x = labelW + (col / Math.max(1, hist.length - 1)) * (c.width - labelW - 6);
    for (let b = 0; b < 5; b++)
    {
      const p = Math.log10(1 + 9 * (hist[col][b] / max)); // 0..1 gamma-ish stretch
      const y = 4 + b * bh;
      const hue = 175 - b * 12;
      ctx.fillStyle = `hsla(${hue}, 80%, ${Math.round(6 + p * 52)}%, 1)`;
      ctx.fillRect(x - 2, y, 4, bh - 2);
    }
  }
  ctx.fillStyle = "#5c6c84";
  for (let b = 0; b < 5; b++)
    ctx.fillText(bands[b], 4, 4 + b * bh + bh / 2 + 3);
}

function drawCircumplex(s) {
  const c = $("circumplex"), ctx = c.getContext("2d");
  ctx.fillStyle = "#070a10";
  ctx.fillRect(0, 0, c.width, c.height);
  const cx = c.width / 2, cy = c.height / 2, r = c.width / 2 - 12;
  ctx.strokeStyle = "#1b2433";
  ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(cx - r, cy); ctx.lineTo(cx + r, cy);
  ctx.moveTo(cx, cy - r); ctx.lineTo(cx, cy + r); ctx.stroke();
  ctx.fillStyle = "#5c6c84"; ctx.font = "10px Consolas";
  ctx.fillText("arousal", cx + 6, 12); ctx.fillText("valence +", c.width - 58, cy - 6);
  ctx.fillText("valence -", 6, cy - 6); ctx.fillText("calm", cx + 6, c.height - 4);

  const px = cx + s.Valence * (r - 14);
  const py = cy - (s.Arousal - 0.5) * 2 * (r - 14);
  ctx.fillStyle = colorForValence(s.Valence);
  ctx.beginPath(); ctx.arc(px, py, 7, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#c9d6e8";
  ctx.fillText(s.Feeling, px + 12, py + 3);
}

function colorForValence(v) {
  return v > 0.1 ? "#46e0c0" : v < -0.1 ? "#e05a6a" : "#e0a546";
}

function drawMods(s) {
  const names = ["dopamine", "serotonin", "acetylcholine", "noradrenaline", "oxytocin"];
  const colors = ["#46e0c0", "#9d7ce0", "#e0d05a", "#e05a6a", "#e07db1"];
  const wrap = $("mods");
  wrap.innerHTML = "";
  names.forEach((n, i) => {
    const v = (s.Modulators && s.Modulators[i]) || 0;
    const row = document.createElement("div");
    row.className = "mod";
    row.innerHTML =
      '<div class="label"><span>' + n + '</span><span class="dim">' + v.toFixed(2) + '</span></div>' +
      '<div class="bar"><i style="width:' + Math.min(100, (v / 1.5) * 100) + '%;background:' + colors[i] + '"></i></div>';
    wrap.appendChild(row);
  });
}

function drawSleep(s) {
  const c = $("sleepstrip"), ctx = c.getContext("2d");
  ctx.fillStyle = "#070a10";
  ctx.fillRect(0, 0, c.width, c.height);
  const strip = s.SleepStrip || [];
  if (strip.length < 2) return;
  const tMin = strip[0][0], tMax = Math.max(strip[strip.length - 1][0], tMin + 1);
  const colors = { Wake: "#46e0c0", Drowsy: "#e0d05a", NREM: "#5c6c84", REM: "#9d7ce0" };
  for (let i = 0; i < strip.length - 1; i++) {
    const [t0, st] = strip[i];
    const t1 = strip[i + 1][0];
    const x0 = ((t0 - tMin) / (tMax - tMin)) * c.width;
    const x1 = ((t1 - tMin) / (tMax - tMin)) * c.width;
    ctx.fillStyle = colors[st] || "#5c6c84";
    ctx.fillRect(x0, 20, Math.max(2, x1 - x0), 24);
  }
}
