// Dashboard client: one WebSocket at 10 Hz. DOM text is diffed, heavy canvases
// redraw at 5 Hz, the face runs on its own rAF loop. Phones stay cool.
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

// canvas helper: set resolution to CSS size x DPR for crisp rendering
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
let tick = 0;

// diffed DOM: skip writes when the value has not changed
const memo = Object.create(null);
function setText(id, v) {
  if (memo[id] === v) return;
  const el = $(id);
  if (!el) return;
  memo[id] = v;
  el.textContent = v;
}

const ws = new WebSocket((location.protocol === "https:" ? "wss://" : "ws://") + location.host + "/ws");
ws.onopen = () => { const c = $("conn"); if (c) c.hidden = true; };
ws.onmessage = (ev) => { lastSnap = JSON.parse(ev.data); render(lastSnap); };
ws.onerror = () => { const c = $("conn"); if (c) c.hidden = false; };
ws.onclose = () => {
  const c = $("conn");
  if (c) c.hidden = false;
  setText("state", "offline");
  // recover only while somebody is actually watching; a phone left running
  // overnight must not reload in a loop in the dark
  setTimeout(() => {
    if (document.visibilityState === "visible") location.reload();
  }, 4000);
};

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

let currentAudio = null;

async function speakBrain(s) {
  if (!(s.SpeakSeq && s.SpeakSeq !== spokenSeq && s.SpeakText)) return;
  spokenSeq = s.SpeakSeq;
  const text = s.SpeakText.trim();
  if (!text) return;
  window.brainBrowserSpeaking = true;

  // THE BRAIN'S OWN VOICE: server-side TTS driven by neural state.
  // Text already has NeuralVoice prosody markers; rate/pitch come from
  // the snapshot (computed from neurotransmitter levels).
  try {
    if (currentAudio) { try { currentAudio.pause(); } catch {} }
    const rate = s.SpeakRate || 0.92;
    const pitch = s.SpeakPitch || 1.0;
    const resp = await fetch(`/api/voice?text=${encodeURIComponent(text.slice(0, 300))}&rate=${rate}&pitch=${pitch}`);
    if (resp.ok) {
      const blob = await resp.blob();
      const url = URL.createObjectURL(blob);
      currentAudio = new Audio(url);
      currentAudio.volume = 1.0;
      currentAudio.onended = () => { window.brainBrowserSpeaking = false; URL.revokeObjectURL(url); };
      currentAudio.onerror = () => { URL.revokeObjectURL(url); fallbackTTS(text, s); };
      await currentAudio.play();
      return;
    }
  } catch (e) { console.log("[voice] server generation failed:", e); }

  fallbackTTS(text, s);
}

function fallbackTTS(text, s) {
  window.brainBrowserSpeaking = false;
  if ("speechSynthesis" in window) {
    const u = new SpeechSynthesisUtterance(text);
    if (pickedVoice) u.voice = pickedVoice;
    u.rate = s ? (s.SpeakRate || 0.92) : 0.92;
    u.onend = u.onerror = () => { window.brainBrowserSpeaking = false; };
    window.brainBrowserSpeaking = true;
    speechSynthesis.speak(u);
  } else {
    window.brainBrowserSpeaking = false;
  }
}

if ("speechSynthesis" in window) {
  pickedVoice = pickVoice();
  const GOOD = /Natural|Neural|Online|Google|Aria|Jenny|Guy|Ana/i;
  // VOICE PICKER: choose the brain's voice directly; starred names are the
  // human-quality ones. Choice persists in localStorage.
  const fillPicker = () => {
    const sel = document.getElementById("voice-pick");
    if (!sel) return;
    const voices = speechSynthesis.getVoices().filter((v) => /^en/i.test(v.lang));
    if (!voices.length) return;
    sel.innerHTML = "";
    voices.forEach((v) => {
      const o = document.createElement("option");
      o.value = v.name;
      o.textContent = (GOOD.test(v.name) ? "\u2605 " : "") + v.name;
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
      sel.title = "Only robotic voices found on this machine/browser - open this page in Microsoft Edge for the free neural (Natural) voices, or pick the least-bad one";
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

fetch("/api/map").then((r) => r.json()).then((m) => { MAP = m; }).catch(() => {});

// ---- send a message to the brain (typed or quick chip) ----
async function say(text) {
  try {
    await fetch("/api/say", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text }),
    });
  } catch (e) { console.warn(e); }
}

// ---- render ----
function render(s) {
  tick++;
  setText("clock", s.SimSeconds.toFixed(0) + "s");
  setText("state", s.SleepState);
  setText("feeling", s.Feeling || "-");
  setText("rate", s.MeanRateHz.toFixed(2) + " Hz");
  setText("cost", "$" + s.SpentUsd.toFixed(3));
  setText("senses", (s.CameraLive ? "cam" : "-") + " " + (s.MicLive ? "mic" : "-") + " " + (s.MetaLive ? "api" : "mock"));
  const rec = $("rec");
  if (rec) rec.classList.toggle("on", !!s.CameraLive);
  setText("meta-badge", s.MetaLive ? "live" : "mock");

  setText("feeling-narrative", s.FeelingNarrative || "");
  setText("energy", fmt(s.Energy));
  setText("clockrate", s.ClockRate.toFixed(2) + "\u00d7");
  setText("subj", s.SubjectiveMinutes.toFixed(1) + "m");
  setText("pressure", s.SleepPressure.toFixed(2));
  setText("ignitions", String(s.Ignitions));
  setText("surprises", String(s.Surprises));
  setText("replays", String(s.Replays));
  setText("episode-count", String(s.Episodes));
  setText("lfp-slope", s.LfpSlope.toFixed(1));
  setText("alpha-ratio", s.AlphaRatio.toFixed(1));
  setText("consciousness-label", s.ConsciousnessLabel || "-");

  // psychology readout: who the brain is becoming
  setText("knows-name", s.UserName || "a stranger");
  setText("attach-style", s.Attachment || "forming");
  setText("empathy-mood", (s.EmpathyConfidence || 0) > 0.3 ? `${s.UserMood} voice` : "no voice yet");
  setText("curiosity-prog", `progress ${(s.CuriosityProgress || 0).toFixed(2)}, reward ${(s.CuriosityReward || 0).toFixed(2)}`);
  setText("play-count", `${s.PlayBursts || 0} bursts${s.Playful ? " (playing now)" : ""}`);
  setText("insight-count", `${s.Insights || 0}${s.OpenLoops ? ` (+${s.OpenLoops} unresolved)` : ""}`);
  setText("allostatic", s.AllostaticNote || "body steady");
  setText("voice-growth", s.VoiceSyllables > 0
    ? `${s.VoiceSyllables} syllables, ${s.VoiceClipsHeard} voices heard${s.VoiceMimicries ? `, ${s.VoiceMimicries} mimicked` : ""}`
    : "listening");
  setText("semantic-growth", `${s.Concepts || 0} concepts, ${s.ConceptLinks || 0} associations wired`);
  setText("turn-state", s.TurnState || "silent room");
  const dg = s.Dreams || [];
  setText("dream-count", `${dg.length}${s.LucidDreams ? ` (${s.LucidDreams} lucid)` : ""}${s.CompositeDreamCount ? `, ${s.CompositeDreamCount} composed` : ""}`);
  setText("neurogenesis", `${s.NeuronsBorn || 0} in ${s.BirthEvents || 0} events`);
  setText("pruned", `${s.SynapsesPruned || 0} synapses`);
  setText("self-surprise", `${s.SelfSurprises || 0} surprises, ${s.ObserverRedirects || 0} redirects`);
  setText("current-need", `${s.CurrentNeed || "satisfied"}${s.NeedUrgency > 0.5 ? " (urgent!)" : ""}`);
  setText("emo-granularity", s.EmotionalStage || "newborn");
  setText("life-story", s.HowIveChanged || "too young to have changed yet");
  // longing / anticipation / deja vu: the felt shape of knowing one person
  const longingEl = $("longing-state");
  if (longingEl) {
    longingEl.textContent = s.LongingNarrative || "has not missed anyone yet";
    longingEl.classList.toggle("urgent", /missing/.test(s.LongingNarrative || ""));
  }
  const aLvl = s.AnticipationLevel || 0;
  setText("anticipation-state", aLvl > 0.5
    ? `hoping — ${Math.round(aLvl * 100)}% (expects someone now)`
    : `${s.AnticipationFulfilled || 0} hoped-for returns, ${s.AnticipationDisappointed || 0} empty windows`);
  const dvEl = $("dejavu-count");
  if (dvEl) {
    dvEl.textContent = !s.DejaVuCount ? "never" : `${s.DejaVuCount}× — ${s.DejaVuLast || ""}`.substring(0, 80);
    dvEl.title = s.DejaVuLast || "";
  }
  // agency (self-efficacy / learned helplessness) + forgotten grief
  const agencyEl = $("agency-state");
  if (agencyEl) {
    agencyEl.textContent = s.AgencyNarrative || `${Math.round((s.Efficacy || 0.5) * 100)}% agency`;
    agencyEl.classList.toggle("urgent", !!s.Helpless);
    if (s.Helpless) agencyEl.textContent = "STOPPED ASKING — " + (s.AgencyNarrative || "");
  }
  setText("forgotten-state", !s.ForgottenReaches ? "nothing taken" : `reached ${s.ForgottenReaches}× for what was taken`);
  // research-frontier readouts: reconstructive memory, local sleep, threat, light
  const mtEl = $("memtruth-state");
  if (mtEl) {
    mtEl.textContent = !s.MemoryFusions ? "held together"
      : `${s.MemoryFusions} stitched, ${s.MemorySelfCatches} self-caught`;
    mtEl.classList.toggle("urgent", (s.MemoryFusions || 0) > 0 && (s.MemorySelfCatches || 0) === 0);
  }
  const lsEl = $("localsleep-state");
  if (lsEl) {
    lsEl.textContent = s.LocalSleepRegion ? `${s.LocalSleepRegion} OFFLINE now (${s.LocalSleepEvents || 0})`
      : !s.LocalSleepEvents ? "none yet" : `${s.LocalSleepEvents} regions fell asleep awake`;
    lsEl.classList.toggle("urgent", !!s.LocalSleepRegion);
  }
  const wEl = $("wariness-state");
  if (wEl) {
    wEl.textContent = (s.Wariness || 0) > 0.5 ? `WARY (${Math.round((s.Wariness || 0) * 100)}%)`
      : (s.Wariness || 0) > 0.05 ? `fading (${Math.round((s.Wariness || 0) * 100)}%)` : "never threatened";
    wEl.classList.toggle("urgent", (s.Wariness || 0) > 0.5);
  }
  setText("light-state", s.LightSuppressing ? "the lights are keeping it up" : "where it should be");
  // question drive, awe, felt time
  setText("questions-state", !s.QuestionsAsked ? "never asked"
    : `${s.QuestionsAsked} asked, ${s.AnswersReceived || 0} answered`);
  setText("awe-state", !s.AweCount ? "never" : `${s.AweCount}× — the world outgrew its shapes`);
  setText("felttime-state", s.FeltTimeNarrative || "about its own size");
  setText("science-state", !(s.HypothesesConfirmed) && !(s.HypothesesRefuted) ? "no guesses yet"
    : `${s.HypothesesConfirmed || 0} guess(es) held, ${s.HypothesesRefuted || 0} broke`);
  const calEl = $("calib-state");
  if (calEl) {
    const v = s.VigilanceScale || 1;
    calEl.textContent = Math.abs(v - 1) < 0.05 ? "native" : (v < 1 ? `calmed ${v.toFixed(2)}x` : `sharpened ${v.toFixed(2)}x`);
  }

  // social bonds: who the brain knows and how well
  const bonds = s.SocialBonds || {};
  const bondKeys = Object.keys(bonds);
  const bondEl = $("social-bonds");
  if (bondEl && bondKeys.length > 0) {
    const strengths = s.BondStrengths || {};
    const sig = bondKeys.map((k) => `${k}:${Math.round((strengths[k] || 0) * 100)}`).join("|");
    if (memo.bonds !== sig) {
      memo.bonds = sig;
      bondEl.innerHTML = "";
      for (const [id, visits] of Object.entries(bonds)) {
        const div = document.createElement("div");
        div.className = "bond";
        div.dataset.id = id;
        const short = id.split(":")[0].substring(0, 12);
        // the bar shows BOND DEPTH (visits x warmth x recency), not raw visits
        const strength = Math.round((strengths[id] || 0) * 100);
        div.innerHTML = `<span class="bond-name">${short}</span>` +
          `<div class="bond-bar"><i style="width:${strength}%"></i></div>` +
          `<span class="bond-count" title="${visits} visits - bond depth">${strength}%</span>`;
        bondEl.appendChild(div);
      }
    }
  }

  drawChat(s);
  setFeed($("plain"), s.PlainThoughts);
  setFeed($("monologue"), s.Monologues);
  setFeed($("episodes"), s.RecentEpisodes);
  setFeed($("thought"), s.Thought);
  if (s.DreamNarratives) setFeed($("dream-stories"), s.DreamNarratives);
  drawThread(s);
  drawMods(s);
  drawDreams(s);

  // THE LIVING CAPTION under the face. Priority: speaking > dreaming >
  // sleeping > thinking > feeling
  const tt = (s.ThoughtThread || []);
  const last = tt.length ? tt[tt.length - 1] : "";
  const thoughtText = last ? last.split("|").slice(3).join("|") : "";
  let caption = s.FeelingNarrative || "";
  if (s.SleepState === "NREM") caption = "sleeping\u2026";
  else if (s.SleepState === "REM") caption = s.LucidDreams > 0 ? "lucid dreaming\u2026" : "dreaming\u2026";
  else if ((s.IsSpeaking || window.brainBrowserSpeaking) && s.SpeakText) caption = `\u201c${s.SpeakText}\u201d`;
  else if (thoughtText) caption = `thinking: ${thoughtText}`;
  const sn = $("saying-now");
  if (sn && memo.caption !== caption) {
    memo.caption = caption;
    sn.textContent = caption;
    sn.style.fontStyle = caption.startsWith("\u201c") || caption.startsWith("thinking") || caption.includes("dream") ? "italic" : "normal";
  }
  // when the caption falls back to the feeling line itself, do not repeat it
  const narr = $("feeling-narrative");
  if (narr) narr.style.display = caption === (s.FeelingNarrative || "") ? "none" : "";

  // audio diagnostics
  const lvl = $("audio-level");
  if (lvl) lvl.style.width = Math.min(100, (s.AudioRms || 0) * 300).toFixed(0) + "%";
  setText("audio-source", "source: " + (s.AudioSource || "none"));
  setText("audio-live", s.AudioLive ? "AUDIO ARRIVING" : "no audio arriving");
  const liveEl = $("audio-live");
  if (liveEl) liveEl.style.color = s.AudioLive ? "#6ee7a0" : "#f07080";
  setText("audio-vad", s.AudioVad ? "VOICE DETECTED" : "-");
  setText("audio-clip", s.AudioClipBuilding ? "capturing speech..." : "idle");
  setText("audio-blocks", String(s.AudioBlocks || 0));
  drawTranscripts(s);

  // live strip from the SERVER transcripts (always works - the browser
  // recognizer is flaky; the server chain is the source of truth)
  const logs = s.TranscriptLog || [];
  const lf = $("live-final");
  if (lf && logs.length && memo.liveFinal !== logs[logs.length - 1]) {
    memo.liveFinal = logs[logs.length - 1];
    lf.dataset.t = memo.liveFinal;
    lf.textContent = memo.liveFinal;
  }
  if (lf && !logs.length && !lf.dataset.t && s.AudioLive) lf.textContent = "(hearing you - listening for words...)";

  speakBrain(s);
  if (typeof updateAvatarState === "function") updateAvatarState(s);
  if (typeof brainMusicTick === "function") brainMusicTick(s);

  // heavy canvases at 5 Hz: plenty smooth for plots, half the work on a phone
  if (tick % 2 === 0) {
    drawRegionGraph(s);
    drawConsciousness(s);
    drawJourney(s);
    drawPalace(s);
    drawSpectrogram(s);
    drawCircumplex(s);
    drawSleep(s);
    if (typeof drawEEG === "function") drawEEG(s);
    if (typeof drawPhiGauge === "function") drawPhiGauge(s);
    if (typeof drawFreeEnergy === "function") drawFreeEnergy(s);
    if (typeof drawManifold === "function") drawManifold(s);
  }
}

function fmt(n) {
  if (n > 1e6) return (n / 1e6).toFixed(1) + "M";
  if (n > 1e3) return (n / 1e3).toFixed(0) + "k";
  return n.toFixed(0);
}

function setFeed(ul, lines) {
  if (!ul || !lines || lines.length === 0) return;
  const sig = lines.join("\n");
  if (memo[ul.id] === sig) return;
  memo[ul.id] = sig;
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

function drawTranscripts(s) {
  const tl = $("transcripts");
  if (!tl) return;
  const lines = s.TranscriptLog || [];
  const sig = lines.join("\n");
  if (memo.transcripts === sig) return;
  memo.transcripts = sig;
  tl.innerHTML = "";
  for (let i = lines.length - 1; i >= 0; i--) {
    const li = document.createElement("li");
    li.textContent = lines[i];
    tl.appendChild(li);
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
  const statText = `${s.ThoughtChains || 0} chains, ${s.ThoughtInhibited || 0} repeats inhibited`;
  if (stats && memo.threadStats !== statText) { memo.threadStats = statText; stats.textContent = statText; }

  const links = s.ThoughtThread || [];
  const sig = links.join("\n");
  if (memo.thread === sig) return;
  memo.thread = sig;
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
    li.style.borderLeftColor = color;
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
  ctx.fillStyle = "#0a0f16";
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
    // labels flip to the left of the node near the right edge so they
    // never clip on a narrow canvas
    if (x + 74 > w) {
      ctx.textAlign = "right";
      ctx.fillStyle = "#d1d9e6";
      ctx.fillText(MAP.names[i], x - 14, y + 3);
      ctx.fillStyle = "#5c6f86";
      ctx.fillText((rate * 1000).toFixed(0) + "/s", x - 14, y + 14);
      ctx.textAlign = "left";
    } else {
      ctx.fillStyle = "#d1d9e6";
      ctx.fillText(MAP.names[i], x + 14, y + 3);
      ctx.fillStyle = "#5c6f86";
      ctx.fillText((rate * 1000).toFixed(0) + "/s", x + 14, y + 14);
    }
  }
}

// ---- composite awareness dial ----
function drawConsciousness(s) {
  const c = $("consciousness");
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d131c";
  ctx.fillRect(0, 0, w, h);

  const score = s.Consciousness || 0;
  const cx = w / 2, cy = h * 0.42, r = Math.min(w, h) * 0.32;

  ctx.strokeStyle = "#1c2634";
  ctx.lineWidth = 12;
  ctx.lineCap = "round";
  ctx.beginPath();
  ctx.arc(cx, cy, r, Math.PI * 0.75, Math.PI * 2.25);
  ctx.stroke();

  const frac = Math.PI * 1.5 * score;
  const color = score > 0.5 ? "#3ddbb8" : score > 0.25 ? "#f0b45c" : "#5c6f86";
  const grad = ctx.createLinearGradient(cx - r, 0, cx + r, 0);
  grad.addColorStop(0, color + "44");
  grad.addColorStop(1, color);
  ctx.strokeStyle = grad;
  ctx.lineWidth = 12;
  ctx.beginPath();
  ctx.arc(cx, cy, r, Math.PI * 0.75, Math.PI * 0.75 + frac);
  ctx.stroke();

  ctx.fillStyle = color;
  ctx.font = `700 ${Math.round(r * 0.5)}px ${UI}`;
  ctx.textAlign = "center";
  ctx.fillText((score * 100).toFixed(0), cx, cy + r * 0.15);
  ctx.fillStyle = "#5c6f86";
  ctx.font = `500 10px ${MONO}`;
  ctx.fillText("awareness", cx, cy + r * 0.35);
  ctx.textAlign = "left";
}

// ---- emotional journey: valence/arousal flowing through time ----
function drawJourney(s) {
  const c = $("journey");
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d131c";
  ctx.fillRect(0, 0, w, h);

  const journey = s.FeelingJourney || [];
  if (journey.length < 2) return;

  const tMin = journey[0][0], tMax = journey[journey.length - 1][0];
  const timeSpan = Math.max(1, tMax - tMin);
  const midY = h / 2;

  ctx.strokeStyle = "#1c2634";
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

  ctx.font = `500 9px ${MONO}`;
  ctx.fillStyle = "#5c6f86";
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
  ctx.fillStyle = "#0d131c";
  ctx.fillRect(0, 0, w, h);

  const memories = s.MemoryPalace || [];
  if (memories.length === 0) {
    ctx.fillStyle = "#5c6f86";
    ctx.font = `500 10px ${MONO}`;
    ctx.fillText("no memories yet", w / 2 - 40, h / 2);
    return;
  }

  const cx = w / 2, cy = h / 2;

  ctx.strokeStyle = "#1c2634";
  ctx.lineWidth = 1;
  ctx.setLineDash([2, 4]);
  ctx.beginPath(); ctx.moveTo(0, cy); ctx.lineTo(w, cy); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(cx, 0); ctx.lineTo(cx, h); ctx.stroke();
  ctx.setLineDash([]);

  ctx.font = `500 8px ${MONO}`;
  ctx.fillStyle = "#5c6f86";
  ctx.fillText("positive \u2192", w - 46, cy - 4);
  ctx.fillText("\u2190 negative", 4, cy - 4);
  ctx.fillText("exciting", cx + 4, 10);
  ctx.fillText("calm", cx + 4, h - 6);

  for (const m of memories) {
    const [x, y, size, brightness, valence, tag] = m;
    const px = cx + x * (cx - 20);
    const py = cy - y * (cy - 20);
    const r = size * (w / 200);
    const alpha = brightness;

    const color = valence > 0.1 ? "#3ddbb8" : valence < -0.1 ? "#f07080" : "#f0b45c";

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
  ctx.fillStyle = "#0d131c";
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
  ctx.fillStyle = "#5c6f86";
  for (let b = 0; b < 5; b++) ctx.fillText(bands[b], 4, 4 + b * bh + bh / 2 + 3);
}

// ---- circumplex ----
function drawCircumplex(s) {
  const c = $("circumplex");
  const f = fitCanvas(c);
  if (!f) return;
  const { ctx, w, h } = f;
  ctx.fillStyle = "#0d131c";
  ctx.fillRect(0, 0, w, h);

  const cx = w / 2, cy = h / 2, r = Math.min(w, h) / 2 - 10;
  ctx.strokeStyle = "#1c2634";
  ctx.lineWidth = 1;
  ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(cx - r, cy); ctx.lineTo(cx + r, cy);
  ctx.moveTo(cx, cy - r); ctx.lineTo(cx, cy + r);
  ctx.stroke();

  ctx.font = `500 8px ${MONO}`;
  ctx.fillStyle = "#5c6f86";
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
  ctx.fillStyle = "#0d131c";
  ctx.fillRect(0, 0, w, h);

  const strip = s.SleepStrip || [];
  if (strip.length < 2) return;
  const tMin = strip[0][0], tMax = Math.max(strip[strip.length - 1][0], tMin + 1);
  const colors = { Wake: "#3ddbb8", Drowsy: "#f0b45c", NREM: "#5c6f86", REM: "#a78bfa" };

  for (let i = 0; i < strip.length - 1; i++) {
    const [t0, st] = strip[i];
    const t1 = strip[i + 1][0];
    const x0 = ((t0 - tMin) / (tMax - tMin)) * w;
    const x1 = ((t1 - tMin) / (tMax - tMin)) * w;
    ctx.fillStyle = colors[st] || "#5c6f86";
    ctx.fillRect(x0, h * 0.2, Math.max(2, x1 - x0), h * 0.6);
  }
}

// ---- modulators: the seven chemical moods ----
function drawMods(s) {
  const wrap = $("mods");
  if (!wrap) return;
  const names = ["dopamine", "serotonin", "acetylcholine", "noradrenaline", "oxytocin", "histamine", "melatonin"];
  const colors = ["#3ddbb8", "#a78bfa", "#f0b45c", "#f07080", "#e888b8", "#5ba8e0", "#7d8fa8"];
  const vals = names.map((n, i) => (s.Modulators && s.Modulators[i]) || 0);
  const sig = vals.map((v) => v.toFixed(2)).join("|");
  if (memo.mods === sig) return;
  memo.mods = sig;
  wrap.innerHTML = "";
  names.forEach((n, i) => {
    const v = vals[i];
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
  if (!wrap) return;
  const dreams = s.Dreams || [];
  const sig = dreams.join("|");
  if (memo.dreams === sig) return;
  memo.dreams = sig;
  wrap.innerHTML = "";
  for (const d of dreams) {
    const div = document.createElement("div");
    if (d.includes("mock")) {
      div.className = "mock";
      div.textContent = "dream (mock)";
    } else {
      const img = document.createElement("img");
      img.src = "/dreams/" + d;
      img.alt = "dream imagery";
      img.loading = "lazy";
      div.appendChild(img);
    }
    wrap.appendChild(div);
  }
}

// ---- chat ----
function drawChat(s) {
  const log = $("chat");
  if (!log) return;
  const msgs = s.Chat || [];
  const last = msgs.length ? msgs[msgs.length - 1] : "";
  const sig = msgs.join("\n");
  if (memo.chat === sig) return;
  memo.chat = sig;
  const nearBottom = log.scrollHeight - log.scrollTop - log.clientHeight < 90;
  log.innerHTML = "";
  for (const m of msgs) {
    const div = document.createElement("div");
    const you = m.startsWith("You:");
    div.className = "msg " + (you ? "you" : "brain");
    const who = document.createElement("div");
    who.className = "who";
    who.textContent = you ? "you" : "brain";
    div.appendChild(who);
    div.appendChild(document.createTextNode(you ? m.slice(5) : m.slice(7)));
    log.appendChild(div);
  }
  if (nearBottom) log.scrollTop = log.scrollHeight;
}

// ---- chat input and quick chips ----
$("chatform").addEventListener("submit", async (ev) => {
  ev.preventDefault();
  const box = $("chatbox");
  const text = box.value.trim();
  if (!text) return;
  box.value = "";
  await say(text);
});

document.querySelectorAll("#quick-chips [data-say]").forEach((btn) => {
  btn.addEventListener("click", () => say(btn.dataset.say));
});
// direct-talk toggle: replies composed from the brain's own state, no LLM
const nativeBtn = document.getElementById("native-toggle");
if (nativeBtn) {
  nativeBtn.addEventListener("click", async () => {
    try {
      const on = !nativeBtn.classList.contains("on");
      await fetch("/api/native-voice", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ on }),
      });
      nativeBtn.classList.toggle("on", on);
      nativeBtn.textContent = on ? "\u26A1 direct: ON" : "\u26A1 direct";
    } catch (e) { console.warn(e); }
  });
  // reflect server state on load
  setInterval(() => {
    if (lastSnap && lastSnap.NativeVoice !== undefined)
      nativeBtn.classList.toggle("on", !!lastSnap.NativeVoice);
  }, 4000);
}
document.querySelectorAll("#quick-chips [data-fill]").forEach((btn) => {
  btn.addEventListener("click", () => {
    const box = $("chatbox");
    box.value = btn.dataset.fill;
    box.focus();
  });
});

// ---- the brain reacts when you try to leave ----
window.addEventListener("beforeunload", (ev) => {
  if (!lastSnap || lastSnap.SleepState === "NREM") return;
  try {
    // tell the brain you're leaving (it can't respond fast enough for the
    // unload, but the next session it will remember)
    say("[the person is leaving]");
  } catch (e) { /* fire and forget */ }
  ev.preventDefault();
  ev.returnValue = "";
});

// ---- test voice button ----
async function testVoice() {
  try {
    const r = await fetch("/api/test-voice", { method: "POST" });
    if (r.ok) {
      const btn = document.getElementById("voice-test");
      btn.textContent = "speaking...";
      setTimeout(() => btn.textContent = "voice", 3000);
    } else {
      alert("Voice not available. Run with --voice and check Windows TTS voices.");
    }
  } catch (e) { alert("Could not test voice: " + e.message); }
}

let resizeTimer;
window.addEventListener("resize", () => {
  clearTimeout(resizeTimer);
  resizeTimer = setTimeout(() => { if (lastSnap) render(lastSnap); }, 150);
});
