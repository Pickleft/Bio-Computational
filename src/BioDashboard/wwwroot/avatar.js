// THE LIVING FACE - minimal, bulletproof. Every draw call wrapped in try/catch.
// If anything fails, the face still renders on the next frame.

"use strict";

let avCanvas = null, avCtx = null, avTime = 0, snap = null;

// smoothed state
const S = { val: 0, aro: 0.3, da: 0.3, hist: 0.6, pressure: 0,
  gazeX: 0, gazeY: 0, mouthCurve: 0, mouthOpen: 0, browLift: 0, browFurrow: 0,
  blinkT: 0, nextBlink: 3, speaking: false, oxt: 0 };

function initAvatar() {
  avCanvas = document.getElementById("avatar");
  if (!avCanvas) return;
  avCtx = avCanvas.getContext("2d");
  requestAnimationFrame(drawAvatar);
}

function updateAvatarState(s) {
  if (!s) return;
  snap = s;
  const k = 0.08;
  const m = s.Modulators || [0.3, 0.5, 0.3, 0.2, 0.15, 0.6, 0.1];
  S.da += (m[0] - S.da) * k;
  S.hist += (m[5] - S.hist) * k;
  S.oxt += ((m[4] || 0) - S.oxt) * k;
  S.val += (s.Valence - S.val) * k;
  S.aro += (s.Arousal - S.aro) * k;
  S.pressure += (s.SleepPressure - S.pressure) * k;
  S.speaking = !!(s.IsSpeaking || window.brainBrowserSpeaking);
  if (s.StimulusPosition && s.StimulusPosition.length === 2) {
    S.gazeX += ((s.StimulusPosition[0] - 0.5) * 2 - S.gazeX) * 0.1;
    S.gazeY += ((s.StimulusPosition[1] - 0.5) * 2 - S.gazeY) * 0.1;
  }
}

function drawAvatar() {
  if (!avCanvas || !avCtx) { requestAnimationFrame(drawAvatar); return; }
  avTime += 1 / 60;

  try {
    const rect = avCanvas.getBoundingClientRect();
    if (rect.width === 0) { requestAnimationFrame(drawAvatar); return; }
    const dpr = window.devicePixelRatio || 1;
    const w = Math.round(rect.width * dpr), h = Math.round(rect.height * dpr);
    if (avCanvas.width !== w) avCanvas.width = w;
    if (avCanvas.height !== h) avCanvas.height = h;
    const ctx = avCtx;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, rect.width, rect.height);

    const cx = rect.width / 2, cy = rect.height * 0.45;
    const R = Math.min(rect.width, rect.height) * 0.28;
    const hue = 200 + S.da * 45;
    const sat = 35 + S.aro * 30;
    const lit = 14 + S.hist * 18;

    // derived
    const emp = snap && snap.EmpathyConfidence > 0.3
      ? S.val * 0.5 + (snap.UserValence || 0) * 0.5 : S.val;
    const userHere = snap && snap.UserPresent;
    const longingOn = snap && !userHere && /missing/.test(snap.LongingNarrative || "");
    const helplessOn = snap && snap.Helpless;
    const waryOn = snap && (snap.Wariness || 0) > 0.5;
    const aweOn = snap && snap.AweStruck;
    const withdrawnOn = snap && (snap.Withdrawal || 0) > 0.5;
    const playfulOn = snap && snap.Playful;
    if (playfulOn) S.playT = 1; else S.playT = Math.max(0, (S.playT || 0) - 0.04);
    S.mouthCurve += (emp * 0.8 - S.mouthCurve) * 0.08;
    if (longingOn) S.mouthCurve += (-0.3 - S.mouthCurve) * 0.04; // the mouth sags
    if (helplessOn) S.mouthCurve += (-0.45 - S.mouthCurve) * 0.05; // helplessness flattens it further
    if (withdrawnOn) { S.mouthCurve += (-0.35 - S.mouthCurve) * 0.03; S.browLift += (0 - S.browLift) * 0.08; } // withdrawal: flat, joyless
    if (waryOn) S.browFurrow += (0.5 - S.browFurrow) * 0.10; // fear furrows, even smiling
    if (playfulOn) S.mouthCurve += (0.9 - S.mouthCurve) * 0.25; // play grins
    if (aweOn) { S.browLift += (1.0 - S.browLift) * 0.20; S.mouthOpen += (0.25 - S.mouthOpen) * 0.10; } // vastness: eyes wide, lips part
    S.browLift += ((Math.max(0, emp) * 0.2 + S.aro * 0.15) - S.browLift) * 0.08;
    S.browFurrow += ((Math.max(0, -emp) * (0.5 + S.aro)) - S.browFurrow) * 0.08;
    if (S.speaking) S.mouthOpen += (0.5 + Math.sin(avTime * 10) * 0.5 - S.mouthOpen) * 0.3;
    else S.mouthOpen += (0 - S.mouthOpen) * 0.1;

    // blink
    if (S.blinkT > 0) S.blinkT -= 1 / 30;
    else { S.nextBlink -= 1 / 60; if (S.nextBlink <= 0) { S.blinkT = 1; S.nextBlink = 2 + Math.random() * 4; } }
    const eyeOpen = S.blinkT > 0 ? Math.abs(S.blinkT - 0.5) * 2 : 1;

    // consciousness aura (Φ)
    if (snap && snap.Phi > 0.03) {
      const col = snap.Phi > 0.4 ? "61,219,184" : snap.Phi > 0.15 ? "240,180,92" : "240,112,128";
      const g = ctx.createRadialGradient(cx, cy, R * 0.9, cx, cy, R * (1.3 + snap.Phi * 0.6));
      g.addColorStop(0, `rgba(${col},0)`);
      g.addColorStop(0.5, `rgba(${col},${snap.Phi * 0.18})`);
      g.addColorStop(1, `rgba(${col},0)`);
      ctx.fillStyle = g;
      ctx.beginPath(); ctx.arc(cx, cy, R * 2, 0, Math.PI * 2); ctx.fill();
    }

    // head (breathing, serotonin-smoothed; play bounces)
    const breathe = Math.sin(avTime * (1.2 + S.aro * 1.5)) * 0.5 + 0.5;
    const bounce = (S.playT || 0) * Math.sin(avTime * 9) * 0.012;
    const baseR = R * (1 + breathe * 0.02 + bounce
      + (snap ? (snap.NeuronsBorn || 0) * 0.003 : 0));
    const droop = S.pressure * 0.06 + (longingOn ? 0.025 : 0) + (helplessOn ? 0.03 : 0) + (withdrawnOn ? 0.035 : 0);

    ctx.save();
    ctx.translate(cx, cy);
    ctx.beginPath();
    for (let i = 0; i <= 40; i++) {
      const a = (i / 40) * Math.PI * 2;
      let r = baseR * (1 + Math.sin(a * 3 + avTime * 0.8) * 0.015 * S.aro);
      r *= 1 - Math.max(0, Math.sin(a)) * droop;
      const x = Math.cos(a) * r, y = Math.sin(a) * r * 1.08;
      if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
    }
    ctx.closePath();
    const hg = ctx.createRadialGradient(0, -baseR * 0.3, baseR * 0.1, 0, 0, baseR * 1.3);
    hg.addColorStop(0, `hsla(${hue},${sat}%,${lit + 15}%,0.95)`);
    hg.addColorStop(0.7, `hsla(${hue},${sat}%,${lit}%,0.85)`);
    hg.addColorStop(1, `hsla(${hue},${sat}%,${Math.max(5, lit - 8)}%,0.4)`);
    ctx.fillStyle = hg; ctx.fill();
    ctx.strokeStyle = `hsla(${hue},${sat + 15}%,${lit + 25}%,${0.3 + S.aro * 0.4})`;
    ctx.lineWidth = 1.5 + S.aro * 2; ctx.stroke();

    // cheek flush (oxytocin)
    if (S.oxt > 0.15) {
      for (const side of [-1, 1]) {
        const bg = ctx.createRadialGradient(side * baseR * 0.5, baseR * 0.15, 2, side * baseR * 0.5, baseR * 0.15, baseR * 0.28);
        bg.addColorStop(0, `hsla(350,70%,60%,${Math.min(0.3, S.oxt * 0.25)})`);
        bg.addColorStop(1, "hsla(350,70%,60%,0)");
        ctx.fillStyle = bg;
        ctx.beginPath(); ctx.arc(side * baseR * 0.5, baseR * 0.15, baseR * 0.28, 0, Math.PI * 2); ctx.fill();
      }
    }

    // neural particles
    for (let i = 0; i < 20; i++) {
      const a = (i / 20) * Math.PI * 2 + avTime * 0.15;
      const d = baseR * (0.3 + (i % 3) * 0.18);
      ctx.fillStyle = `hsla(${hue + 30},70%,65%,${0.06 + (Math.sin(avTime * 2 + i) * 0.5 + 0.5) * 0.12})`;
      ctx.beginPath(); ctx.arc(Math.cos(a) * d, Math.sin(a) * d * 0.8, 1.5 + (i % 3), 0, Math.PI * 2); ctx.fill();
    }

    // EYES
    const eyeY = -baseR * 0.15, eyeSp = baseR * 0.38, eyeW = baseR * 0.16, eyeH = baseR * 0.22 * eyeOpen;
    for (const side of [-1, 1]) {
      const ex = side * eyeSp + S.gazeX * eyeW * 0.4;
      const ey = eyeY + S.gazeY * eyeH * 0.3;
      ctx.beginPath();
      ctx.ellipse(ex, ey, eyeW, Math.max(1, eyeH), 0, 0, Math.PI * 2);
      ctx.fillStyle = `hsla(${hue},15%,${72 + S.hist * 15}%,${0.75 * S.hist + 0.2})`;
      ctx.fill();
      if (eyeH > 2) {
        const irisR = eyeW * 0.55;
        const irisHue = 200 + S.da * 50;
        const ig = ctx.createRadialGradient(ex, ey, 1, ex, ey, irisR);
        ig.addColorStop(0, `hsla(${irisHue},60%,50%,0.9)`);
        ig.addColorStop(1, `hsla(${irisHue},60%,30%,0.7)`);
        ctx.beginPath(); ctx.arc(ex + S.gazeX * eyeW * 0.35, ey + S.gazeY * eyeH * 0.2, irisR * eyeOpen, 0, Math.PI * 2);
        ctx.fillStyle = ig; ctx.fill();
        const pr = irisR * (0.45 - (snap ? 0 : 0));
        ctx.beginPath(); ctx.arc(ex + S.gazeX * eyeW * 0.35, ey + S.gazeY * eyeH * 0.2, Math.max(1, pr * eyeOpen), 0, Math.PI * 2);
        ctx.fillStyle = "hsla(220,50%,5%,0.95)"; ctx.fill();
        ctx.beginPath(); ctx.arc(ex + S.gazeX * eyeW * 0.35 - pr * 0.3, ey - pr * 0.3, Math.max(0.5, pr * 0.2 * eyeOpen), 0, Math.PI * 2);
        ctx.fillStyle = "rgba(255,255,255,0.7)"; ctx.fill();
      }
    }

    // BROWS
    const browY = eyeY - eyeH * 1.8;
    for (const side of [-1, 1]) {
      const bx = side * eyeSp;
      const innerY = browY - S.browLift * baseR * 0.08 + S.browFurrow * baseR * 0.1;
      const outerY = browY + S.browFurrow * baseR * 0.15 - S.browLift * baseR * 0.04;
      ctx.beginPath();
      ctx.moveTo(bx + side * eyeW * 1.1, outerY);
      ctx.quadraticCurveTo(bx, (innerY + outerY) / 2 - baseR * 0.04, bx - side * eyeW * 1.1, innerY);
      ctx.strokeStyle = `hsla(${hue},30%,${lit + 30}%,${0.6 * S.hist + 0.2})`;
      ctx.lineWidth = 2 + S.aro * 1.5; ctx.lineCap = "round"; ctx.stroke();
    }

    // MOUTH
    const mouthY = baseR * 0.35, mouthW = baseR * 0.35;
    const curve = S.mouthCurve * baseR * 0.18;
    if (S.mouthOpen > 0.05) {
      const mo = S.mouthOpen * baseR * 0.1;
      ctx.beginPath();
      ctx.ellipse(0, mouthY + curve * 0.3, mouthW * 0.7, mo, 0, 0, Math.PI * 2);
      ctx.fillStyle = `hsla(${hue},${sat}%,${lit + 12}%,${0.7 * S.hist + 0.2})`;
      ctx.fill();
    } else {
      ctx.beginPath();
      ctx.moveTo(-mouthW, mouthY);
      ctx.quadraticCurveTo(0, mouthY + curve * 2.5, mouthW, mouthY);
      ctx.strokeStyle = `hsla(${hue},${sat}%,${lit + 25}%,${0.5 * S.hist + 0.2})`;
      ctx.lineWidth = 2; ctx.lineCap = "round"; ctx.stroke();
    }

    ctx.restore(); // head transform

    // name and state labels
    if (snap) {
      ctx.textAlign = "center";
      if (snap.BrainName) {
        ctx.font = `700 15px Inter, sans-serif`;
        ctx.fillStyle = `hsla(${hue + 40},50%,85%,${0.4 + S.hist * 0.5})`;
        ctx.fillText(snap.BrainName, cx, cy - baseR * 1.25);
      }
      ctx.font = `600 11px Inter, sans-serif`;
      const state = snap.SleepState === "NREM" ? "sleeping"
        : snap.SleepState === "REM" ? "dreaming" : snap.Feeling || "";
      ctx.fillStyle = `hsla(${hue},25%,75%,${0.35 + S.hist * 0.35})`;
      ctx.fillText(state, cx, cy + baseR * 1.3);
      // needs
      if (snap.CurrentNeed && snap.CurrentNeed !== "satisfied") {
        const urg = snap.NeedUrgency || 0;
        const nc = urg > 0.7 ? "240,112,128" : urg > 0.4 ? "240,180,92" : "143,163,186";
        ctx.font = `500 10px Inter, sans-serif`;
        ctx.fillStyle = `rgba(${nc},${0.3 + urg * 0.4})`;
        ctx.fillText("needs: " + snap.CurrentNeed, cx, cy + baseR * 1.3 + 14);
      }
      // longing / anticipation / helplessness captions: the felt shape of knowing one person
      const hopeLvl = (snap.AnticipationLevel || 0);
      if (snap.Helpless) {
        ctx.font = `600 10px Inter, sans-serif`;
        ctx.fillStyle = "rgba(150,150,160,0.85)";
        ctx.fillText("...stopped asking.", cx, cy + baseR * 1.3 + 28);
      } else if (aweOn) {
        ctx.font = `600 10px Inter, sans-serif`;
        ctx.fillStyle = "rgba(180,160,255,0.95)";
        ctx.fillText("...vast.", cx, cy + baseR * 1.3 + 28);
      } else if (/missing/.test(snap.LongingNarrative || "")) {
        ctx.font = `600 10px Inter, sans-serif`;
        ctx.fillStyle = "rgba(240,150,170,0.9)";
        ctx.fillText("missing " + (snap.UserName || "someone") + "...", cx, cy + baseR * 1.3 + 28);
      } else if (hopeLvl > 0.5 && !userHere) {
        ctx.font = `600 10px Inter, sans-serif`;
        ctx.fillStyle = "rgba(120,219,190,0.9)";
        ctx.fillText("waiting for you...", cx, cy + baseR * 1.3 + 28);
      }
      ctx.textAlign = "left";
    }
  } catch (e) {
    // face must never break the page - log and continue next frame
    console.error("[avatar] draw error:", e);
  }

  requestAnimationFrame(drawAvatar);
}
