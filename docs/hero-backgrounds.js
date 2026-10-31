'use strict';

// Select before the first paint. A shuffled bag shows every scene once per cycle.
(() => {
  const scenes = ['a1', 'b1', 'b2', 'b3'];
  const key = 'ghost-vault-hero-scenes-v1';
  let last = null;
  let remaining = [];
  try {
    const saved = JSON.parse(localStorage.getItem(key));
    if (saved && scenes.includes(saved.last)) {
      last = saved.last;
      if (Array.isArray(saved.remaining) && saved.remaining.length < scenes.length &&
          new Set(saved.remaining).size === saved.remaining.length &&
          saved.remaining.every(id => scenes.includes(id) && id !== last)) {
        remaining = saved.remaining.slice();
      }
    }
  } catch { /* Storage may be blocked. The artwork still works. */ }

  if (!remaining.length) {
    remaining = scenes.slice();
    for (let i = remaining.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [remaining[i], remaining[j]] = [remaining[j], remaining[i]];
    }
    if (remaining[0] === last) {
      const j = 1 + Math.floor(Math.random() * (remaining.length - 1));
      [remaining[0], remaining[j]] = [remaining[j], remaining[0]];
    }
  }

  const selected = remaining.shift();
  const url = `assets/hero-scene-${selected}-v1.webp`;
  document.documentElement.dataset.heroScene = selected;
  document.documentElement.style.setProperty('--ghost-hero-background', `url("${url}")`);
  const preload = document.createElement('link');
  preload.rel = 'preload';
  preload.as = 'image';
  preload.href = url;
  document.head.appendChild(preload);
  try {
    localStorage.setItem(key, JSON.stringify({ last: selected, remaining }));
  } catch { /* No persistence is required to display a scene. */ }
})();
