// UI glue: the whole room is lit by the creature (hue = dopamine, glow = arousal + firing), tabs, log drawer.
"use strict";
(() => {
  const root = document.documentElement.style;
  const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
  let hue = 175, glow = 0.3;

  ws.addEventListener("message", (e) => {
    let s; try { s = JSON.parse(e.data); } catch { return; }
    const da = (s.Modulators && s.Modulators[0]) || 0.3;
    hue += ((205 - clamp(da / 1.2, 0, 1) * 155) - hue) * 0.1;
    glow += (clamp((s.Arousal || 0) * 0.6 + (s.MeanRateHz || 0) / 30 * 0.4, 0, 1) - glow) * 0.15;
    root.setProperty("--hue", hue.toFixed(0));
    root.setProperty("--glow", glow.toFixed(2));
    document.body.dataset.sleep = s.SleepState || "";
  });

  document.querySelectorAll("[data-tab]").forEach((b) => b.addEventListener("click", () => {
    document.querySelectorAll("[data-tab]").forEach((x) => x.classList.toggle("on", x === b));
    document.querySelectorAll(".tabpane").forEach((p) => p.classList.toggle("on", p.dataset.pane === b.dataset.tab));
  }));

  const toggleLog = () => document.body.classList.toggle("log-open");
  const toggleFs = () => document.fullscreenElement ? document.exitFullscreen() : document.documentElement.requestFullscreen();
  document.getElementById("log-toggle").onclick = toggleLog;
  document.getElementById("fs-toggle").onclick = toggleFs;
  addEventListener("keydown", (e) => {
    if (/input|textarea/i.test(e.target.tagName) || e.metaKey || e.ctrlKey) return;
    if (e.key === "l") toggleLog();
    if (e.key === "f") toggleFs();
  });
})();
