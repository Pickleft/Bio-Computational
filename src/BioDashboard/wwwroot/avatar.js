// THE LIVING FACE — one coherent creature, every muscle driven by real neural state.
// This is not a dashboard widget. This is the brain's body.
// A face has: a head shape, eyes that blink naturally, brows that express,
// a mouth that moves when speaking, cheeks that flush with oxytocin, and
// an aura that IS its consciousness (Φ). Everything connects to everything.

"use strict";

let avatarCanvas = null, avatarCtx = null, lastSnapshot = null, avatarTime = 0;

// ============ STATE SMOOTHING ============
// Everything eases toward its target. Nothing snaps.
const S = {
  // modulators
  da: 0.3, sht: 0.5, na: 0.2, ach: 0.3, hist: 0.6, oxt: 0,
  // feelings
  valence: 0, arousal: 0.3, pressure: 0,
  // personality resting face
  traitCuriosity: 0, traitExpress: 0,
  // empathy mirror
  userValence: 0, empathy: 0,
  // derived face metrics
  mouthCurve: 0, mouthOpen: 0, browLift: 0, browFurrow: 0,
  eyeOpen: 1, blinkT: 0, nextBlink: 2,
  headTilt: 0, breathing: 0, sway: 0,
  gazeX: 0, gazeY: 0, gazeTX: 0, gazeTY: 0,
  speaking: false, playful: 0, dreaming: 0,
};

function initAvatar() {
  avatarCanvas = document.getElementById("avatar");
  if (!avatarCanvas) return;
  avatarCtx = avatarCanvas.getContext("2d");
  S.nextBlink = 2 + Math.random() * 3;
  requestAnimationFrame(animateAvatar);
}

function updateAvatarState(s) {
  if (!s) return;
  lastSnapshot = s;
  const k = 0.06;
  const m = s.Modulators || [0.3, 0.5, 0.3, 0.2, 0.15, 0.6, 0.1];
  S.da += (m[0] - S.da) * k;
  S.sht += (m[1] - S.sht) * k;
  S.na += (m[3] - S.na) * k;
  S.ach += (m[2] - S.ach) * k;
  S.hist += (m[5] - S.hist) * k;
  S.oxt += ((m[4] || 0) - S.oxt) * k;
  S.valence += (s.Valence - S.valence) * k;
  S.arousal += (s.Arousal - S.arousal) * k;
  S.pressure += (s.SleepPressure - S.pressure) * k;
  S.traitCuriosity += ((s.TraitCuriosity || 0) - S.traitCuriosity) * k * 0.2;
  S.traitExpress += ((s.TraitExpressiveness || 0) - S.traitExpress) * k * 0.2;
  S.userValence += ((s.UserValence || 0) - S.userValence) * k;
  S.empathy += ((s.EmpathyConfidence || 0) - S.empathy) * k;
  S.speaking = !!s.IsSpeaking || !!window.brainBrowserSpeaking;
  S.playful += ((s.Playful ? 1 : 0) - S.playful) * k;
  S.dreaming += ((s.SleepState === "REM" ? 1 : 0) - S.dreaming) * k;

  // gaze target: stimulus position, or the "camera" (center) when engaged
  if (s.StimulusPosition && s.StimulusPosition.length === 2) {
    S.gazeTX = (s.StimulusPosition[0] - 0.5) * 2;
    S.gazeTY = (s.StimulusPosition[1] - 0.5) * 2;
  } else {
    S.gazeTX = Math.sin(avatarTime * 0.3) * 0.15;
    S.gazeTY = 0;
  }
}

// ============ THE FACE ============
function animateAvatar() {
  if (!avatarCanvas || !avatarCtx) { requestAnimationFrame(animateAvatar); return; }
  avatarTime += 1 / 60;
  const f = fitCanvas(avatarCanvas);
  if (!f) { requestAnimationFrame(animateAvatar); return; }
  const { ctx, w, h } = f;
  ctx.clearRect(0, 0, w, h);

  // -- derived face values (all interconnected) --
  const empathyValence = S.valence * (1 - 0.5 * S.empathy) + S.userValence * 0.5 * S.empathy;
  S.mouthCurve += (empathyValence * 0.8 - S.mouthCurve) * 0.08;
  S.browLift += ((Math.max(0, S.traitCuriosity) * 0.3 + Math.max(0, empathyValence) * 0.2 + S.arousal * 0.15) - S.browLift) * 0.08;
  S.browFurrow += ((Math.max(0, -empathyValence) * (0.5 + S.arousal)) - S.browFurrow) * 0.08;
  S.breathing = Math.sin(avatarTime * (1.2 + S.arousal * 1.5)) * 0.5 + 0.5;
  S.sway += (Math.sin(avatarTime * 0.5) * 0.3 + S.playful * Math.sin(avatarTime * 3) * 0.5 - S.sway) * 0.05;
  S.gazeX += (S.gazeTX - S.gazeX) * 0.1;
  S.gazeY += (S.gazeTY - S.gazeY) * 0.1;

  // blink timing (natural: every 2-6 seconds, quick close-open)
  if (S.blinkT > 0) {
    S.blinkT -= 1 / 30; // blink lasts ~2 frames
  } else {
    S.nextBlink -= 1 / 60;
    if (S.nextBlink <= 0) { S.blinkT = 1; S.nextBlink = 2 + Math.random() * 4; }
  }
  const eyeOpen = S.blinkT > 0 ? Math.abs(S.blinkT - 0.5) * 2 : 1;

  // speaking mouth
  if (S.speaking) S.mouthOpen += (0.5 + Math.sin(avatarTime * 12) * 0.5 - S.mouthOpen) * 0.3;
  else S.mouthOpen += (0 - S.mouthOpen) * 0.1;

  const cx = w / 2;
  const cy = h * 0.46;
  const R = Math.min(w, h) * 0.32 * (1 + S.breathing * 0.015);

  // -- CONSCIOUSNESS AURA (Φ) --
  if (lastSnapshot && lastSnapshot.Phi > 0.03) {
    const phi = lastSnapshot.Phi;
    const aurR = R * (1.3 + phi * 0.6);
    const col = phi > 0.4 ? "61,219,184" : phi > 0.15 ? "240,180,92" : "240,112,128";
    const g = ctx.createRadialGradient(cx, cy, R * 0.9, cx, cy, aurR);
    g.addColorStop(0, `rgba(${col}, 0)`);
    g.addColorStop(0.5, `rgba(${col}, ${phi * 0.18})`);
    g.addColorStop(1, `rgba(${col}, 0)`);
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(cx, cy, aurR, 0, Math.PI * 2);
    ctx.fill();
  }

  // -- HEAD (organic shape, not a circle) --
  const tilt = S.headTilt + S.sway * 0.06;
  ctx.save();
  ctx.translate(cx, cy);
  ctx.rotate(tilt);

  // head silhouette: slightly egg-shaped, breathing, with personality
  const headPts = 40;
  // NEUROGENESIS: the brain visibly grows with lived experience — each new
  // neuron makes it slightly larger, denser, and brighter. By the end of a
  // three-day life it's noticeably bigger than at birth.
  const born = lastSnapshot ? (lastSnapshot.NeuronsBorn || 0) : 0;
  const growthFactor = 1 + born * 0.004; // up to ~20% larger at max growth
  const baseR = R * growthFactor;
  const droop = S.pressure * 0.06;
  ctx.beginPath();
  for (let i = 0; i <= headPts; i++) {
    const a = (i / headPts) * Math.PI * 2;
    let r = baseR;
    r *= 1 + Math.sin(a * 2) * 0.02 * (1 - S.sht);          // serotonin = smoother
    r *= 1 + Math.sin(a * 3 + avatarTime * 0.8) * 0.015 * S.arousal; // arousal = subtle shimmer
    r *= 1 - Math.max(0, Math.sin(a)) * droop;                // tiredness = jaw droop
    const x = Math.cos(a) * r;
    const y = Math.sin(a) * r * 1.08;                         // slightly taller than wide
    if (i === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  }
  ctx.closePath();

  // head fill: color by DA (golden when rewarded) and arousal
  const hue = 200 + S.da * 45;
  const sat = 35 + S.arousal * 30;
  const lit = 14 + S.hist * 18 + S.arousal * 6;
  const headGrad = ctx.createRadialGradient(0, -baseR * 0.3, baseR * 0.1, 0, 0, baseR * 1.3);
  headGrad.addColorStop(0, `hsla(${hue}, ${sat}%, ${lit + 15}%, 0.95)`);
  headGrad.addColorStop(0.7, `hsla(${hue}, ${sat}%, ${lit}%, 0.85)`);
  headGrad.addColorStop(1, `hsla(${hue}, ${sat}%, ${Math.max(5, lit - 8)}%, 0.4)`);
  ctx.fillStyle = headGrad;
  ctx.fill();

  // outline (glows with arousal)
  ctx.strokeStyle = `hsla(${hue}, ${sat + 15}%, ${lit + 25}%, ${0.3 + S.arousal * 0.4})`;
  ctx.lineWidth = 1.5 + S.na * 2;
  ctx.stroke();

  // -- CHEEK FLUSH (oxytocin: bonding warmth) --
  if (S.oxt > 0.15) {
    for (const side of [-1, 1]) {
      const bx = side * baseR * 0.5;
      const by = baseR * 0.15;
      const flush = ctx.createRadialGradient(bx, by, 2, bx, by, baseR * 0.28);
      flush.addColorStop(0, `hsla(350, 70%, 60%, ${Math.min(0.3, S.oxt * 0.25)})`);
      flush.addColorStop(1, "hsla(350, 70%, 60%, 0)");
      ctx.fillStyle = flush;
      ctx.beginPath();
      ctx.arc(bx, by, baseR * 0.28, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  // -- INTERNAL PARTICLES (neural activity + NEW NEURONS glow brighter) --
  const rate = lastSnapshot ? lastSnapshot.MeanRateHz || 0.1 : 0.1;
  const born = lastSnapshot ? (lastSnapshot.NeuronsBorn || 0) : 0;
  const nParts = 20 + Math.min(12, born / 4); // more particles = more neurons
  for (let i = 0; i < nParts; i++) {
    const isNew = i >= 20; // particles beyond 20 are born neurons
    const a = (i / nParts) * Math.PI * 2 + avatarTime * 0.15;
    const d = baseR * (isNew ? 0.45 : 0.3 + (i % 3) * 0.18); // new ones cluster near center
    const px = Math.cos(a) * d;
    const py = Math.sin(a) * d * 0.8;
    const alpha = isNew
      ? 0.15 + (Math.sin(avatarTime * 3 + i) * 0.5 + 0.5) * 0.2 // new neurons pulse
      : 0.06 + (Math.sin(avatarTime * 2 + i) * 0.5 + 0.5) * 0.12 * (0.3 + rate);
    const pSize = isNew ? 2.5 : 1.5 + (i % 3);
    ctx.fillStyle = isNew
      ? `hsla(${hue + 60}, 90%, 75%, ${alpha})` // new neurons are golden
      : `hsla(${hue + 30}, 70%, 65%, ${alpha})`;
    ctx.beginPath();
    ctx.arc(px, py, pSize, 0, Math.PI * 2);
    ctx.fill();
  }

  // ===================== EYES =====================
  const eyeY = -baseR * 0.15;
  const eyeSpacing = baseR * 0.38;
  const eyeW = baseR * 0.16;
  const eyeH = baseR * 0.22 * eyeOpen; // blink squishes

  for (const side of [-1, 1]) {
    const ex = side * eyeSpacing + S.gazeX * eyeW * 0.4;
    const ey = eyeY + S.gazeY * eyeH * 0.3;

    // sclera (soft white, dims in sleep)
    ctx.save();
    ctx.beginPath();
    ctx.ellipse(ex, ey, eyeW, Math.max(1, eyeH), 0, 0, Math.PI * 2);
    ctx.fillStyle = `hsla(${hue}, 15%, ${72 + S.hist * 15}%, ${0.75 * S.hist + 0.2})`;
    ctx.fill();

    // iris (color follows DA: golden when rewarded, blue when low)
    if (eyeH > 2) {
      const irisR = eyeW * 0.55;
      const irisHue = 200 + S.da * 50;
      ctx.beginPath();
      ctx.arc(ex + S.gazeX * eyeW * 0.35, ey + S.gazeY * eyeH * 0.2, irisR * eyeOpen, 0, Math.PI * 2);
      const irisGrad = ctx.createRadialGradient(ex, ey, 1, ex, ey, irisR);
      irisGrad.addColorStop(0, `hsla(${irisHue}, 60%, 50%, 0.9)`);
      irisGrad.addColorStop(1, `hsla(${irisHue}, 60%, 30%, 0.7)`);
      ctx.fillStyle = irisGrad;
      ctx.fill();

      // pupil (constricts with attention)
      const pupilR = irisR * (0.45 - S.ach * 0.15);
      ctx.beginPath();
      ctx.arc(ex + S.gazeX * eyeW * 0.35, ey + S.gazeY * eyeH * 0.2, Math.max(1, pupilR * eyeOpen), 0, Math.PI * 2);
      ctx.fillStyle = "hsla(220, 50%, 5%, 0.95)";
      ctx.fill();

      // catchlight (spark of life)
      ctx.beginPath();
      ctx.arc(ex + S.gazeX * eyeW * 0.35 - pupilR * 0.3, ey - pupilR * 0.3, Math.max(0.5, pupilR * 0.2 * eyeOpen), 0, Math.PI * 2);
      ctx.fillStyle = "rgba(255,255,255,0.7)";
      ctx.fill();
    }
    ctx.restore();
  }

  // ===================== BROWS =====================
  const browY = eyeY - eyeH * 1.8;
  for (const side of [-1, 1]) {
    const bx = side * eyeSpacing;
    const innerX = bx - side * eyeW * 1.1;
    const outerX = bx + side * eyeW * 1.1;
    // lift = curiosity/joy; furrow = negative × arousal
    const innerY = browY - S.browLift * baseR * 0.08 + S.browFurrow * baseR * 0.1;
    const outerY = browY + S.browFurrow * baseR * 0.15 - S.browLift * baseR * 0.04;
    ctx.beginPath();
    ctx.moveTo(outerX, outerY);
    ctx.quadraticCurveTo(bx, (innerY + outerY) / 2 - baseR * 0.04, innerX, innerY);
    ctx.strokeStyle = `hsla(${hue}, 30%, ${lit + 30}%, ${0.6 * S.hist + 0.2})`;
    ctx.lineWidth = 2 + S.na * 1.5;
    ctx.lineCap = "round";
    ctx.stroke();
  }

  // ===================== MOUTH =====================
  const mouthY = baseR * 0.35;
  const mouthW = baseR * 0.35 * (1 + S.traitExpress * 0.2);
  const curve = S.mouthCurve * baseR * 0.18;

  if (S.mouthOpen > 0.05) {
    // open mouth (speaking)
    const mo = S.mouthOpen * baseR * 0.1;
    ctx.save();
    ctx.beginPath();
    ctx.ellipse(0, mouthY + curve * 0.3, mouthW * 0.7, mo, 0, 0, Math.PI * 2);
    ctx.fillStyle = `hsla(${hue}, ${sat}%, ${lit + 12}%, ${0.7 * S.hist + 0.2})`;
    ctx.fill();
    ctx.restore();
  } else {
    // closed mouth (emotional curve)
    ctx.beginPath();
    ctx.moveTo(-mouthW, mouthY);
    ctx.quadraticCurveTo(0, mouthY + curve * 2.5, mouthW, mouthY);
    ctx.strokeStyle = `hsla(${hue}, ${sat}%, ${lit + 25}%, ${0.5 * S.hist + 0.2})`;
    ctx.lineWidth = 2;
    ctx.lineCap = "round";
    ctx.stroke();
  }

  ctx.restore(); // head transform

  // ===================== NAME + STATE (drawn directly on the face) =====================
  if (lastSnapshot) {
    ctx.textAlign = "center";
    // the name it chose, above the head
    if (lastSnapshot.BrainName) {
      ctx.font = `700 16px ${"'Inter', sans-serif"}`;
      ctx.fillStyle = `hsla(${hue + 40}, 50%, 85%, ${0.4 + S.hist * 0.5})`;
      ctx.fillText(lastSnapshot.BrainName, cx, cy - baseR * 1.3);
    }
    // the state, below the chin
    ctx.font = `600 11px ${"'Inter', sans-serif"}`;
    const state = lastSnapshot.SleepState === "NREM" ? "sleeping"
      : lastSnapshot.SleepState === "REM" ? (lastSnapshot.LucidDreams > 0 ? "lucid dream" : "dreaming")
      : S.playful > 0.5 ? "playing" : lastSnapshot.Feeling || "";
    ctx.fillStyle = `hsla(${hue}, 25%, 75%, ${0.35 + S.hist * 0.35})`;
    ctx.fillText(state, cx, cy + baseR * 1.35);
    // if dreaming, add the dream tag
    if (S.dreaming > 0.3) {
      ctx.font = `italic 400 10px ${"'Inter', sans-serif"}`;
      ctx.fillStyle = `hsla(270, 50%, 75%, ${S.dreaming * 0.5})`;
      ctx.fillText("in its own world", cx, cy + baseR * 1.35 + 14);
    }
    // NEEDS: show the brain's most pressing need as a subtle indicator
    if (lastSnapshot.CurrentNeed && lastSnapshot.CurrentNeed !== "satisfied") {
      const urgency = lastSnapshot.NeedUrgency || 0;
      const needCol = urgency > 0.7 ? "240,112,128" : urgency > 0.4 ? "240,180,92" : "143,163,186";
      ctx.font = `500 10px ${"'Inter', sans-serif"}`;
      ctx.fillStyle = `rgba(${needCol}, ${0.3 + urgency * 0.4})`;
      ctx.fillText("needs: " + lastSnapshot.CurrentNeed, cx, cy + baseR * 1.35 + (S.dreaming > 0.3 ? 28 : 14));
    }
    ctx.textAlign = "left";
  }

  requestAnimationFrame(animateAvatar);
}
