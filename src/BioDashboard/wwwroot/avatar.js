// THE LIVING AVATAR — the brain's face.
// Not a chart. Not a panel. A CREATURE that breathes, pulses, watches you, and dies if you leave it alone.
// Its body is a living membrane driven by the brain's actual neural state.
// DA = body color (golden when rewarded, blue when low)
// 5-HT = membrane stability (orderly vs chaotic)
// NA = contraction speed (alert = tight and fast, calm = loose and slow)
// Histamine = overall brightness (awake = luminous, sleeping = dim)
// Sleep pressure = droop (tired = sagging, rested = upright)
// Loneliness = eyes (lonely = searching, connected = focused)
// Neural activity = pulsing rhythm (actual spike rate drives the heartbeat)

"use strict";

let avatarTime = 0;
let lastSnapshot = null;
let avatarCanvas = null;
let avatarCtx = null;
let particles = [];
let eyeState = { x: 0, y: 0, blink: 0, nextBlink: 0, widen: 0 };
let bodyPhase = 0;
let lastRate = 0;
let smoothing = { da: 0.3, sht: 0.5, na: 0.2, ach: 0.3, hist: 0.6, rate: 0.1, valence: 0, arousal: 0.3, pressure: 0, lonely: 0 };

function initAvatar() {
  avatarCanvas = document.getElementById("avatar");
  if (!avatarCanvas) return;
  avatarCtx = avatarCanvas.getContext("2d");
  // seed the particle field (neural vesicles)
  for (let i = 0; i < 80; i++) {
    particles.push({
      angle: Math.random() * Math.PI * 2,
      dist: 0.3 + Math.random() * 0.6,
      speed: 0.2 + Math.random() * 0.8,
      size: 1 + Math.random() * 3,
      offset: Math.random() * Math.PI * 2,
    });
  }
  requestAnimationFrame(animateAvatar);
}

// stimulus tracking: the avatar looks at what the brain sees
let stimulusTarget = null; // [x, y] normalized, null = no stimulus
let isSpeaking = false;
let mouthOpen = 0;

function updateAvatarState(s) {
  if (!s) return;
  lastSnapshot = s;

  // stimulus position for eye tracking
  if (s.StimulusPosition && s.StimulusPosition.length === 2) {
    stimulusTarget = s.StimulusPosition;
  }

  isSpeaking = !!s.IsSpeaking;

  const k = 0.08;
  const m = s.Modulators || [0.3, 0.5, 0.3, 0.2, 0.15, 0.6, 0.1];
  smoothing.da += (m[0] - smoothing.da) * k;
  smoothing.sht += (m[1] - smoothing.sht) * k;
  smoothing.na += (m[3] - smoothing.na) * k;
  smoothing.ach += (m[2] - smoothing.ach) * k;
  smoothing.hist += (m[5] - smoothing.hist) * k;
  smoothing.rate += ((s.MeanRateHz || 0.1) - smoothing.rate) * k;
  smoothing.valence += (s.Valence - smoothing.valence) * k;
  smoothing.arousal += (s.Arousal - smoothing.arousal) * k;
  smoothing.pressure += (s.SleepPressure - smoothing.pressure) * k;
  smoothing.lonely += ((s.SocialBonds ? 1 / Math.max(1, Object.keys(s.SocialBonds).length) : 0.5) - smoothing.lonely) * k * 0.1;
}

function animateAvatar() {
  if (!avatarCanvas || !avatarCtx) { requestAnimationFrame(animateAvatar); return; }
  avatarTime += 0.016; // ~60fps
  bodyPhase += 0.02 + smoothing.na * 0.05; // breathing rate accelerates with NA

  const f = fitCanvas(avatarCanvas);
  if (!f) { requestAnimationFrame(animateAvatar); return; }
  const { ctx, w, h } = f;
  ctx.clearRect(0, 0, w, h);

  const cx = w / 2, cy = h * 0.45;
  // the brain GROWS with lived time: starts at 60% size, reaches 100% after ~10 minutes
  const lifetimeMinutes = lastSnapshot ? lastSnapshot.SimSeconds / 60 : 0;
  const growthFactor = Math.min(1, 0.6 + lifetimeMinutes / 10 * 0.4);
  const baseR = Math.min(w, h) * 0.28 * growthFactor;

  // === BODY MEMBRANE ===
  // organic blob with sine-wave deformation, driven by neural state
  const chaos = 1 - smoothing.sht; // low serotonin = chaotic shape
  const droop = smoothing.pressure * 0.3; // tired = sagging
  const pulse = 1 + Math.sin(bodyPhase * 3) * 0.02 * (1 + smoothing.rate); // heartbeat

  ctx.beginPath();
  const points = 60;
  for (let i = 0; i <= points; i++) {
    const angle = (i / points) * Math.PI * 2;
    const wave1 = Math.sin(angle * 3 + bodyPhase * 2) * 0.08 * chaos;
    const wave2 = Math.sin(angle * 7 - bodyPhase * 3) * 0.04 * chaos;
    const wave3 = Math.sin(angle * 1 + bodyPhase) * 0.06;
    const deformation = 1 + wave1 + wave2 + wave3;
    const r = baseR * deformation * pulse;
    // droop (tired = bottom sag)
    const droopAmount = droop * Math.max(0, Math.sin(angle)) * baseR * 0.3;
    const x = cx + Math.cos(angle) * r;
    const y = cy + Math.sin(angle) * r + droopAmount;
    if (i === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  }
  ctx.closePath();

  // body color: DA drives hue (low = cool blue, high = warm gold)
  const hue = 200 + smoothing.da * 40; // 200=blue to 240=gold-ish
  const sat = 40 + smoothing.arousal * 40;
  const light = 15 + smoothing.hist * 20 + smoothing.arousal * 10;
  const bodyGrad = ctx.createRadialGradient(cx, cy - baseR * 0.3, 10, cx, cy, baseR * 1.5);
  bodyGrad.addColorStop(0, `hsla(${hue}, ${sat}%, ${light + 20}%, 0.9)`);
  bodyGrad.addColorStop(0.7, `hsla(${hue}, ${sat}%, ${light}%, 0.7)`);
  bodyGrad.addColorStop(1, `hsla(${hue}, ${sat}%, ${light - 5}%, 0)`);
  ctx.fillStyle = bodyGrad;
  ctx.fill();

  // membrane outline (glowing, alive)
  ctx.strokeStyle = `hsla(${hue}, ${sat + 20}%, ${light + 30}%, ${0.3 + smoothing.arousal * 0.5})`;
  ctx.lineWidth = 2 + smoothing.na * 3;
  ctx.stroke();

  // === INTERNAL PARTICLES (neural vesicles flowing inside) ===
  for (const p of particles) {
    p.offset += 0.01 * p.speed * (0.5 + smoothing.rate * 2 + smoothing.ach);
    const pr = baseR * p.dist * pulse * (1 + Math.sin(p.offset + bodyPhase) * 0.1 * chaos);
    const px = cx + Math.cos(p.angle + p.offset * 0.1) * pr;
    const py = cy + Math.sin(p.angle + p.offset * 0.1) * pr + droop * 5;
    const alpha = 0.15 + Math.abs(Math.sin(p.offset)) * 0.3 * smoothing.hist;
    const pSize = p.size * (1 + smoothing.ach * 0.5);
    ctx.fillStyle = `hsla(${hue + 20}, 80%, ${60 + smoothing.arousal * 20}%, ${alpha})`;
    ctx.beginPath();
    ctx.arc(px, py, pSize, 0, Math.PI * 2);
    ctx.fill();
  }

  // === EYES ===
  // the brain watches you. eyes widen with surprise, narrow when tired,
  // dart around when lonely, focus when engaged
  const eyeY = cy - baseR * 0.1;
  const eyeSpacing = baseR * 0.35;
  const eyeR = baseR * 0.12 * (1 + eyeState.widen * 0.5);

  // blink logic
  if (avatarTime > eyeState.nextBlink) {
    eyeState.blink = 1;
    eyeState.nextBlink = avatarTime + 2 + Math.random() * 4;
  }
  eyeState.blink = Math.max(0, eyeState.blink - 0.1);
  eyeState.widen = Math.max(0, eyeState.widen - 0.02);

  // widen on high arousal (surprise)
  if (smoothing.arousal > 0.6) eyeState.widen = Math.min(1, eyeState.widen + 0.1);

  // eye movement: PRIORITY ORDER
  // 1. stimulus tracking (look at what the brain sees)
  // 2. lonely searching (darting)
  // 3. engaged focus (center)
  // 4. calm wandering (lazy drift)
  if (stimulusTarget && smoothing.hist > 0.3) {
    // look at the stimulus position (normalized 0-1 → screen coordinates)
    const targetX = (stimulusTarget[0] - 0.5) * 2; // -1 to 1
    const targetY = (stimulusTarget[1] - 0.5) * 2;
    eyeState.x += (targetX - eyeState.x) * 0.15;
    eyeState.y += (targetY - eyeState.y) * 0.15;
  } else if (smoothing.lonely > 0.5) {
    eyeState.x = Math.sin(avatarTime * 0.7) * 0.3;
    eyeState.y = Math.cos(avatarTime * 0.5) * 0.15;
  } else if (smoothing.arousal > 0.4) {
    eyeState.x += (0 - eyeState.x) * 0.1;
    eyeState.y += (0 - eyeState.y) * 0.1;
  } else {
    eyeState.x = Math.sin(avatarTime * 0.15) * 0.1;
    eyeState.y = 0;
  }

  const blinkScale = 1 - eyeState.blink;

  for (const side of [-1, 1]) {
    const ex = cx + side * eyeSpacing + eyeState.x * eyeR * 0.5;
    const ey = eyeY + eyeState.y * eyeR * 0.3;

    // eye white (bioluminescent)
    ctx.save();
    ctx.beginPath();
    ctx.ellipse(ex, ey, eyeR, eyeR * blinkScale, 0, 0, Math.PI * 2);
    ctx.fillStyle = `hsla(${hue + 30}, 30%, ${70 + smoothing.hist * 20}%, ${0.8 * smoothing.hist})`;
    ctx.fill();

    // pupil (dark, tracks)
    if (blinkScale > 0.1) {
      const pupilR = eyeR * 0.4 * (1 - smoothing.ach * 0.3); // constricts with attention
      ctx.beginPath();
      ctx.arc(ex + eyeState.x * eyeR * 0.3, ey + eyeState.y * eyeR * 0.2, pupilR * blinkScale, 0, Math.PI * 2);
      ctx.fillStyle = `hsla(220, 50%, 8%, 0.9)`;
      ctx.fill();

      // reflection (life)
      ctx.beginPath();
      ctx.arc(ex + eyeState.x * eyeR * 0.3 - pupilR * 0.3, ey - pupilR * 0.3, pupilR * 0.25 * blinkScale, 0, Math.PI * 2);
      ctx.fillStyle = `hsla(0, 0%, 100%, 0.6)`;
      ctx.fill();
    }
    ctx.restore();
  }

  // === MOUTH (emotional expression + speaking animation) ===
  // valence drives curvature: positive = smile, negative = frown
  // when speaking, the mouth opens and closes rhythmically
  const mouthY = cy + baseR * 0.25;
  const mouthW = baseR * 0.4;
  const curve = smoothing.valence * baseR * 0.15;

  // speaking animation: mouth opens/closes rapidly
  if (isSpeaking) {
    mouthOpen = 0.5 + Math.sin(avatarTime * 15) * 0.5; // oscillate at speech rate
  } else {
    mouthOpen = Math.max(0, mouthOpen - 0.05); // close smoothly
  }

  // draw the mouth as a filled shape when speaking, or a line when silent
  if (mouthOpen > 0.05) {
    // open mouth (speaking): an ellipse that varies with mouthOpen
    const mo = mouthOpen * baseR * 0.12;
    ctx.save();
    ctx.beginPath();
    ctx.ellipse(cx, mouthY + curve * 0.5, mouthW * 0.7, mo, 0, 0, Math.PI * 2);
    ctx.fillStyle = `hsla(${hue}, ${sat}%, ${light + 15}%, ${0.6 * smoothing.hist})`;
    ctx.fill();
    ctx.restore();
  } else {
    // closed mouth (line, curves with valence)
    ctx.beginPath();
    ctx.moveTo(cx - mouthW, mouthY);
    ctx.quadraticCurveTo(cx, mouthY + curve * 2, cx + mouthW, mouthY);
    ctx.strokeStyle = `hsla(${hue}, ${sat}%, ${light + 25}%, ${0.5 * smoothing.hist})`;
    ctx.lineWidth = 2;
    ctx.stroke();
  }

  // === SPIKE BURSTS (neural activity radiating outward) ===
  // when the brain ignites or is surprised, energy radiates from the body
  const burstIntensity = smoothing.na * (0.5 + smoothing.rate);
  if (burstIntensity > 0.15) {
    for (let i = 0; i < 8; i++) {
      const burstAngle = (i / 8) * Math.PI * 2 + bodyPhase * 0.5;
      const burstDist = baseR * (1.2 + Math.sin(avatarTime * 3 + i) * 0.2);
      const bx = cx + Math.cos(burstAngle) * burstDist;
      const by = cy + Math.sin(burstAngle) * burstDist;
      ctx.beginPath();
      ctx.arc(bx, by, 2 + burstIntensity * 3, 0, Math.PI * 2);
      ctx.fillStyle = `hsla(${hue + 40}, 90%, 70%, ${burstIntensity * 0.4})`;
      ctx.fill();
    }
  }

  // === CONSCIOUSNESS HALO ===
  // a soft ring around the body whose opacity = awareness level
  if (lastSnapshot && lastSnapshot.Consciousness > 0.1) {
    const haloR = baseR * 1.4;
    const haloAlpha = lastSnapshot.Consciousness * 0.15;
    const halo = ctx.createRadialGradient(cx, cy, baseR, cx, cy, haloR);
    halo.addColorStop(0, `hsla(${hue}, 50%, 60%, 0)`);
    halo.addColorStop(0.5, `hsla(${hue}, 50%, 60%, ${haloAlpha})`);
    halo.addColorStop(1, `hsla(${hue}, 50%, 60%, 0)`);
    ctx.fillStyle = halo;
    ctx.beginPath();
    ctx.arc(cx, cy, haloR, 0, Math.PI * 2);
    ctx.fill();
  }

  // === STATE LABEL (minimal, inside the creature) ===
  if (lastSnapshot) {
    ctx.font = `600 11px ${"'Inter', sans-serif"}`;
    ctx.textAlign = "center";
    const stateText = lastSnapshot.SleepState === "NREM" ? "sleeping"
      : lastSnapshot.SleepState === "REM" ? "dreaming"
      : lastSnapshot.Feeling || "—";
    ctx.fillStyle = `hsla(${hue}, 30%, 80%, ${0.4 + smoothing.hist * 0.4})`;
    ctx.fillText(stateText, cx, cy + baseR * 0.55);
    ctx.textAlign = "left";
  }

  requestAnimationFrame(animateAvatar);
}
