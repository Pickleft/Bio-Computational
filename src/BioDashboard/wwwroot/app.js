// Bio-Computational dashboard: vanilla JS, one WebSocket at 10 Hz.
"use strict";

if (new URLSearchParams(location.search).has("demo")) document.body.classList.add("demo");

const $ = (id) => document.getElementById(id);
const regionColors = ["#46e0c0", "#5aa9e0", "#9d7ce0", "#e0a546", "#e05a6a", "#6ae08d",
  "#e07db1", "#8ecfe0", "#cfe08d", "#a0a0ff", "#e0d05a", "#7d8fa8"];

const ws = new WebSocket((location.protocol === "https:" ? "wss://" : "ws://") + location.host + "/ws");
ws.onmessage = (ev) => render(JSON.parse(ev.data));
ws.onclose = () => { $("state").textContent = "disconnected"; setTimeout(() => location.reload(), 2000); };

let lastRasterT = 0;

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

  drawRaster(s);
  drawCircumplex(s);
  drawSleep(s);
  drawMods(s);
  drawDreams(s);

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

function drawRaster(s) {
  const c = $("raster"), ctx = c.getContext("2d");
  ctx.fillStyle = "#070a10";
  ctx.fillRect(0, 0, c.width, c.height);
  const now = Math.max(1, s.SimSeconds);
  const windowSec = 12;
  const t0 = Math.max(0, now - windowSec);
  for (const p of s.Raster || []) {
    const [tSec, frac, region] = p;
    if (tSec < t0) continue;
    const x = ((tSec - t0) / windowSec) * c.width;
    const y = (1 - frac) * c.height;
    ctx.fillStyle = regionColors[region % regionColors.length];
    ctx.fillRect(x, y, 1.6, 1.6);
  }
  ctx.fillStyle = "#33445c";
  ctx.font = "10px Consolas";
  ctx.fillText("last " + windowSec + "s   |   " + s.Raster.length + " spikes", 8, 12);
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
