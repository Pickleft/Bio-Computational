// Bio-Computational dashboard: crisp, DPR-aware, one WebSocket at 10 Hz.
"use strict";

if (new URLSearchParams(location.search).has("demo")) document.body.classList.add("demo");

const $ = (id) => document.getElementById(id);
const DPR = window.devicePixelRatio || 1;

const PALETTE = [
  "#3ddbb8", "#5ba8e0", "#a78bfa", "#f0b45c", "#f07080", "#6ee7a0",
  "#e888b8", "#7dd0e8", "#c8e888", "#8888f8", "#e8d05c", "#7d8fa8",
];

const MONO = "'JetBrains Mono', 'Cascadia Code', Consolas, monospace";
const UI = "'Inter', -apple-system, sans-serif";

// ---- canvas helper: set resolution to CSS size x DPR for crisp rendering ----
function fitCanvas(c) {
  const rect = c.getBoundingClientRect();
  if (rect.width === 0) return null;
  const w = Math.round(rect.width * DPR);
  const h = Math.round(rect.height * DPR);
  if (c.width !== w || c.height !== h) { c.width = w; c.height = h; }
  const ctx = c.getContext("2d");
  ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
  return { ctx, w: rect.width, h: rect.height };
}

// ---- state ----
let MAP = null;
let lastSnap = null;

const ws = new WebSocket((location.protocol === "https:" ? "wss://" : "ws://") + location.host + "/ws");
ws.onmessage = (ev) => { lastSnap = JSON.parse(ev.data); render(lastSnap); };
ws.onclose = () => { $("state").textContent = "offline"; setTimeout(() => location.reload(), 3000); };

// ---- the brain's voice ----
// Natural browser voices (Edge/Chrome online neural voices, Google voices on
// Android) replace robotic local TTS. Prosody comes from the snapshot: the
// pitch and rate the brain developed from the voices it heard, layered with
// its current feeling. The voice is heard on every device watching the brain.
let spokenSeq = 0;
let pickedVoice = null;
window.brainBrowserSpeaking = false;

function pickVoice() {
  const voices = speechSynthesis.getVoices();
  if (!voices.length) return null;
  // STRICT: only genuinely natural voices. The robotic Microsoft SAPI voices
  // (David/Zira) are deliberately excluded - a network voice or the browser
  // default beats them. Log tells you which one you got.
  const prefer = [
    /Natural.*English/i, /Neural.*English/i, /Online.*English/i,
    /Google (US|UK) English/i, /Aria|Jenny|Guy|Ana/i,
  ];
  for (const re of prefer) {
    const v = voices.find((x) => re.test(x.name));
    if (v) { console.log("[voice] using", v.name); return v; }
  }
  return voices.find((v) => /^en/i.test(v.lang)) || voices[0];
}

function speakBrain(s) {
  if (!("speechSynthesis" in window)) return;
  if (s.SpeakSeq && s.SpeakSeq !== spokenSeq && s.SpeakText) {
    spokenSeq = s.SpeakSeq;
    // HUMAN PROSODY: split into sentence/clause chunks and speak them as a
    // sequence with natural micro-pauses - one long run-on utterance is the
    // single biggest tell of robotic TTS. Each chunk gets a tiny organic
    // rate/pitch wobble so no two sentences sound identical.
    const chunks = s.SpeakText.match(/[^.!?…,]+[.!?…,]*/g) || [s.SpeakText];
    // QUEUE, never cancel: cutting the previous sentence mid-word is what
    // made it interrupt its own thoughts; utterances now play in order
    let first = true;
    for (const chunk of chunks) {
      const text = chunk.trim();
      if (!text) continue;
      const u = new SpeechSynthesisUtterance(text);
      if (pickedVoice) u.voice = pickedVoice;
      const wobble = ((spokenSeq + text.length) % 5 - 2) * 0.015; // organic drift
      u.rate = Math.max(0.6, Math.min(1.4, (s.SpeakRate || 1) + wobble));
      u.pitch = Math.max(0.5, Math.min(1.7, (s.SpeakPitch || 1) + wobble * 0.8));
      if (first) { u.onstart = () => { window.brainBrowserSpeaking = true; }; first = false; }
      u.onend = u.onerror = () => { if (!speechSynthesis.pending) window.brainBrowserSpeaking = false; };
      speechSynthesis.speak(u);
    }
  }
}

if ("speechSynthesis" in window) {
  pickedVoice = pickVoice();
  const GOOD = /Natural|Neural|Online|Google|Aria|Jenny|Guy|Ana/i;
  // VOICE PICKER: lets you choose the brain's voice directly; starred names
  // are the human-quality ones. Choice persists in localStorage.
  const fillPicker = () => {
    const sel = document.getElementById("voice-pick");
    if (!sel) return;
    const voices = speechSynthesis.getVoices().filter((v) => /^en/i.test(v.lang));
    if (!voices.length) return;
    sel.innerHTML = "";
    voices.forEach((v) => {
      const o = document.createElement("option");
      o.value = v.name;
      o.textContent = (GOOD.test(v.name) ? "★ " : "") + v.name;
      sel.appendChild(o);
    });
    const saved = localStorage.getItem("brainVoice");
    if (saved && voices.some((v) => v.name === saved)) {
      sel.value = saved;
      pickedVoice = voices.find((v) => v.name === saved);
    } else if (pickedVoice) sel.value = pickedVoice.name;
    // honest warning: if ONLY robotic voices exist, say so once
    if (!voices.some((v) => GOOD.test(v.name))) {
      sel.style.borderColor = "#f07080";
      sel.title = "Only robotic voices found on this machine/browser — open this page in Microsoft Edge for the free neural (Natural) voices, or pick the least-bad one";
    }
  };
  fillPicker();
  document.addEventListener("change", (e) => {
    if (e.target && e.target.id === "voice-pick") {
      pickedVoice = speechSynthesis.getVoices().find((v) => v.name === e.target.value) || pickedVoice;
      localStorage.setItem("brainVoice", e.target.value);
      console.log("[voice] user picked", e.target.value);
    }
  });
  speechSynthesis.onvoiceschanged = () => { pickedVoice = pickVoice(); fillPicker(); };
  // mobile autoplay policy: speech needs one user gesture before it can
  // start. Unlock on the first tap/click anywhere, then re-pick voices
  // (Android populates the voice list only after interaction).
  const unlock = () => {
    try {
      const u = new SpeechSynthesisUtterance(" ");
      u.volume = 0;
      speechSynthesis.speak(u);
      pickedVoice = pickVoice();
      console.log("[voice] unlocked by user gesture");
    } catch { }
    document.removeEventListener("pointerdown", unlock);
  };
  document.addEventListener("pointerdown", unlock);
}

fetch("/api/map").then((r) => r.json()).then((m) => { MAP = m; });

// ---- render ----
function render(s) {
  $("clock").textContent = s.SimSeconds.toFixed(0) + "s";
  $("state").textContent = s.SleepState;
  $("feeling").textContent = s.Feeling;
  $("rate").textContent = s.MeanRateHz.toFixed(2) + " Hz";
  $("cost").textContent = "$" + s.SpentUsd.toFixed(3);
  $("senses").textContent =
    (s.CameraLive ? "cam" : "—") + " " + (s.MicLive ? "mic" : "—") + " " + (s.MetaLive ? "api" : "mock");
  $("rec").classList.toggle("on", !!s.CameraLive);
  $("feeling-narrative").textContent = s.FeelingNarrative;
  $("energy").textContent = fmt(s.Energy);
  $("clockrate").textContent = s.ClockRate.toFixed(2) + "×";
  $("subj").textContent = s.SubjectiveMinutes.toFixed(1) + "m";
  $("pressure").textContent = s.SleepPressure.toFixed(2);
  $("ignitions").textContent = s.Ignitions;
  $("surprises").textContent = s.Surprises;
  $("replays").textContent = s.Replays;
  $("episode-count").textContent = s.Episodes;
  $("lfp-slope").textContent = s.LfpSlope.toFixed(1);
  $("alpha-ratio").textContent = s.AlphaRatio.toFixed(1);
  $("meta-badge").textContent = s.MetaLive ? "live" : "mock";

  drawRegionGraph(s);
  drawConsciousness(s);
  drawJourney(s);
  drawPalace(s);
  drawSpectrogram(s);
  drawCircumplex(s);
  drawSleep(s);
  drawMods(s);
  drawDreams(s);
  drawChat(s);
  if (typeof brainMusicTick === "function") brainMusicTick(s);
  if (typeof updateAvatarState === "function") updateAvatarState(s);
  speakBrain(s);
  // THE LIVING CAPTION: what it's doing RIGHT NOW, directly under the face.
  // Priority: speaking > thinking > dreaming > sleeping > feeling label
  const sn = $("saying-now");
  if (sn) {
    if ((s.IsSpeaking || window.brainBrowserSpeaking) && s.SpeakText) {
      sn.textContent = `“${s.SpeakText}”`;
      sn.style.fontStyle = "italic";
    } else {
      const tt = (s.ThoughtThread || []);
      const last = tt.length ? tt[tt.length - 1] : "";
      const text = last ? last.split("|").slice(3).join("|") : "";
      if (s.SleepState === "NREM") { sn.textContent = "sleeping…"; sn.style.fontStyle = "normal"; }
      else if (s.SleepState === "REM") { sn.textContent = s.LucidDreams > 0 ? "lucid dreaming…" : "dreaming…"; sn.style.fontStyle = "italic"; }
      else if (text) { sn.textContent = `thinking: ${text}`; sn.style.fontStyle = "italic"; }
      else { sn.textContent = s.FeelingNarrative || ""; sn.style.fontStyle = "normal"; }
    }
  }
  if (typeof drawEEG === "function") drawEEG(s);
  if (typeof drawPhiGauge === "function") drawPhiGauge(s);
  if (typeof drawFreeEnergy === "function") drawFreeEnergy(s);
  if (typeof drawManifold === "function") drawManifold(s);

  $("consciousness-label").textContent = s.ConsciousnessLabel || "—";

  // social bonds: who the brain knows and how well
  const bonds = s.SocialBonds || {};
  const bondKeys = Object.keys(bonds);
  const bondEl = $("social-bonds");
  if (bondEl && bondKeys.length > 0) {
    const strengths = s.BondStrengths || {};
    if (bondEl.childElementCount !== bondKeys.length ||
        (bondEl.firstElementChild && bondEl.firstElementChild.dataset.id !== bondKeys[0])) {
      bondEl.innerHTML = "";
      for (const [id, visits] of Object.entries(bonds)) {
        const div = document.createElement("div");
        div.className = "bond";
        div.dataset.id = id;
        const short = id.split(":")[0].substring(0, 12);
        // the bar now shows BOND DEPTH (visits x warmth x recency), not raw visits
        const strength = Math.round((strengths[id] || 0) * 100);
        div.innerHTML = `<span class="bond-name">${short}</span>` +
          `<div class="bond-bar"><i style="width:${strength}%"></i></div>` +
          `<span class="bond-count" title="bond depth">${strength}%</span>`;
        bondEl.appendChild(div);
      }
    }
  }

  setFeed($("plain"), s.PlainThoughts);
  setFeed($("monologue"), s.Monologues);
  setFeed($("episodes"), s.RecentEpisodes);
  drawThread(s);

  // psychology readout (Social tab): who the brain is becoming
  const set = (id, text) => { const el = $(id); if (el) el.textContent = text; };
  set("knows-name", s.UserName || "a stranger");
  set("attach-style", s.Attachment || "forming");
  set("empathy-mood", (s.EmpathyConfidence || 0) > 0.3 ? `${s.UserMood} voice` : "no voice yet");
  set("curiosity-prog", `progress ${(s.CuriosityProgress || 0).toFixed(2)}, reward ${(s.CuriosityReward || 0).toFixed(2)}`);
  set("play-count", `${s.PlayBursts || 0} bursts${s.Playful ? " (playing now)" : ""}`);
  set("insight-count", `${s.Insights || 0}${s.OpenLoops ? ` (+${s.OpenLoops} unresolved)` : ""}`);
  set("allostatic", s.AllostaticNote || "body steady");
  set("voice-growth", s.VoiceSyllables > 0
    ? `${s.VoiceSyllables} syllables, ${s.VoiceClipsHeard} voices heard${s.VoiceMimicries ? `, ${s.VoiceMimicries} mimicked` : ""}`
    : "listening");
  set("semantic-growth", `${s.Concepts || 0} concepts, ${s.ConceptLinks || 0} associations wired`);
  set("turn-state", s.TurnState || "silent room");
  const dg = s.Dreams || [];
  set("dream-count", `${dg.length}${s.LucidDreams ? ` (${s.LucidDreams} lucid)` : ""}`);
  set("neurogenesis", `${s.NeuronsBorn || 0} in ${s.BirthEvents || 0} events`);
  // LIVE STRIP from the SERVER transcripts (always works - the browser
  // recognizer is flaky; the server chain is the source of truth)
  const logs = s.TranscriptLog || [];
  const lf = document.getElementById("live-final");
  if (lf && logs.length && lf.dataset.t !== logs[logs.length-1]) {
    lf.dataset.t = logs[logs.length-1];
    lf.textContent = logs[logs.length-1];
  }
  if (lf && !logs.length && !lf.dataset.t && s.AudioLive) lf.textContent = "(hearing you - listening for words...)";

  // live transcribe panel: proves whether the mic is actually intercepted
  const lvl = document.getElementById("audio-level");
  if (lvl) lvl.style.width = Math.min(100, (s.AudioRms || 0) * 300).toFixed(0) + "%";
  set("audio-source", "source: " + (s.AudioSource || "none"));
  set("audio-live", s.AudioLive ? "AUDIO ARRIVING" : "no audio arriving");
  const liveEl = $("audio-live");
  if (liveEl) liveEl.style.color = s.AudioLive ? "var(--pos, #6ee7a0)" : "var(--neg, #f07080)";
  set("audio-vad", s.AudioVad ? "VOICE DETECTED" : "—");
  set("audio-clip", s.AudioClipBuilding ? "capturing speech..." : "idle");
  set("audio-blocks", String(s.AudioBlocks || 0));
  const tl = $("transcripts");
  if (tl) {
    const lines = s.TranscriptLog || [];
    if (tl.childElementCount !== lines.length || (lines.length && tl.lastChild && tl.lastChild.textContent !== lines[lines.length - 1])) {
      tl.innerHTML = "";
      for (let i = lines.length - 1; i >= 0; i--) {
        const li = document.createElement("li");
        li.textContent = lines[i];
        tl.appendChild(li);
      }
    }
  }
  setFeed($("thought"), s.Thought);
  if (s.DreamNarratives) setFeed($("dream-stories"), s.DreamNarratives);
}

function fmt(n) {
  if (n > 1e6) return (n / 1e6).toFixed(1) + "M";
  if (n > 1e3) return (n / 1e3).toFixed(0) + "k";
  return n.toFixed(0);
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

// ---- the thought thread: the live chain developing link by link ----
// each link is colored by its evaluated valence (green positive, red negative,
// gray neutral) and faded by decaying salience: unresolved thoughts glow,
// boring ones fade out before the thread dies and a new one seeds
function drawThread(s) {
  const el = $("thread");
  if (!el) return;
  const stats = $("thread-stats");
  if (stats) stats.textContent =
    `${s.ThoughtChains || 0} chains, ${s.ThoughtInhibited || 0} repeats inhibited`;
  const links = s.ThoughtThread || [];
  el.innerHTML = "";
  if (!links.length) {
    const li = document.createElement("li");
    li.style.opacity = "0.4";
    li.textContent = "no active thread - waiting for the next thought to surface...";
    el.appendChild(li);
    return;
  }
  for (const raw of links) {
    const parts = raw.split("|");
    const depth = parts[0], val = parseFloat(parts[1]), sal = parseFloat(parts[2]), text = parts.slice(3).join("|");
    const li = document.createElement("li");
    const color = val > 0.2 ? "#6ee7a0" : val < -0.2 ? "#f07080" : "#7d8fa8";
    li.style.borderLeft = `2px solid ${color}`;
    li.style.paddingLeft = "8px";
    li.style.opacity = (0.45 + 0.55 * Math.min(1, sal * 1.5)).toFixed(2);
    const d = document.createElement("span");
    d.className = "t";
    d.textContent = depth;
    li.appendChild(d);
    li.appendChild(document.createTextNode(" " + text));
    el.appendChild(li);
  }
}

// ---- region graph ----
function drawRegionGraph(s) {
  const c = $("regions");
  const f = fitCanvas(c);
  if (!f || !MAP) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d1017";
  ctx.fillRect(0, 0, w, h);

  let maxX = 1, maxY = 1;
  for (const [ax, ay] of MAP.anchors) {
    if (ax > maxX) maxX = ax;
    if (ay > maxY) maxY = ay;
  }

  const rates = s.RegionRates || [];
  const maxRate = Math.max(0.05, ...rates);
  const pad = 60;

  const P = (i) => {
    const [ax, ay] = MAP.anchors[i];
    return [pad + (ax / maxX) * (w - pad * 2) + 20, h - 40 - (ay / maxY) * (h - 80)];
  };

  for (const [from, to, wt] of MAP.edges) {
    const [x1, y1] = P(from), [x2, y2] = P(to);
    const traffic = (rates[from] || 0) / maxRate;
    ctx.strokeStyle = `rgba(61, 219, 184, ${0.04 + 0.35 * traffic})`;
    ctx.lineWidth = 0.5 + Math.min(2.5, wt / 20);
    ctx.beginPath(); ctx.moveTo(x1, y1); ctx.lineTo(x2, y2); ctx.stroke();
  }

  ctx.font = `600 10px ${MONO}`;
  for (let i = 0; i < MAP.names.length; i++) {
    const [x, y] = P(i);
    const rate = rates[i] || 0;
    const glow = rate / maxRate;
    const r = 6 + 14 * glow;
    const grad = ctx.createRadialGradient(x, y, 1, x, y, r);
    grad.addColorStop(0, PALETTE[i % PALETTE.length]);
    grad.addColorStop(1, "rgba(0,0,0,0)");
    ctx.globalAlpha = 0.3 + 0.7 * glow;
    ctx.fillStyle = grad;
    ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill();
    ctx.globalAlpha = 1;
    ctx.fillStyle = "#d1d9e6";
    ctx.fillText(MAP.names[i], x + 14, y + 3);
    ctx.fillStyle = "#5a6474";
    ctx.fillText((rate * 1000).toFixed(0) + "/s", x + 14, y + 14);
  }
}

// ---- consciousness gauge: a single dramatic dial ----
function drawConsciousness(s) {
  const c = $("consciousness");
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d1017";
  ctx.fillRect(0, 0, w, h);

  const score = s.Consciousness || 0;
  const cx = w / 2, cy = h * 0.42, r = Math.min(w, h) * 0.32;

  // arc background (track)
  ctx.strokeStyle = "#1c222e";
  ctx.lineWidth = 12;
  ctx.lineCap = "round";
  ctx.beginPath();
  ctx.arc(cx, cy, r, Math.PI * 0.75, Math.PI * 2.25);
  ctx.stroke();

  // arc fill (score)
  const frac = Math.PI * 1.5 * score;
  const color = score > 0.5 ? "#3ddbb8" : score > 0.25 ? "#f0b45c" : "#5a6474";
  const grad = ctx.createLinearGradient(cx - r, 0, cx + r, 0);
  grad.addColorStop(0, color + "44");
  grad.addColorStop(1, color);
  ctx.strokeStyle = grad;
  ctx.lineWidth = 12;
  ctx.beginPath();
  ctx.arc(cx, cy, r, Math.PI * 0.75, Math.PI * 0.75 + frac);
  ctx.stroke();

  // score text
  ctx.fillStyle = color;
  ctx.font = `700 ${Math.round(r * 0.5)}px ${UI}`;
  ctx.textAlign = "center";
  ctx.fillText((score * 100).toFixed(0), cx, cy + r * 0.15);
  ctx.fillStyle = "#5a6474";
  ctx.font = `500 10px ${MONO}`;
  ctx.fillText("consciousness", cx, cy + r * 0.35);
  ctx.textAlign = "left";
}

// ---- emotional journey: valence/arousal flowing through time ----
function drawJourney(s) {
  const c = $("journey");
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d1017";
  ctx.fillRect(0, 0, w, h);

  const journey = s.FeelingJourney || [];
  if (journey.length < 2) return;

  const tMin = journey[0][0], tMax = journey[journey.length - 1][0];
  const timeSpan = Math.max(1, tMax - tMin);
  const midY = h / 2;

  // zero lines
  ctx.strokeStyle = "#1c222e";
  ctx.lineWidth = 1;
  ctx.setLineDash([2, 4]);
  ctx.beginPath(); ctx.moveTo(0, midY); ctx.lineTo(w, midY); ctx.stroke();
  ctx.setLineDash([]);

  // valence ribbon (positive above, negative below)
  ctx.beginPath();
  ctx.moveTo(0, midY);
  for (const [t, valence] of journey) {
    const x = ((t - tMin) / timeSpan) * w;
    const y = midY - valence * midY * 0.8;
    ctx.lineTo(x, y);
  }
  ctx.lineTo(w, midY);
  ctx.closePath();

  const grad = ctx.createLinearGradient(0, 0, 0, h);
  grad.addColorStop(0, "rgba(61, 219, 184, 0.25)");
  grad.addColorStop(0.5, "rgba(0, 0, 0, 0)");
  grad.addColorStop(1, "rgba(240, 112, 128, 0.25)");
  ctx.fillStyle = grad;
  ctx.fill();

  // valence line
  ctx.beginPath();
  for (let i = 0; i < journey.length; i++) {
    const [t, valence] = journey[i];
    const x = ((t - tMin) / timeSpan) * w;
    const y = midY - valence * midY * 0.8;
    if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
  }
  ctx.strokeStyle = "#3ddbb8";
  ctx.lineWidth = 1.5;
  ctx.stroke();

  // arousal dots (size = arousal intensity)
  for (let i = 0; i < journey.length; i += 3) {
    const [t, valence, arousal] = journey[i];
    const x = ((t - tMin) / timeSpan) * w;
    const y = midY - valence * midY * 0.8;
    const r = 1 + arousal * 3;
    ctx.fillStyle = `rgba(240, 180, 92, ${0.3 + arousal * 0.5})`;
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fill();
  }

  // labels
  ctx.font = `500 9px ${MONO}`;
  ctx.fillStyle = "#5a6474";
  ctx.fillText("positive", 6, 12);
  ctx.fillText("negative", 6, h - 6);
  ctx.fillText(`last ${timeSpan.toFixed(0)}s`, w - 50, h - 6);
}

// ---- memory palace: emotional map of all memories ----
function drawPalace(s) {
  const c = $("palace");
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d1017";
  ctx.fillRect(0, 0, w, h);

  const memories = s.MemoryPalace || [];
  if (memories.length === 0) {
    ctx.fillStyle = "#5a6474";
    ctx.font = `500 10px ${MONO}`;
    ctx.fillText("no memories yet", w / 2 - 30, h / 2);
    return;
  }

  const cx = w / 2, cy = h / 2;

  // axes
  ctx.strokeStyle = "#1c222e";
  ctx.lineWidth = 1;
  ctx.setLineDash([2, 4]);
  ctx.beginPath(); ctx.moveTo(0, cy); ctx.lineTo(w, cy); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(cx, 0); ctx.lineTo(cx, h); ctx.stroke();
  ctx.setLineDash([]);

  // labels
  ctx.font = `500 8px ${MONO}`;
  ctx.fillStyle = "#5a6474";
  ctx.fillText("positive →", w - 40, cy - 4);
  ctx.fillText("← negative", 4, cy - 4);
  ctx.fillText("exciting", cx + 4, 10);
  ctx.fillText("calm", cx + 4, h - 6);

  // memory nodes
  for (const m of memories) {
    const [x, y, size, brightness, valence, tag] = m;
    const px = cx + x * (cx - 20);
    const py = cy - y * (cy - 20);
    const r = size * (w / 200); // scale to canvas
    const alpha = brightness;

    // color: green for positive, red for negative, amber for neutral
    const color = valence > 0.1 ? "#3ddbb8" : valence < -0.1 ? "#f07080" : "#f0b45c";

    // glow for significant memories
    if (Math.abs(valence) > 0.3) {
      const grad = ctx.createRadialGradient(px, py, 1, px, py, r * 2);
      grad.addColorStop(0, color + "88");
      grad.addColorStop(1, "rgba(0,0,0,0)");
      ctx.globalAlpha = alpha * 0.5;
      ctx.fillStyle = grad;
      ctx.beginPath(); ctx.arc(px, py, r * 2, 0, Math.PI * 2); ctx.fill();
    }

    ctx.globalAlpha = alpha;
    ctx.fillStyle = color;
    ctx.beginPath(); ctx.arc(px, py, r, 0, Math.PI * 2); ctx.fill();

    // tag label for significant memories
    if (Math.abs(valence) > 0.3 && brightness > 0.3) {
      ctx.fillStyle = "#8b96a8";
      ctx.font = `500 8px ${MONO}`;
      ctx.fillText(tag.split(",")[0], px + r + 2, py + 3);
    }
  }
  ctx.globalAlpha = 1;
}

// ---- EEG spectrogram ----
function drawSpectrogram(s) {
  const c = $("spectrogram");
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d1017";
  ctx.fillRect(0, 0, w, h);

  const bands = ["\u03B4 1-4", "\u03B8 4-8", "\u03B1 8-12", "\u03B2 12-30", "\u03B3 30-80"];
  const hist = s.LfpHistory || [];
  if (hist.length === 0) return;

  let max = 1e-9;
  for (const col of hist) for (const v of col) if (v > max) max = v;

  const labelW = 50;
  const bh = (h - 8) / 5;

  for (let col = 0; col < hist.length; col++) {
    const x = labelW + (col / Math.max(1, hist.length - 1)) * (w - labelW - 6);
    for (let b = 0; b < 5; b++) {
      const p = Math.log10(1 + 9 * (hist[col][b] / max));
      const y = 4 + b * bh;
      ctx.fillStyle = `hsla(${168 - b * 14}, 75%, ${Math.round(5 + p * 50)}%, 1)`;
      ctx.fillRect(x - 1, y, 3, bh - 2);
    }
  }

  ctx.font = `500 9px ${MONO}`;
  ctx.fillStyle = "#5a6474";
  for (let b = 0; b < 5; b++) ctx.fillText(bands[b], 4, 4 + b * bh + bh / 2 + 3);
}

// ---- circumplex ----
function drawCircumplex(s) {
  const c = $("circumplex");
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d1017";
  ctx.fillRect(0, 0, w, h);

  const cx = w / 2, cy = h / 2, r = Math.min(w, h) / 2 - 10;
  ctx.strokeStyle = "#1c222e";
  ctx.lineWidth = 1;
  ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(cx - r, cy); ctx.lineTo(cx + r, cy);
  ctx.moveTo(cx, cy - r); ctx.lineTo(cx, cy + r);
  ctx.stroke();

  ctx.font = `500 8px ${MONO}`;
  ctx.fillStyle = "#5a6474";
  ctx.fillText("high", cx + 4, 10);
  ctx.fillText("calm", cx + 4, cy - 4);

  const px = cx + s.Valence * (r - 12);
  const py = cy - (s.Arousal - 0.5) * 2 * (r - 12);
  const col = s.Valence > 0.1 ? "#3ddbb8" : s.Valence < -0.1 ? "#f07080" : "#f0b45c";
  ctx.fillStyle = col;
  ctx.shadowColor = col;
  ctx.shadowBlur = 8;
  ctx.beginPath(); ctx.arc(px, py, 6, 0, Math.PI * 2); ctx.fill();
  ctx.shadowBlur = 0;

  ctx.fillStyle = "#d1d9e6";
  ctx.font = `600 11px ${UI}`;
  ctx.fillText(s.Feeling, px + 10, py + 3);
}

// ---- sleep strip ----
function drawSleep(s) {
  const c = $("sleepstrip");
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d1017";
  ctx.fillRect(0, 0, w, h);

  const strip = s.SleepStrip || [];
  if (strip.length < 2) return;
  const tMin = strip[0][0], tMax = Math.max(strip[strip.length - 1][0], tMin + 1);
  const colors = { Wake: "#3ddbb8", Drowsy: "#f0b45c", NREM: "#5a6474", REM: "#a78bfa" };

  for (let i = 0; i < strip.length - 1; i++) {
    const [t0, st] = strip[i];
    const t1 = strip[i + 1][0];
    const x0 = ((t0 - tMin) / (tMax - tMin)) * w;
    const x1 = ((t1 - tMin) / (tMax - tMin)) * w;
    ctx.fillStyle = colors[st] || "#5a6474";
    ctx.fillRect(x0, h * 0.2, Math.max(2, x1 - x0), h * 0.6);
  }
}

// ---- modulators ----
function drawMods(s) {
  const wrap = $("mods");
  const names = ["dopamine", "serotonin", "acetylcholine", "noradrenaline", "oxytocin", "histamine", "melatonin"];
  const colors = ["#3ddbb8", "#a78bfa", "#f0b45c", "#f07080", "#e888b8", "#5ba8e0", "#7d8fa8"];
  wrap.innerHTML = "";
  names.forEach((n, i) => {
    const v = (s.Modulators && s.Modulators[i]) || 0;
    const el = document.createElement("div");
    el.className = "mod";
    el.innerHTML =
      `<div class="label"><span>${n}</span><span class="val">${v.toFixed(2)}</span></div>` +
      `<div class="bar"><i style="width:${Math.min(100, (v / 1.5) * 100)}%;background:${colors[i]}"></i></div>`;
    wrap.appendChild(el);
  });
}

// ---- dreams ----
function drawDreams(s) {
  const wrap = $("dreams");
  const dreams = s.Dreams || [];
  if (wrap.childElementCount === dreams.length) return;
  wrap.innerHTML = "";
  for (const d of dreams) {
    const div = document.createElement("div");
    if (d.includes("mock")) {
      div.className = "mock";
      div.textContent = "dream (mock)";
    } else {
      const img = document.createElement("img");
      img.src = "/dreams/" + d;
      div.appendChild(img);
    }
    wrap.appendChild(div);
  }
}

// ---- chat ----
function drawChat(s) {
  const log = $("chat");
  const msgs = s.Chat || [];
  const last = msgs.length ? msgs[msgs.length - 1] : "";
  if (log.childElementCount === msgs.length && log.lastElementChild && log.lastElementChild.dataset.raw === last) return;
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

// ---- chat input ----
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
  } catch (e) { console.warn(e); }
});

// ---- the brain reacts when you try to leave ----
window.addEventListener("beforeunload", async (ev) => {
  if (!lastSnapshot || lastSnapshot.SleepState === "NREM") return;
  try {
    // tell the brain you're leaving (it can't respond fast enough for the unload,
    // but the next session it will remember)
    fetch("/api/say", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text: "[the person is leaving]" }),
      keepalive: true,
    });
  } catch (e) { /* fire and forget */ }
  // show the confirmation dialog with the brain's plea
  ev.preventDefault();
  ev.returnValue = "";
});
let resizeTimer;
// ---- test voice button ----
async function testVoice() {
  try {
    const r = await fetch("/api/test-voice", { method: "POST" });
    if (r.ok) {
      const btn = document.getElementById("voice-test");
      btn.textContent = "🔊 speaking...";
      setTimeout(() => btn.textContent = "🔊 voice", 3000);
    } else {
      alert("Voice not available. Run with --voice and check Windows TTS voices.");
    }
  } catch (e) { alert("Could not test voice: " + e.message); }
}

window.addEventListener("resize", () => {
  clearTimeout(resizeTimer);
  resizeTimer = setTimeout(() => { if (lastSnap) render(lastSnap); }, 150);
});
