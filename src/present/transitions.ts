import type { Slide } from '../model/types';
import { clamp } from '../model/util';

/* ------------------------------------------------------------------ */
/* Engine                                                              */
/* ------------------------------------------------------------------ */

export interface Controller {
  skipped: boolean;
  anims: Set<Animation>;
  skip(): void;
}

export function createController(): Controller {
  const ctl: Controller = {
    skipped: false,
    anims: new Set(),
    skip() {
      ctl.skipped = true;
      for (const a of ctl.anims) {
        try { a.finish(); } catch { /* infinite or cancelled */ }
      }
    },
  };
  return ctl;
}

export interface TransitionCtx {
  /** Outgoing slide layer, null when the show starts. */
  from: HTMLElement | null;
  /** Incoming slide layer. */
  to: HTMLElement;
  /** Overlay container above both layers, in the same coordinate space. */
  fx: HTMLElement;
  W: number;
  H: number;
  duration: number;
  dir: string;
  easing: string;
  accent: string;
  ctl: Controller;
  fromSlide?: Slide | null;
  toSlide?: Slide;
  cleanups: (() => void)[];
}

export interface DirOption {
  value: string;
  label: string;
}

export type TransitionCategory = 'Subtle' | 'Exciting' | 'Dynamic content' | 'Creative';

export interface TransitionDef {
  id: string;
  name: string;
  category: TransitionCategory;
  directions?: DirOption[];
  run(ctx: TransitionCtx): Promise<void>;
}

export const EASINGS: { id: string; label: string; css: string; fn: (t: number) => number }[] = [
  { id: 'ease-in-out', label: 'Smooth', css: 'cubic-bezier(.65,0,.35,1)', fn: (t) => (t < 0.5 ? 4 * t ** 3 : 1 - (-2 * t + 2) ** 3 / 2) },
  { id: 'ease-out', label: 'Decelerate', css: 'cubic-bezier(.22,1,.36,1)', fn: (t) => 1 - (1 - t) ** 3 },
  { id: 'ease-in', label: 'Accelerate', css: 'cubic-bezier(.64,0,.78,0)', fn: (t) => t ** 3 },
  { id: 'linear', label: 'Linear', css: 'linear', fn: (t) => t },
  {
    id: 'back', label: 'Overshoot', css: 'cubic-bezier(.34,1.56,.64,1)',
    fn: (t) => 1 + 2.70158 * (t - 1) ** 3 + 1.70158 * (t - 1) ** 2,
  },
  {
    id: 'bounce', label: 'Bounce', css: 'cubic-bezier(.34,1.56,.64,1)',
    fn: (t) => {
      const n = 7.5625, d = 2.75;
      if (t < 1 / d) return n * t * t;
      if (t < 2 / d) return n * (t -= 1.5 / d) * t + 0.75;
      if (t < 2.5 / d) return n * (t -= 2.25 / d) * t + 0.9375;
      return n * (t -= 2.625 / d) * t + 0.984375;
    },
  },
];

export function easingCss(id: string): string {
  return EASINGS.find((e) => e.id === id)?.css ?? id;
}

function easingFn(id: string): (t: number) => number {
  return EASINGS.find((e) => e.id === id)?.fn ?? EASINGS[0].fn;
}

interface WaOpts {
  duration?: number;
  delay?: number;
  easing?: string;
}

function wa(ctx: TransitionCtx, el: Element, kf: Keyframe[], o: WaOpts = {}): Promise<void> {
  const a = el.animate(kf, {
    duration: Math.max(1, o.duration ?? ctx.duration),
    delay: o.delay ?? 0,
    easing: easingCss(o.easing ?? ctx.easing),
    fill: 'both',
  });
  ctx.ctl.anims.add(a);
  if (ctx.ctl.skipped) a.finish();
  return a.finished.then(() => undefined, () => undefined);
}

/** Frame driven animation for effects that rebuild a clip path every frame. */
function tween(ctx: TransitionCtx, fn: (p: number) => void, o: WaOpts = {}): Promise<void> {
  const d = Math.max(1, o.duration ?? ctx.duration);
  const ease = easingFn(o.easing ?? ctx.easing);
  return new Promise((resolve) => {
    const start = performance.now();
    fn(0);
    const tick = (now: number) => {
      const t = ctx.ctl.skipped ? 1 : clamp((now - start) / d, 0, 1);
      fn(ease(t));
      if (t >= 1) resolve();
      else requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  });
}

const all = (p: Promise<void>[]) => Promise.all(p).then(() => undefined);

/** Seeded random numbers so a transition looks the same each time it runs. */
function rng(seed = 7) {
  let s = seed;
  return () => {
    s = (s * 16807) % 2147483647;
    return (s - 1) / 2147483646;
  };
}

function cloneLayer(ctx: TransitionCtx, layer: HTMLElement): HTMLElement {
  const c = layer.cloneNode(true) as HTMLElement;
  c.querySelectorAll('iframe, video').forEach((n) => {
    const ph = document.createElement('div');
    ph.style.cssText = 'width:100%;height:100%;background:#000';
    n.replaceWith(ph);
  });
  c.style.cssText = `position:absolute;left:0;top:0;width:${ctx.W}px;height:${ctx.H}px;`;
  return c;
}

interface Tile {
  el: HTMLElement;
  c: number;
  r: number;
  x: number;
  y: number;
  w: number;
  h: number;
  cx: number;
  cy: number;
}

function tiles(ctx: TransitionCtx, layer: HTMLElement, cols: number, rows: number): Tile[] {
  const tw = ctx.W / cols;
  const th = ctx.H / rows;
  const out: Tile[] = [];
  for (let r = 0; r < rows; r++) {
    for (let c = 0; c < cols; c++) {
      const t = document.createElement('div');
      const x = c * tw, y = r * th;
      t.style.cssText = `position:absolute;left:${x}px;top:${y}px;width:${tw + 0.6}px;height:${th + 0.6}px;overflow:hidden;backface-visibility:hidden;will-change:transform`;
      const cl = cloneLayer(ctx, layer);
      cl.style.left = `${-x}px`;
      cl.style.top = `${-y}px`;
      t.appendChild(cl);
      ctx.fx.appendChild(t);
      out.push({ el: t, c, r, x, y, w: tw, h: th, cx: x + tw / 2, cy: y + th / 2 });
    }
  }
  return out;
}

function overlay(ctx: TransitionCtx, css: string): HTMLElement {
  const d = document.createElement('div');
  d.style.cssText = `position:absolute;left:0;top:0;width:${ctx.W}px;height:${ctx.H}px;${css}`;
  ctx.fx.appendChild(d);
  return d;
}

function hide(el: HTMLElement | null) {
  if (el) el.style.visibility = 'hidden';
}

function off(ctx: TransitionCtx, dir: string): [number, number] {
  switch (dir) {
    case 'left': return [-ctx.W, 0];
    case 'top': return [0, -ctx.H];
    case 'bottom': return [0, ctx.H];
    default: return [ctx.W, 0];
  }
}

const tr = (x: number, y: number) => `translate(${x}px, ${y}px)`;
const sgn = (dir: string) => (dir === 'left' || dir === 'top' ? -1 : 1);
const isVertical = (dir: string) => dir === 'top' || dir === 'bottom';

const rect = (x: number, y: number, w: number, h: number) =>
  w <= 0 || h <= 0 ? '' : `M${x.toFixed(1)} ${y.toFixed(1)}h${w.toFixed(1)}v${h.toFixed(1)}h${(-w).toFixed(1)}Z`;

/** clip-path: path() with a fallback that hides everything while the path is empty. */
function setPath(el: HTMLElement, d: string) {
  el.style.clipPath = d ? `path("${d}")` : 'inset(50% 50% 50% 50%)';
}

function wedgePath(cx: number, cy: number, R: number, a0: number, a1: number): string {
  if (a1 - a0 <= 0.0001) return '';
  const steps = Math.max(2, Math.ceil(((a1 - a0) * 180) / Math.PI / 4));
  let d = `M${cx.toFixed(1)} ${cy.toFixed(1)}`;
  for (let i = 0; i <= steps; i++) {
    const a = a0 + ((a1 - a0) * i) / steps;
    d += `L${(cx + R * Math.cos(a)).toFixed(1)} ${(cy + R * Math.sin(a)).toFixed(1)}`;
  }
  return d + 'Z';
}

function hexPath(cx: number, cy: number, R: number): string {
  if (R <= 0.5) return '';
  let d = '';
  for (let i = 0; i < 6; i++) {
    const a = (Math.PI / 3) * i;
    d += `${i ? 'L' : 'M'}${(cx + R * Math.cos(a)).toFixed(1)} ${(cy + R * Math.sin(a)).toFixed(1)}`;
  }
  return d + 'Z';
}

function diamondPath(cx: number, cy: number, R: number): string {
  if (R <= 0.5) return '';
  return `M${cx.toFixed(1)} ${(cy - R).toFixed(1)}L${(cx + R).toFixed(1)} ${cy.toFixed(1)}L${cx.toFixed(1)} ${(cy + R).toFixed(1)}L${(cx - R).toFixed(1)} ${cy.toFixed(1)}Z`;
}

/* ------------------------------------------------------------------ */
/* Option sets                                                         */
/* ------------------------------------------------------------------ */

const DIR4: DirOption[] = [
  { value: 'right', label: 'From right' },
  { value: 'left', label: 'From left' },
  { value: 'bottom', label: 'From bottom' },
  { value: 'top', label: 'From top' },
];
const DIR2: DirOption[] = [
  { value: 'right', label: 'From right' },
  { value: 'left', label: 'From left' },
];
const ORIENT: DirOption[] = [
  { value: 'horizontal', label: 'Horizontal' },
  { value: 'vertical', label: 'Vertical' },
];
const CORNERS: DirOption[] = [
  { value: 'tl', label: 'From top left' },
  { value: 'tr', label: 'From top right' },
  { value: 'bl', label: 'From bottom left' },
  { value: 'br', label: 'From bottom right' },
];

/* ------------------------------------------------------------------ */
/* Definitions                                                         */
/* ------------------------------------------------------------------ */

function insetStart(dir: string): string {
  switch (dir) {
    case 'left': return 'inset(0 100% 0 0)';
    case 'top': return 'inset(0 0 100% 0)';
    case 'bottom': return 'inset(100% 0 0 0)';
    default: return 'inset(0 0 0 100%)';
  }
}

function fadeThrough(color: string) {
  return (ctx: TransitionCtx) => {
    const o = overlay(ctx, `background:${color};`);
    return all([
      wa(ctx, o, [{ opacity: 0 }, { opacity: 1, offset: 0.45 }, { opacity: 1, offset: 0.55 }, { opacity: 0 }], { easing: 'linear' }),
      wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 0, offset: 0.5 }, { opacity: 1, offset: 0.5 }, { opacity: 1 }], { easing: 'linear' }),
    ]);
  };
}

function cube(ctx: TransitionCtx, dip: number) {
  const s = sgn(ctx.dir);
  const vert = isVertical(ctx.dir);
  const d = (vert ? ctx.H : ctx.W) / 2;
  const axis = vert ? 'rotateX' : 'rotateY';
  // Incoming from right uses the right face (+90 on Y). Incoming from bottom uses -90 on X.
  const inAngle = vert ? -s * 90 : s * 90;
  const persp = `perspective(${Math.max(ctx.W, ctx.H) * 2.2}px)`;
  const face = (a: number, extra = 0) => `${persp} translateZ(${-d - extra}px) ${axis}(${a}deg) translateZ(${d}px)`;
  const mid = (a: number) => ({ transform: face(a, dip * d), offset: 0.5 });
  for (const el of [ctx.from, ctx.to]) if (el) el.style.backfaceVisibility = 'hidden';
  return all([
    ctx.from ? wa(ctx, ctx.from, [{ transform: face(0) }, ...(dip ? [mid(-inAngle / 2)] : []), { transform: face(-inAngle) }]) : Promise.resolve(),
    wa(ctx, ctx.to, [{ transform: face(inAngle) }, ...(dip ? [mid(inAngle / 2)] : []), { transform: face(0) }]),
  ]);
}

function clipTween(ctx: TransitionCtx, build: (p: number) => string, target: HTMLElement = ctx.to) {
  return tween(ctx, (p) => setPath(target, p >= 1 ? rect(-2, -2, ctx.W + 4, ctx.H + 4) : build(p)));
}

function randomOrder(n: number, seed = 11): number[] {
  const rand = rng(seed);
  return Array.from({ length: n }, () => rand());
}

export const TRANSITIONS: TransitionDef[] = [
  /* ---------------- Subtle ---------------- */
  { id: 'none', name: 'None', category: 'Subtle', run: async () => undefined },
  {
    id: 'fade', name: 'Fade', category: 'Subtle',
    directions: [{ value: 'smooth', label: 'Smoothly' }, { value: 'black', label: 'Through black' }, { value: 'white', label: 'Through white' }],
    run: (ctx) => {
      if (ctx.dir === 'black') return fadeThrough('#000')(ctx);
      if (ctx.dir === 'white') return fadeThrough('#fff')(ctx);
      return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
    },
  },
  {
    id: 'push', name: 'Push', category: 'Subtle', directions: DIR4,
    run: (ctx) => {
      const [x, y] = off(ctx, ctx.dir);
      return all([
        wa(ctx, ctx.to, [{ transform: tr(x, y) }, { transform: 'none' }]),
        ctx.from ? wa(ctx, ctx.from, [{ transform: 'none' }, { transform: tr(-x, -y) }]) : Promise.resolve(),
      ]);
    },
  },
  {
    id: 'wipe', name: 'Wipe', category: 'Subtle', directions: DIR4,
    run: (ctx) => wa(ctx, ctx.to, [{ clipPath: insetStart(ctx.dir) }, { clipPath: 'inset(0 0 0 0)' }]),
  },
  {
    id: 'smooth-wipe', name: 'Soft wipe', category: 'Subtle', directions: DIR4,
    run: (ctx) => {
      const to = { right: 'to left', left: 'to right', top: 'to bottom', bottom: 'to top' }[ctx.dir] ?? 'to left';
      return tween(ctx, (p) => {
        const a = p * 130 - 30, b = p * 130;
        const m = p >= 1 ? 'none' : `linear-gradient(${to}, #000 ${a}%, transparent ${b}%)`;
        ctx.to.style.maskImage = m;
        ctx.to.style.setProperty('-webkit-mask-image', m);
      });
    },
  },
  {
    id: 'split', name: 'Split', category: 'Subtle',
    directions: [
      { value: 'vertical-out', label: 'Vertical out' }, { value: 'vertical-in', label: 'Vertical in' },
      { value: 'horizontal-out', label: 'Horizontal out' }, { value: 'horizontal-in', label: 'Horizontal in' },
    ],
    run: (ctx) => {
      const vert = ctx.dir.startsWith('vertical');
      const closed = vert ? 'inset(0 50% 0 50%)' : 'inset(50% 0 50% 0)';
      if (ctx.dir.endsWith('in') && ctx.from) {
        ctx.from.style.zIndex = '3';
        return wa(ctx, ctx.from, [{ clipPath: 'inset(0 0 0 0)' }, { clipPath: closed }]);
      }
      return wa(ctx, ctx.to, [{ clipPath: closed }, { clipPath: 'inset(0 0 0 0)' }]);
    },
  },
  {
    id: 'reveal', name: 'Reveal', category: 'Subtle', directions: DIR4,
    run: (ctx) => {
      const [x, y] = off(ctx, ctx.dir);
      if (ctx.from) ctx.from.style.zIndex = '3';
      return all([
        ctx.from ? wa(ctx, ctx.from, [{ transform: 'none', opacity: 1 }, { transform: tr(-x * 0.35, -y * 0.35), opacity: 0 }]) : Promise.resolve(),
        wa(ctx, ctx.to, [{ opacity: 0, transform: tr(x * 0.08, y * 0.08) }, { opacity: 0, offset: 0.3 }, { opacity: 1, transform: 'none' }]),
      ]);
    },
  },
  {
    id: 'random-bars', name: 'Random bars', category: 'Subtle', directions: ORIENT,
    run: (ctx) => {
      const n = 48;
      const order = randomOrder(n, 3);
      const vert = ctx.dir === 'vertical';
      const size = (vert ? ctx.W : ctx.H) / n;
      return clipTween(ctx, (p) => {
        let d = '';
        for (let i = 0; i < n; i++) {
          if (p > order[i]) d += vert ? rect(i * size, 0, size + 0.5, ctx.H) : rect(0, i * size, ctx.W, size + 0.5);
        }
        return d;
      });
    },
  },
  {
    id: 'shape', name: 'Shape', category: 'Subtle',
    directions: [
      { value: 'circle-out', label: 'Circle out' }, { value: 'circle-in', label: 'Circle in' },
      { value: 'diamond-out', label: 'Diamond out' }, { value: 'diamond-in', label: 'Diamond in' },
      { value: 'plus-out', label: 'Plus out' }, { value: 'plus-in', label: 'Plus in' },
    ],
    run: (ctx) => {
      const [kind, way] = ctx.dir.split('-');
      const plus = (a: number, e: number) => {
        const p = [
          [50 - a, 50 - e], [50 + a, 50 - e], [50 + a, 50 - a], [50 + e, 50 - a], [50 + e, 50 + a], [50 + a, 50 + a],
          [50 + a, 50 + e], [50 - a, 50 + e], [50 - a, 50 + a], [50 - e, 50 + a], [50 - e, 50 - a], [50 - a, 50 - a],
        ];
        return `polygon(${p.map(([x, y]) => `${x}% ${y}%`).join(',')})`;
      };
      let closed: string, open: string, midFrame: Keyframe[] = [];
      if (kind === 'diamond') {
        closed = 'polygon(50% 50%, 50% 50%, 50% 50%, 50% 50%)';
        open = 'polygon(50% -55%, 155% 50%, 50% 155%, -55% 50%)';
      } else if (kind === 'plus') {
        closed = plus(0, 0);
        open = plus(60, 160);
        midFrame = [{ clipPath: plus(8, 55), offset: 0.5 }];
      } else {
        closed = 'circle(0% at 50% 50%)';
        open = 'circle(72% at 50% 50%)';
      }
      if (way === 'in' && ctx.from) {
        ctx.from.style.zIndex = '3';
        return wa(ctx, ctx.from, [{ clipPath: open }, ...midFrame.map((m) => m), { clipPath: closed }]);
      }
      return wa(ctx, ctx.to, [{ clipPath: closed }, ...midFrame, { clipPath: open }]);
    },
  },
  {
    id: 'uncover', name: 'Uncover', category: 'Subtle', directions: DIR4,
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const [x, y] = off(ctx, ctx.dir);
      ctx.from.style.zIndex = '3';
      return wa(ctx, ctx.from, [{ transform: 'none' }, { transform: tr(-x, -y) }]);
    },
  },
  {
    id: 'cover', name: 'Cover', category: 'Subtle', directions: DIR4,
    run: (ctx) => {
      const [x, y] = off(ctx, ctx.dir);
      return wa(ctx, ctx.to, [{ transform: tr(x, y) }, { transform: 'none' }]);
    },
  },
  {
    id: 'flash', name: 'Flash', category: 'Subtle',
    run: (ctx) => {
      const o = overlay(ctx, 'background:#fff;');
      return all([
        wa(ctx, o, [{ opacity: 0 }, { opacity: 1, offset: 0.35 }, { opacity: 1, offset: 0.5 }, { opacity: 0 }], { easing: 'linear' }),
        wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 0, offset: 0.45 }, { opacity: 1, offset: 0.46 }, { opacity: 1 }], { easing: 'linear' }),
      ]);
    },
  },
  {
    id: 'dissolve', name: 'Dissolve', category: 'Subtle',
    run: (ctx) => {
      const cols = 32, rows = Math.max(1, Math.round((32 * ctx.H) / ctx.W));
      const cw = ctx.W / cols, ch = ctx.H / rows;
      const order = randomOrder(cols * rows, 5);
      return clipTween(ctx, (p) => {
        let d = '';
        for (let i = 0; i < order.length; i++) {
          if (p > order[i]) d += rect((i % cols) * cw, Math.floor(i / cols) * ch, cw + 0.5, ch + 0.5);
        }
        return d;
      });
    },
  },
  {
    id: 'morph', name: 'Morph', category: 'Subtle',
    run: (ctx) => runMorph(ctx),
  },

  /* ---------------- Exciting ---------------- */
  {
    id: 'fall-over', name: 'Fall over', category: 'Exciting', directions: [{ value: 'left', label: 'Left' }, { value: 'right', label: 'Right' }],
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const s = ctx.dir === 'left' ? -1 : 1;
      ctx.from.style.zIndex = '3';
      ctx.from.style.transformOrigin = '50% 100%';
      return wa(ctx, ctx.from, [
        { transform: 'perspective(1800px) rotateX(0deg) rotateZ(0deg)', opacity: 1 },
        { transform: `perspective(1800px) rotateX(70deg) rotateZ(${s * 6}deg)`, opacity: 1, offset: 0.8 },
        { transform: `perspective(1800px) rotateX(90deg) rotateZ(${s * 8}deg)`, opacity: 0 },
      ], { easing: 'ease-in' });
    },
  },
  {
    id: 'drape', name: 'Drape', category: 'Exciting', directions: [{ value: 'left', label: 'Left' }, { value: 'right', label: 'Right' }],
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const s = ctx.dir === 'left' ? -1 : 1;
      ctx.from.style.zIndex = '3';
      ctx.from.style.transformOrigin = s > 0 ? '0% 0%' : '100% 0%';
      return wa(ctx, ctx.from, [
        { transform: 'none' },
        { transform: `rotate(${s * 6}deg) skewY(${s * 4}deg) translateY(4%)`, offset: 0.35 },
        { transform: `rotate(${s * 28}deg) skewY(${s * 14}deg) translateY(125%)` },
      ], { easing: 'ease-in' });
    },
  },
  {
    id: 'curtains', name: 'Curtains', category: 'Exciting',
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const t = tiles(ctx, ctx.from, 2, 1);
      hide(ctx.from);
      t[0].el.style.transformOrigin = '0% 50%';
      t[1].el.style.transformOrigin = '100% 50%';
      return all([
        wa(ctx, t[0].el, [{ transform: 'perspective(1400px) none', filter: 'brightness(1)' }, { transform: 'perspective(1400px) translateX(-10%) scaleX(0.04) rotateY(25deg)', filter: 'brightness(.5)' }]),
        wa(ctx, t[1].el, [{ transform: 'perspective(1400px) none', filter: 'brightness(1)' }, { transform: 'perspective(1400px) translateX(10%) scaleX(0.04) rotateY(-25deg)', filter: 'brightness(.5)' }]),
        wa(ctx, ctx.to, [{ transform: 'scale(.94)', opacity: 0.6 }, { transform: 'none', opacity: 1 }]),
      ]);
    },
  },
  {
    id: 'wind', name: 'Wind', category: 'Exciting', directions: [{ value: 'right', label: 'Right' }, { value: 'left', label: 'Left' }],
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const s = ctx.dir === 'left' ? -1 : 1;
      const cols = 10, rows = 6;
      const t = tiles(ctx, ctx.from, cols, rows);
      hide(ctx.from);
      const rand = rng(17);
      const D = ctx.duration;
      return all([
        wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]),
        ...t.map((tile) => {
          const order = s > 0 ? tile.c / cols : 1 - tile.c / cols;
          return wa(ctx, tile.el, [
            { transform: 'none', opacity: 1 },
            { transform: `translate(${s * ctx.W * (0.9 + rand() * 0.6)}px, ${(rand() - 0.6) * ctx.H * 0.5}px) rotate(${(rand() - 0.5) * 140}deg) scale(${0.4 + rand() * 0.4})`, opacity: 0 },
          ], { duration: D * 0.55, delay: (1 - order) * D * 0.4 + rand() * D * 0.05, easing: 'ease-in' });
        }),
      ]);
    },
  },
  {
    id: 'prestige', name: 'Prestige', category: 'Exciting',
    run: (ctx) => {
      if (ctx.from) ctx.from.style.zIndex = '3';
      return all([
        ctx.from ? wa(ctx, ctx.from, [
          { transform: 'perspective(1600px) none', opacity: 1 },
          { transform: 'perspective(1600px) translateY(-25%) translateZ(-700px) rotateX(55deg)', opacity: 0 },
        ], { duration: ctx.duration * 0.7 }) : Promise.resolve(),
        wa(ctx, ctx.to, [
          { transform: 'perspective(1600px) translateZ(-500px) rotateX(-25deg)', opacity: 0 },
          { opacity: 0, offset: 0.35 },
          { transform: 'perspective(1600px) none', opacity: 1 },
        ]),
      ]);
    },
  },
  {
    id: 'fracture', name: 'Fracture', category: 'Exciting',
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const t = tiles(ctx, ctx.from, 6, 4);
      hide(ctx.from);
      const rand = rng(23);
      return all([
        wa(ctx, ctx.to, [{ transform: 'scale(.9)', filter: 'brightness(.5)' }, { transform: 'none', filter: 'brightness(1)' }]),
        ...t.map((tile) => {
          const dx = (tile.cx - ctx.W / 2) * (1.2 + rand() * 1.5);
          const dy = (tile.cy - ctx.H / 2) * (1.2 + rand() * 1.5) + ctx.H * 0.3;
          return wa(ctx, tile.el, [
            { transform: 'perspective(1200px) none', opacity: 1 },
            { transform: `perspective(1200px) translate3d(${dx}px, ${dy}px, ${200 + rand() * 400}px) rotateX(${(rand() - 0.5) * 360}deg) rotateY(${(rand() - 0.5) * 360}deg)`, opacity: 0 },
          ], { delay: rand() * ctx.duration * 0.2, duration: ctx.duration * 0.8, easing: 'ease-in' });
        }),
      ]);
    },
  },
  {
    id: 'crush', name: 'Crush', category: 'Exciting',
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const t = tiles(ctx, ctx.from, 6, 4);
      hide(ctx.from);
      const rand = rng(31);
      const maxDist = Math.hypot(ctx.W, ctx.H) / 2;
      return all([
        wa(ctx, ctx.to, [{ opacity: 0, transform: 'scale(.85)' }, { opacity: 0, offset: 0.4 }, { opacity: 1, transform: 'none' }]),
        ...t.map((tile) => {
          const dist = Math.hypot(tile.cx - ctx.W / 2, tile.cy - ctx.H / 2) / maxDist;
          return wa(ctx, tile.el, [
            { transform: 'none', opacity: 1 },
            { transform: `translate(${(ctx.W / 2 - tile.cx) * 0.95}px, ${(ctx.H / 2 - tile.cy) * 0.95}px) rotate(${(rand() - 0.5) * 300}deg) scale(.15)`, opacity: 1, offset: 0.7 },
            { transform: `translate(${ctx.W / 2 - tile.cx}px, ${ctx.H / 2 - tile.cy + ctx.H}px) rotate(${(rand() - 0.5) * 400}deg) scale(.05)`, opacity: 0 },
          ], { delay: (1 - dist) * ctx.duration * 0.15, duration: ctx.duration * 0.85, easing: 'ease-in' });
        }),
      ]);
    },
  },
  {
    id: 'peel-off', name: 'Peel off', category: 'Exciting', directions: [{ value: 'left', label: 'Left' }, { value: 'right', label: 'Right' }],
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const s = ctx.dir === 'right' ? 1 : -1;
      ctx.from.style.zIndex = '3';
      ctx.from.style.transformOrigin = s < 0 ? '0% 100%' : '100% 100%';
      return wa(ctx, ctx.from, [
        { transform: 'perspective(2000px) none', filter: 'drop-shadow(0 0 0 rgba(0,0,0,0))' },
        { transform: `perspective(2000px) translate(${-s * 6}%, -8%) rotateY(${s * 35}deg) rotateZ(${-s * 10}deg)`, filter: 'drop-shadow(0 30px 40px rgba(0,0,0,.45))', offset: 0.45 },
        { transform: `perspective(2000px) translate(${-s * 120}%, -60%) rotateY(${s * 75}deg) rotateZ(${-s * 35}deg)`, filter: 'drop-shadow(0 30px 40px rgba(0,0,0,.45))' },
      ], { easing: 'ease-in' });
    },
  },
  {
    id: 'page-curl', name: 'Page curl', category: 'Exciting',
    directions: [{ value: 'left', label: 'Single left' }, { value: 'right', label: 'Single right' }, { value: 'double', label: 'Double' }],
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const page = (el: HTMLElement, origin: string, angle: number) => {
        el.style.transformOrigin = origin;
        el.style.backfaceVisibility = 'hidden';
        return wa(ctx, el, [
          { transform: 'perspective(2400px) rotateY(0deg)', filter: 'brightness(1)' },
          { transform: `perspective(2400px) rotateY(${angle}deg)`, filter: 'brightness(.55)' },
        ], { easing: 'ease-in' });
      };
      if (ctx.dir === 'double') {
        const t = tiles(ctx, ctx.from, 2, 1);
        hide(ctx.from);
        return all([page(t[0].el, '0% 50%', 92), page(t[1].el, '100% 50%', -92)]);
      }
      ctx.from.style.zIndex = '3';
      return ctx.dir === 'right' ? page(ctx.from, '100% 50%', -92) : page(ctx.from, '0% 50%', 92);
    },
  },
  {
    id: 'airplane', name: 'Airplane', category: 'Exciting', directions: [{ value: 'right', label: 'Right' }, { value: 'left', label: 'Left' }],
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const s = ctx.dir === 'left' ? -1 : 1;
      ctx.from.style.zIndex = '3';
      return all([
        wa(ctx, ctx.from, [
          { transform: 'perspective(1600px) none', opacity: 1 },
          { transform: 'perspective(1600px) rotateX(25deg) scale(.7)', opacity: 1, offset: 0.3 },
          { transform: `perspective(1600px) rotateX(50deg) rotateZ(${-s * 20}deg) scale(.35, .55)`, opacity: 1, offset: 0.55 },
          { transform: `perspective(1600px) translate(${s * ctx.W * 1.1}px, ${-ctx.H}px) rotateX(50deg) rotateZ(${-s * 35}deg) scale(.12)`, opacity: 0 },
        ], { easing: 'ease-in-out' }),
        wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 0, offset: 0.4 }, { opacity: 1 }]),
      ]);
    },
  },
  {
    id: 'origami', name: 'Origami', category: 'Exciting', directions: [{ value: 'left', label: 'Left' }, { value: 'right', label: 'Right' }],
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const s = ctx.dir === 'right' ? 1 : -1;
      const n = 6;
      const t = tiles(ctx, ctx.from, n, 1);
      hide(ctx.from);
      return all([
        wa(ctx, ctx.to, [{ opacity: 0, transform: 'scale(.9)' }, { opacity: 0, offset: 0.35 }, { opacity: 1, transform: 'none' }]),
        ...t.map((tile, i) => {
          tile.el.style.transformOrigin = i % 2 === 0 ? '0% 50%' : '100% 50%';
          const target = s > 0 ? ctx.W - tile.cx : -tile.cx;
          return wa(ctx, tile.el, [
            { transform: 'perspective(1400px) none', filter: 'brightness(1)', opacity: 1 },
            { transform: `perspective(1400px) translateX(${target * 0.5}px) rotateY(${i % 2 ? -80 : 80}deg)`, filter: 'brightness(.6)', opacity: 1, offset: 0.6 },
            { transform: `perspective(1400px) translate(${target}px, ${-ctx.H * 0.6}px) rotateY(${i % 2 ? -90 : 90}deg) scale(.3)`, filter: 'brightness(.6)', opacity: 0 },
          ], { delay: (s > 0 ? n - 1 - i : i) * ctx.duration * 0.04 });
        }),
      ]);
    },
  },
  {
    id: 'checkerboard', name: 'Checkerboard', category: 'Exciting', directions: [{ value: 'across', label: 'From left' }, { value: 'down', label: 'From top' }],
    run: (ctx) => {
      const down = ctx.dir === 'down';
      const cols = 12, rows = Math.max(2, Math.round((12 * ctx.H) / ctx.W));
      const cw = ctx.W / cols, ch = ctx.H / rows;
      return clipTween(ctx, (p) => {
        let d = '';
        for (let r = 0; r < rows; r++) {
          for (let c = 0; c < cols; c++) {
            const major = down ? r : c, minor = down ? c : r;
            const count = down ? rows : cols;
            const start = ((major + (minor % 2) * 0.5) / (count + 0.5)) * 0.75;
            const lp = clamp((p - start) / 0.25, 0, 1);
            if (lp <= 0) continue;
            d += down ? rect(c * cw, r * ch, cw + 0.5, ch * lp + 0.5) : rect(c * cw, r * ch, cw * lp + 0.5, ch + 0.5);
          }
        }
        return d;
      });
    },
  },
  {
    id: 'blinds', name: 'Blinds', category: 'Exciting', directions: ORIENT,
    run: (ctx) => {
      const n = 10;
      const vert = ctx.dir === 'vertical';
      const size = (vert ? ctx.W : ctx.H) / n;
      return clipTween(ctx, (p) => {
        let d = '';
        for (let i = 0; i < n; i++) d += vert ? rect(i * size, 0, size * p + 0.5, ctx.H) : rect(0, i * size, ctx.W, size * p + 0.5);
        return d;
      });
    },
  },
  {
    id: 'clock', name: 'Clock', category: 'Exciting',
    directions: [{ value: 'clockwise', label: 'Clockwise' }, { value: 'counter', label: 'Counterclockwise' }, { value: 'wedge', label: 'Wedge' }],
    run: (ctx) => {
      const R = Math.hypot(ctx.W, ctx.H);
      const cx = ctx.W / 2, cy = ctx.H / 2;
      const top = -Math.PI / 2;
      return clipTween(ctx, (p) => {
        const sweep = p * Math.PI * 2;
        if (ctx.dir === 'wedge') return wedgePath(cx, cy, R, top - sweep / 2, top + sweep / 2);
        if (ctx.dir === 'counter') return wedgePath(cx, cy, R, top - sweep, top);
        return wedgePath(cx, cy, R, top, top + sweep);
      });
    },
  },
  {
    id: 'wheel', name: 'Wheel', category: 'Exciting',
    directions: [1, 2, 3, 4, 8].map((n) => ({ value: String(n), label: `${n} spoke${n > 1 ? 's' : ''}` })),
    run: (ctx) => {
      const n = Number(ctx.dir) || 4;
      const R = Math.hypot(ctx.W, ctx.H);
      return clipTween(ctx, (p) => {
        let d = '';
        const seg = (Math.PI * 2) / n;
        for (let i = 0; i < n; i++) {
          const a0 = -Math.PI / 2 + i * seg;
          d += wedgePath(ctx.W / 2, ctx.H / 2, R, a0, a0 + seg * p);
        }
        return d;
      });
    },
  },
  {
    id: 'ripple', name: 'Ripple', category: 'Exciting',
    directions: [{ value: 'center', label: 'Center' }, ...CORNERS],
    run: (ctx) => {
      const origin = { center: '50% 50%', tl: '0% 0%', tr: '100% 0%', bl: '0% 100%', br: '100% 100%' }[ctx.dir] ?? '50% 50%';
      const [ox, oy] = origin.split(' ').map((v) => parseFloat(v) / 100);
      const R = Math.hypot(ctx.W, ctx.H) * (ctx.dir === 'center' ? 0.55 : 1.05);
      const rings = Array.from({ length: 4 }, (_, i) => {
        const ring = overlay(ctx, '');
        ring.style.cssText = `position:absolute;left:${ox * ctx.W - R}px;top:${oy * ctx.H - R}px;width:${R * 2}px;height:${R * 2}px;border-radius:50%;border:${Math.max(2, ctx.W / 200)}px solid rgba(255,255,255,.6);box-shadow:0 0 ${ctx.W / 60}px rgba(255,255,255,.5);`;
        return wa(ctx, ring, [{ transform: 'scale(0)', opacity: 0.9 }, { transform: 'scale(1)', opacity: 0 }], { delay: i * ctx.duration * 0.12, duration: ctx.duration * 0.7, easing: 'ease-out' });
      });
      return all([
        ...rings,
        wa(ctx, ctx.to, [
          { clipPath: `circle(0px at ${origin})`, transform: 'scale(1.06)' },
          { clipPath: `circle(${R}px at ${origin})`, transform: 'scale(1)' },
        ], { easing: 'ease-out' }),
      ]);
    },
  },
  {
    id: 'honeycomb', name: 'Honeycomb', category: 'Exciting',
    run: (ctx) => {
      const R = ctx.H / 8;
      const cells: { x: number; y: number; t: number }[] = [];
      const rand = rng(41);
      for (let c = 0; c * R * 1.5 < ctx.W + R * 2; c++) {
        for (let r = 0; r * R * Math.sqrt(3) < ctx.H + R * 2; r++) {
          cells.push({ x: c * R * 1.5, y: r * R * Math.sqrt(3) + (c % 2 ? (R * Math.sqrt(3)) / 2 : 0), t: rand() * 0.7 });
        }
      }
      return all([
        ctx.from ? wa(ctx, ctx.from, [{ filter: 'brightness(1)' }, { filter: 'brightness(.4)' }]) : Promise.resolve(),
        clipTween(ctx, (p) => cells.map((cell) => hexPath(cell.x, cell.y, R * 1.02 * clamp((p - cell.t) / 0.3, 0, 1))).join('')),
      ]);
    },
  },
  {
    id: 'glitter', name: 'Glitter', category: 'Exciting',
    directions: [
      { value: 'right-diamond', label: 'Diamonds from right' }, { value: 'left-diamond', label: 'Diamonds from left' },
      { value: 'right-hex', label: 'Hexagons from right' }, { value: 'left-hex', label: 'Hexagons from left' },
      { value: 'top-diamond', label: 'Diamonds from top' }, { value: 'bottom-diamond', label: 'Diamonds from bottom' },
    ],
    run: (ctx) => {
      const [side, kind] = ctx.dir.split('-');
      const size = ctx.W / 30;
      const rand = rng(53);
      const cells: { x: number; y: number; t: number }[] = [];
      for (let y = 0; y < ctx.H + size; y += size) {
        for (let x = 0; x < ctx.W + size; x += size) {
          const along = side === 'right' ? 1 - x / ctx.W : side === 'left' ? x / ctx.W : side === 'top' ? y / ctx.H : 1 - y / ctx.H;
          cells.push({ x, y, t: clamp(along, 0, 1) * 0.6 + rand() * 0.25 });
        }
      }
      const shape = kind === 'hex' ? hexPath : diamondPath;
      return clipTween(ctx, (p) => cells.map((c) => shape(c.x, c.y, size * 0.75 * clamp((p - c.t) / 0.15, 0, 1))).join(''));
    },
  },
  {
    id: 'vortex', name: 'Vortex', category: 'Exciting', directions: DIR4,
    run: (ctx) => {
      const [bx, by] = off(ctx, ctx.dir);
      const t = tiles(ctx, ctx.to, 10, 6);
      hide(ctx.to);
      const rand = rng(61);
      const D = ctx.duration;
      return all([
        ctx.from ? wa(ctx, ctx.from, [{ transform: 'none', filter: 'blur(0px)', opacity: 1 }, { transform: 'scale(.85) rotate(-8deg)', filter: `blur(${ctx.W / 200}px)`, opacity: 0 }]) : Promise.resolve(),
        ...t.map((tile) => {
          const a = rand() * Math.PI * 2;
          const rad = Math.max(ctx.W, ctx.H) * (0.3 + rand() * 0.5);
          return wa(ctx, tile.el, [
            { transform: `translate(${bx * 0.6 + Math.cos(a) * rad}px, ${by * 0.6 + Math.sin(a) * rad}px) rotate(${(rand() - 0.5) * 900}deg) scale(${0.1 + rand() * 0.4})`, opacity: 0 },
            { opacity: 1, offset: 0.4 },
            { transform: 'none', opacity: 1 },
          ], { duration: D * 0.65, delay: rand() * D * 0.35, easing: 'ease-out' });
        }),
      ]);
    },
  },
  {
    id: 'shred', name: 'Shred', category: 'Exciting',
    directions: [{ value: 'strips', label: 'Strips' }, { value: 'particles', label: 'Particles' }],
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const particles = ctx.dir === 'particles';
      const t = particles ? tiles(ctx, ctx.from, 16, 9) : tiles(ctx, ctx.from, 16, 1);
      hide(ctx.from);
      const rand = rng(71);
      return all([
        wa(ctx, ctx.to, [{ opacity: 0.4, transform: 'scale(.96)' }, { opacity: 1, transform: 'none' }]),
        ...t.map((tile, i) => {
          if (particles) {
            return wa(ctx, tile.el, [
              { transform: 'none', opacity: 1 },
              { transform: `translate(${(rand() - 0.5) * ctx.W * 0.3}px, ${ctx.H * (0.8 + rand())}px) rotate(${(rand() - 0.5) * 540}deg) scale(.5)`, opacity: 0 },
            ], { delay: rand() * ctx.duration * 0.4, duration: ctx.duration * 0.6, easing: 'ease-in' });
          }
          const dirY = i % 2 ? 1 : -1;
          return wa(ctx, tile.el, [
            { transform: 'none' },
            { transform: `translateY(${dirY * ctx.H * 1.1}px) rotate(${dirY * (rand() * 6)}deg)` },
          ], { delay: i * ctx.duration * 0.02, duration: ctx.duration * 0.7, easing: 'ease-in' });
        }),
      ]);
    },
  },
  {
    id: 'switch', name: 'Switch', category: 'Exciting', directions: DIR2,
    run: (ctx) => {
      const s = sgn(ctx.dir);
      const p = 'perspective(1600px)';
      return all([
        ctx.from ? wa(ctx, ctx.from, [
          { transform: `${p} none`, zIndex: 3 },
          { transform: `${p} translateX(${-s * 55}%) rotateY(${s * 30}deg) scale(.7)`, zIndex: 3, offset: 0.5 },
          { transform: `${p} translateZ(-600px) scale(.6)`, opacity: 0, zIndex: 1 },
        ]) : Promise.resolve(),
        wa(ctx, ctx.to, [
          { transform: `${p} translateZ(-600px) scale(.6)`, opacity: 0, zIndex: 1 },
          { transform: `${p} translateX(${s * 55}%) rotateY(${-s * 30}deg) scale(.7)`, opacity: 1, zIndex: 1, offset: 0.5 },
          { transform: `${p} none`, opacity: 1, zIndex: 4 },
        ]),
      ]);
    },
  },
  {
    id: 'flip', name: 'Flip', category: 'Exciting', directions: DIR2,
    run: (ctx) => {
      const s = sgn(ctx.dir);
      const p = 'perspective(2400px)';
      for (const el of [ctx.from, ctx.to]) if (el) el.style.backfaceVisibility = 'hidden';
      return all([
        ctx.from ? wa(ctx, ctx.from, [
          { transform: `${p} rotateY(0deg) scale(1)` },
          { transform: `${p} rotateY(${-s * 90}deg) scale(.8)`, offset: 0.5 },
          { transform: `${p} rotateY(${-s * 180}deg) scale(1)` },
        ]) : Promise.resolve(),
        wa(ctx, ctx.to, [
          { transform: `${p} rotateY(${s * 180}deg) scale(1)` },
          { transform: `${p} rotateY(${s * 90}deg) scale(.8)`, offset: 0.5 },
          { transform: `${p} rotateY(0deg) scale(1)` },
        ]),
      ]);
    },
  },
  {
    id: 'gallery', name: 'Gallery', category: 'Exciting', directions: DIR2,
    run: (ctx) => {
      const s = sgn(ctx.dir);
      const p = 'perspective(1800px)';
      return all([
        ctx.from ? wa(ctx, ctx.from, [
          { transform: `${p} none` },
          { transform: `${p} translateX(${-s * 30}%) rotateY(${s * 25}deg) scale(.8)`, offset: 0.35 },
          { transform: `${p} translateX(${-s * 115}%) rotateY(${s * 25}deg) scale(.8)` },
        ]) : Promise.resolve(),
        wa(ctx, ctx.to, [
          { transform: `${p} translateX(${s * 115}%) rotateY(${-s * 25}deg) scale(.8)` },
          { transform: `${p} translateX(${s * 30}%) rotateY(${-s * 25}deg) scale(.8)`, offset: 0.65 },
          { transform: `${p} none` },
        ]),
      ]);
    },
  },
  { id: 'cube', name: 'Cube', category: 'Exciting', directions: DIR4, run: (ctx) => cube(ctx, 0) },
  { id: 'box', name: 'Box', category: 'Exciting', directions: DIR4, run: (ctx) => cube(ctx, 0.6) },
  {
    id: 'doors', name: 'Doors', category: 'Exciting', directions: ORIENT,
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const vert = ctx.dir === 'horizontal';
      const t = vert ? tiles(ctx, ctx.from, 1, 2) : tiles(ctx, ctx.from, 2, 1);
      hide(ctx.from);
      const axis = vert ? 'rotateX' : 'rotateY';
      const p = 'perspective(1600px)';
      t[0].el.style.transformOrigin = vert ? '50% 0%' : '0% 50%';
      t[1].el.style.transformOrigin = vert ? '50% 100%' : '100% 50%';
      return all([
        wa(ctx, ctx.to, [{ transform: 'scale(.8)', opacity: 0.3 }, { transform: 'none', opacity: 1 }]),
        wa(ctx, t[0].el, [{ transform: `${p} ${axis}(0deg)`, filter: 'brightness(1)', opacity: 1 }, { transform: `${p} ${axis}(${vert ? 95 : -95}deg)`, filter: 'brightness(.5)', opacity: 0 }]),
        wa(ctx, t[1].el, [{ transform: `${p} ${axis}(0deg)`, filter: 'brightness(1)', opacity: 1 }, { transform: `${p} ${axis}(${vert ? -95 : 95}deg)`, filter: 'brightness(.5)', opacity: 0 }]),
      ]);
    },
  },
  {
    id: 'comb', name: 'Comb', category: 'Exciting', directions: ORIENT,
    run: (ctx) => {
      const n = 12;
      const vert = ctx.dir === 'vertical';
      const size = (vert ? ctx.W : ctx.H) / n;
      return clipTween(ctx, (p) => {
        let d = '';
        for (let i = 0; i < n; i++) {
          if (vert) d += i % 2 ? rect(i * size, ctx.H * (1 - p), size + 0.5, ctx.H * p) : rect(i * size, 0, size + 0.5, ctx.H * p);
          else d += i % 2 ? rect(ctx.W * (1 - p), i * size, ctx.W * p, size + 0.5) : rect(0, i * size, ctx.W * p, size + 0.5);
        }
        return d;
      });
    },
  },
  {
    id: 'zoom', name: 'Zoom', category: 'Exciting',
    directions: [{ value: 'in', label: 'In' }, { value: 'out', label: 'Out' }, { value: 'rotate', label: 'Zoom and rotate' }],
    run: (ctx) => {
      if (ctx.dir === 'rotate') {
        return all([
          wa(ctx, ctx.to, [{ transform: 'scale(0) rotate(-720deg)', opacity: 0 }, { transform: 'none', opacity: 1 }]),
          ctx.from ? wa(ctx, ctx.from, [{ opacity: 1 }, { opacity: 0.3 }]) : Promise.resolve(),
        ]);
      }
      const inward = ctx.dir !== 'out';
      return all([
        wa(ctx, ctx.to, [{ transform: inward ? 'scale(.3)' : 'scale(1.7)', opacity: 0 }, { transform: 'none', opacity: 1 }]),
        ctx.from ? wa(ctx, ctx.from, [{ transform: 'none', opacity: 1 }, { transform: inward ? 'scale(1.7)' : 'scale(.3)', opacity: 0 }]) : Promise.resolve(),
      ]);
    },
  },
  {
    id: 'diagonal', name: 'Strips', category: 'Exciting', directions: CORNERS,
    run: (ctx) => {
      const k = ctx.W + ctx.H;
      const { W, H } = ctx;
      const shapes: Record<string, [string, string]> = {
        tl: ['polygon(0px 0px, 0px 0px, 0px 0px)', `polygon(0px 0px, ${k}px 0px, 0px ${k}px)`],
        tr: [`polygon(${W}px 0px, ${W}px 0px, ${W}px 0px)`, `polygon(${W}px 0px, ${W - k}px 0px, ${W}px ${k}px)`],
        bl: [`polygon(0px ${H}px, 0px ${H}px, 0px ${H}px)`, `polygon(0px ${H}px, ${k}px ${H}px, 0px ${H - k}px)`],
        br: [`polygon(${W}px ${H}px, ${W}px ${H}px, ${W}px ${H}px)`, `polygon(${W}px ${H}px, ${W - k}px ${H}px, ${W}px ${H - k}px)`],
      };
      const [a, b] = shapes[ctx.dir] ?? shapes.tl;
      return wa(ctx, ctx.to, [{ clipPath: a }, { clipPath: b }]);
    },
  },
  {
    id: 'static', name: 'TV static', category: 'Exciting',
    run: (ctx) => {
      const cols = 40, rows = Math.max(1, Math.round((40 * ctx.H) / ctx.W));
      const cw = ctx.W / cols, ch = ctx.H / rows;
      let seed = 1;
      return clipTween(ctx, (p) => {
        const rand = rng(seed++);
        let d = '';
        for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) if (rand() < p) d += rect(c * cw, r * ch, cw + 0.5, ch + 0.5);
        return d;
      });
    },
  },
  {
    id: 'tile-flip', name: 'Tile flip', category: 'Exciting', directions: CORNERS,
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const cols = 8, rows = Math.max(2, Math.round((8 * ctx.H) / ctx.W));
      const back = tiles(ctx, ctx.to, cols, rows);
      const front = tiles(ctx, ctx.from, cols, rows);
      hide(ctx.from);
      hide(ctx.to);
      const p = 'perspective(1200px)';
      const flipX = ctx.dir === 'tr' || ctx.dir === 'br';
      const flipY = ctx.dir === 'bl' || ctx.dir === 'br';
      const tileDur = ctx.duration * 0.5;
      return all(front.flatMap((f, i) => {
        const b = back[i];
        const c = flipX ? cols - 1 - f.c : f.c;
        const r = flipY ? rows - 1 - f.r : f.r;
        const delay = ((c + r) / (cols + rows - 2)) * ctx.duration * 0.5;
        return [
          wa(ctx, f.el, [{ transform: `${p} rotateY(0deg)` }, { transform: `${p} rotateY(180deg)` }], { delay, duration: tileDur }),
          wa(ctx, b.el, [{ transform: `${p} rotateY(-180deg)` }, { transform: `${p} rotateY(0deg)` }], { delay, duration: tileDur }),
        ];
      }));
    },
  },

  /* ---------------- Dynamic content ---------------- */
  {
    id: 'pan', name: 'Pan', category: 'Dynamic content', directions: DIR4,
    run: (ctx) => {
      const [x, y] = off(ctx, ctx.dir);
      return all([
        ctx.from ? wa(ctx, ctx.from, [
          { transform: 'none' }, { transform: `${tr(-x * 0.5, -y * 0.5)} scale(.88)`, offset: 0.5 }, { transform: tr(-x, -y) },
        ]) : Promise.resolve(),
        wa(ctx, ctx.to, [
          { transform: tr(x, y) }, { transform: `${tr(x * 0.5, y * 0.5)} scale(.88)`, offset: 0.5 }, { transform: 'none' },
        ]),
      ]);
    },
  },
  {
    id: 'ferris', name: 'Ferris wheel', category: 'Dynamic content', directions: DIR2,
    run: (ctx) => {
      const s = sgn(ctx.dir);
      for (const el of [ctx.from, ctx.to]) if (el) el.style.transformOrigin = '50% 280%';
      return all([
        ctx.from ? wa(ctx, ctx.from, [{ transform: 'rotate(0deg)' }, { transform: `rotate(${-s * 30}deg)` }]) : Promise.resolve(),
        wa(ctx, ctx.to, [{ transform: `rotate(${s * 30}deg)` }, { transform: 'rotate(0deg)' }]),
      ]);
    },
  },
  {
    id: 'conveyor', name: 'Conveyor', category: 'Dynamic content', directions: DIR2,
    run: (ctx) => {
      const s = sgn(ctx.dir);
      const p = 'perspective(1800px)';
      const back = (x: number, ry: number) => `${p} translateX(${x}%) translateZ(-350px) rotateY(${ry}deg)`;
      return all([
        ctx.from ? wa(ctx, ctx.from, [
          { transform: `${p} none` },
          { transform: back(0, s * 15), offset: 0.25 },
          { transform: back(-s * 115, s * 15), offset: 0.8 },
          { transform: back(-s * 115, s * 15) },
        ]) : Promise.resolve(),
        wa(ctx, ctx.to, [
          { transform: back(s * 115, -s * 15) },
          { transform: back(s * 115, -s * 15), offset: 0.2 },
          { transform: back(0, -s * 15), offset: 0.75 },
          { transform: `${p} none` },
        ]),
      ]);
    },
  },
  {
    id: 'rotate', name: 'Rotate', category: 'Dynamic content', directions: DIR4,
    run: (ctx) => {
      const s = sgn(ctx.dir);
      const axis = isVertical(ctx.dir) ? 'rotateX' : 'rotateY';
      const p = 'perspective(2000px)';
      const a = isVertical(ctx.dir) ? -s * 90 : s * 90;
      for (const el of [ctx.from, ctx.to]) if (el) el.style.backfaceVisibility = 'hidden';
      return all([
        ctx.from ? wa(ctx, ctx.from, [
          { transform: `${p} ${axis}(0deg)`, opacity: 1 }, { transform: `${p} ${axis}(${-a}deg)`, opacity: 1, offset: 0.5 }, { transform: `${p} ${axis}(${-a}deg)`, opacity: 0 },
        ]) : Promise.resolve(),
        wa(ctx, ctx.to, [
          { transform: `${p} ${axis}(${a}deg)`, opacity: 0 }, { transform: `${p} ${axis}(${a}deg)`, opacity: 1, offset: 0.5 }, { transform: `${p} ${axis}(0deg)`, opacity: 1 },
        ]),
      ]);
    },
  },
  {
    id: 'window', name: 'Window', category: 'Dynamic content', directions: ORIENT,
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const vert = ctx.dir === 'horizontal';
      const t = vert ? tiles(ctx, ctx.from, 1, 2) : tiles(ctx, ctx.from, 2, 1);
      hide(ctx.from);
      const axis = vert ? 'rotateX' : 'rotateY';
      const p = 'perspective(1400px)';
      t[0].el.style.transformOrigin = vert ? '50% 0%' : '0% 50%';
      t[1].el.style.transformOrigin = vert ? '50% 100%' : '100% 50%';
      return all([
        wa(ctx, ctx.to, [{ transform: 'perspective(1400px) translateZ(-900px)', opacity: 0 }, { opacity: 1, offset: 0.4 }, { transform: 'perspective(1400px) translateZ(0px)', opacity: 1 }]),
        wa(ctx, t[0].el, [{ transform: `${p} ${axis}(0deg)` }, { transform: `${p} ${axis}(${vert ? -88 : 88}deg)`, opacity: 0 }]),
        wa(ctx, t[1].el, [{ transform: `${p} ${axis}(0deg)` }, { transform: `${p} ${axis}(${vert ? 88 : -88}deg)`, opacity: 0 }]),
      ]);
    },
  },
  {
    id: 'orbit', name: 'Orbit', category: 'Dynamic content', directions: DIR2,
    run: (ctx) => {
      const s = sgn(ctx.dir);
      const p = 'perspective(2400px)';
      for (const el of [ctx.from, ctx.to]) if (el) el.style.transformOrigin = `50% 50% -${ctx.W * 0.8}px`;
      return all([
        ctx.from ? wa(ctx, ctx.from, [{ transform: `${p} rotateY(0deg)`, opacity: 1 }, { transform: `${p} rotateY(${-s * 70}deg)`, opacity: 0 }]) : Promise.resolve(),
        wa(ctx, ctx.to, [{ transform: `${p} rotateY(${s * 70}deg)`, opacity: 0 }, { transform: `${p} rotateY(0deg)`, opacity: 1 }]),
      ]);
    },
  },
  {
    id: 'fly-through', name: 'Fly through', category: 'Dynamic content',
    directions: [{ value: 'in', label: 'In' }, { value: 'out', label: 'Out' }, { value: 'in-bounce', label: 'In with bounce' }, { value: 'out-bounce', label: 'Out with bounce' }],
    run: (ctx) => {
      const out = ctx.dir.startsWith('out');
      const bounce = ctx.dir.endsWith('bounce');
      const start = out ? 'scale(2.6)' : 'scale(.2)';
      const end = out ? 'scale(.2)' : 'scale(2.6)';
      return all([
        ctx.from ? wa(ctx, ctx.from, [{ transform: 'none', opacity: 1 }, { transform: end, opacity: 0 }], { duration: ctx.duration * 0.8 }) : Promise.resolve(),
        wa(ctx, ctx.to, [
          { transform: start, opacity: 0 },
          ...(bounce ? [{ transform: out ? 'scale(.94)' : 'scale(1.08)', opacity: 1, offset: 0.75 }] : []),
          { transform: 'none', opacity: 1 },
        ]),
      ]);
    },
  },

  /* ---------------- Creative (Canva, Impress and Slides favourites) ---------------- */
  {
    id: 'color-wipe', name: 'Colour wipe', category: 'Creative', directions: DIR4,
    run: (ctx) => {
      const [x, y] = off(ctx, ctx.dir);
      const panel = overlay(ctx, `background:${ctx.accent};`);
      const e = easingCss(ctx.easing);
      return all([
        wa(ctx, panel, [
          { transform: tr(x, y), easing: e }, { transform: 'none', offset: 0.5, easing: e }, { transform: tr(-x, -y) },
        ], { easing: 'linear' }),
        wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 0, offset: 0.5 }, { opacity: 1, offset: 0.501 }, { opacity: 1 }], { easing: 'linear' }),
      ]);
    },
  },
  {
    id: 'line-wipe', name: 'Line wipe', category: 'Creative', directions: DIR4,
    run: (ctx) => {
      const thick = Math.max(3, ctx.W / 160);
      const vert = isVertical(ctx.dir);
      const line = overlay(ctx, `background:${ctx.accent};box-shadow:0 0 ${thick * 3}px ${ctx.accent};`);
      line.style.width = vert ? `${ctx.W}px` : `${thick}px`;
      line.style.height = vert ? `${thick}px` : `${ctx.H}px`;
      const startEnd: Record<string, [string, string]> = {
        right: [tr(ctx.W, 0), tr(-thick, 0)], left: [tr(-thick, 0), tr(ctx.W, 0)],
        bottom: [tr(0, ctx.H), tr(0, -thick)], top: [tr(0, -thick), tr(0, ctx.H)],
      };
      const [a, b] = startEnd[ctx.dir] ?? startEnd.right;
      return all([
        wa(ctx, line, [{ transform: a }, { transform: b }]),
        wa(ctx, ctx.to, [{ clipPath: insetStart(ctx.dir) }, { clipPath: 'inset(0 0 0 0)' }]),
      ]);
    },
  },
  {
    id: 'stack', name: 'Stack', category: 'Creative', directions: DIR4,
    run: (ctx) => {
      const [x, y] = off(ctx, ctx.dir);
      return all([
        wa(ctx, ctx.to, [
          { transform: `${tr(x * 1.05, y * 1.05)} scale(1.04)`, filter: 'drop-shadow(0 0 0 rgba(0,0,0,0))' },
          { transform: 'scale(1.02)', filter: `drop-shadow(0 ${ctx.H / 30}px ${ctx.H / 20}px rgba(0,0,0,.45))`, offset: 0.8 },
          { transform: 'none', filter: 'drop-shadow(0 0 0 rgba(0,0,0,0))' },
        ]),
        ctx.from ? wa(ctx, ctx.from, [{ transform: 'none', filter: 'brightness(1)' }, { transform: 'scale(.9)', filter: 'brightness(.55)' }]) : Promise.resolve(),
      ]);
    },
  },
  {
    id: 'chop', name: 'Chop', category: 'Creative',
    directions: [{ value: 'diagonal', label: 'Diagonal' }, { value: 'horizontal', label: 'Horizontal' }, { value: 'vertical', label: 'Vertical' }],
    run: (ctx) => {
      if (!ctx.from) return wa(ctx, ctx.to, [{ opacity: 0 }, { opacity: 1 }]);
      const a = cloneLayer(ctx, ctx.from);
      const b = cloneLayer(ctx, ctx.from);
      ctx.fx.append(a, b);
      hide(ctx.from);
      let clipA = 'polygon(0 0, 100% 0, 0 100%)', clipB = 'polygon(100% 0, 100% 100%, 0 100%)';
      let moveA = `translate(${-ctx.W * 0.7}px, ${-ctx.H * 0.7}px) rotate(-12deg)`, moveB = `translate(${ctx.W * 0.7}px, ${ctx.H * 0.7}px) rotate(12deg)`;
      if (ctx.dir === 'horizontal') {
        clipA = 'inset(0 0 50% 0)'; clipB = 'inset(50% 0 0 0)';
        moveA = `translateY(${-ctx.H * 0.8}px) rotate(-6deg)`; moveB = `translateY(${ctx.H * 0.8}px) rotate(6deg)`;
      } else if (ctx.dir === 'vertical') {
        clipA = 'inset(0 50% 0 0)'; clipB = 'inset(0 0 0 50%)';
        moveA = `translateX(${-ctx.W * 0.8}px) rotate(-6deg)`; moveB = `translateX(${ctx.W * 0.8}px) rotate(6deg)`;
      }
      a.style.clipPath = clipA;
      b.style.clipPath = clipB;
      return all([
        wa(ctx, ctx.to, [{ transform: 'scale(.92)', opacity: 0.5 }, { transform: 'none', opacity: 1 }]),
        wa(ctx, a, [{ transform: 'none' }, { transform: moveA }], { easing: 'ease-in' }),
        wa(ctx, b, [{ transform: 'none' }, { transform: moveB }], { easing: 'ease-in' }),
      ]);
    },
  },
  {
    id: 'blur', name: 'Blur', category: 'Creative',
    run: (ctx) => all([
      wa(ctx, ctx.to, [{ opacity: 0, filter: `blur(${ctx.W / 60}px)` }, { opacity: 1, filter: 'blur(0px)' }]),
      ctx.from ? wa(ctx, ctx.from, [{ filter: 'blur(0px)' }, { filter: `blur(${ctx.W / 60}px)` }]) : Promise.resolve(),
    ]),
  },
  {
    id: 'iris', name: 'Iris', category: 'Creative',
    directions: [{ value: 'out', label: 'Out' }, { value: 'in', label: 'In' }],
    run: (ctx) => {
      const r = Math.min(ctx.W, ctx.H) * 0.08;
      const big = `inset(-5% -5% -5% -5% round ${r}px)`;
      const small = `inset(50% 50% 50% 50% round ${r}px)`;
      if (ctx.dir === 'in' && ctx.from) {
        ctx.from.style.zIndex = '3';
        return wa(ctx, ctx.from, [{ clipPath: big }, { clipPath: small }]);
      }
      return wa(ctx, ctx.to, [{ clipPath: small }, { clipPath: big }]);
    },
  },
  {
    id: 'rochade', name: 'Rochade', category: 'Creative', directions: DIR2,
    run: (ctx) => {
      const s = sgn(ctx.dir);
      const p = 'perspective(1800px)';
      return all([
        ctx.from ? wa(ctx, ctx.from, [
          { transform: `${p} none`, zIndex: 3 },
          { transform: `${p} translateX(${-s * 60}%) translateZ(-500px)`, zIndex: 3, offset: 0.5 },
          { transform: `${p} translateZ(-900px)`, opacity: 0, zIndex: 1 },
        ]) : Promise.resolve(),
        wa(ctx, ctx.to, [
          { transform: `${p} translateZ(-900px)`, opacity: 0, zIndex: 1 },
          { transform: `${p} translateX(${s * 60}%) translateZ(-500px)`, opacity: 1, zIndex: 1, offset: 0.5 },
          { transform: `${p} none`, zIndex: 4 },
        ]),
      ]);
    },
  },
  {
    id: 'random', name: 'Random', category: 'Creative',
    run: (ctx) => {
      const pool = TRANSITIONS.filter((t) => !['none', 'random', 'morph'].includes(t.id));
      const def = pool[Math.floor(Math.random() * pool.length)];
      const dirs = def.directions;
      return def.run({ ...ctx, dir: dirs ? dirs[Math.floor(Math.random() * dirs.length)].value : '' });
    },
  },
];

export function getTransition(id: string): TransitionDef {
  return TRANSITIONS.find((t) => t.id === id) ?? TRANSITIONS[0];
}

export const TRANSITION_CATEGORIES: TransitionCategory[] = ['Subtle', 'Exciting', 'Dynamic content', 'Creative'];

/**
 * Runs a transition and always restores both layers afterwards, so the
 * incoming slide ends exactly as it is drawn without any transition.
 */
export async function runTransition(ctx: TransitionCtx, id: string): Promise<void> {
  const def = getTransition(id);
  const layers = [ctx.from, ctx.to].filter(Boolean) as HTMLElement[];
  const saved = layers.map((l) => l.style.cssText);
  const dir = ctx.dir || def.directions?.[0]?.value || '';
  try {
    await def.run({ ...ctx, dir });
  } catch (err) {
    console.error('Transition failed', err);
  } finally {
    layers.forEach((l, i) => {
      l.getAnimations().forEach((a) => a.cancel());
      l.style.cssText = saved[i];
    });
    ctx.fx.querySelectorAll('*').forEach((n) => n.getAnimations().forEach((a) => a.cancel()));
    ctx.fx.replaceChildren();
    ctx.cleanups.forEach((fn) => fn());
    ctx.cleanups.length = 0;
  }
}

/* ------------------------------------------------------------------ */
/* Morph: matches elements by id (duplicated slides keep ids) or name. */
/* ------------------------------------------------------------------ */

function runMorph(ctx: TransitionCtx): Promise<void> {
  const { from, to, fromSlide, toSlide } = ctx;
  if (!from || !fromSlide || !toSlide) return wa(ctx, to, [{ opacity: 0 }, { opacity: 1 }]);
  const fromById = new Map(fromSlide.elements.map((e) => [e.id, e]));
  const fromByName = new Map(fromSlide.elements.filter((e) => e.name).map((e) => [e.name as string, e]));
  const used = new Set<string>();
  const jobs: Promise<void>[] = [];
  const D = ctx.duration;

  // The old background fades out underneath the incoming elements.
  const content = to.querySelector('.slide-content') as HTMLElement | null;
  const fromContent = from.querySelector('.slide-content') as HTMLElement | null;
  if (content && fromContent) {
    const bg = document.createElement('div');
    const cs = getComputedStyle(fromContent);
    bg.style.cssText = `position:absolute;inset:0;background-color:${cs.backgroundColor};background-image:${cs.backgroundImage};background-size:${cs.backgroundSize};background-position:center;`;
    content.prepend(bg);
    ctx.cleanups.push(() => bg.remove());
    jobs.push(wa(ctx, bg, [{ opacity: 1 }, { opacity: 0 }]));
  }

  for (const el of toSlide.elements) {
    const node = to.querySelector(`[data-el-id="${CSS.escape(el.id)}"]`) as HTMLElement | null;
    if (!node) continue;
    let src = fromById.get(el.id);
    if (!src || src.type !== el.type) src = el.name ? fromByName.get(el.name) : undefined;
    if (!src || used.has(src.id)) {
      jobs.push(wa(ctx, node, [{ opacity: 0 }, { opacity: 0, offset: 0.3 }, { opacity: 1 }]));
      continue;
    }
    used.add(src.id);
    const dx = src.x + src.w / 2 - (el.x + el.w / 2);
    const dy = src.y + src.h / 2 - (el.y + el.h / 2);
    const sx = src.w / Math.max(1, el.w);
    const sy = src.h / Math.max(1, el.h);
    jobs.push(wa(ctx, node, [
      { transform: `translate(${dx}px, ${dy}px) rotate(${src.rot}deg) scale(${sx}, ${sy})` },
      { transform: `rotate(${el.rot}deg)` },
    ]));
    const inner = node.querySelector('.el-anim');
    if (inner) jobs.push(wa(ctx, inner, [{ opacity: 0 }, { opacity: el.opacity }], { easing: 'linear' }));
    const oldNode = from.querySelector(`[data-el-id="${CSS.escape(src.id)}"]`) as HTMLElement | null;
    if (oldNode) {
      const ghost = oldNode.cloneNode(true) as HTMLElement;
      ctx.fx.appendChild(ghost);
      jobs.push(wa(ctx, ghost, [
        { transform: `rotate(${src.rot}deg)`, opacity: 1 },
        { transform: `translate(${-dx}px, ${-dy}px) rotate(${el.rot}deg) scale(${1 / sx}, ${1 / sy})`, opacity: 0 },
      ]));
    }
  }
  // Old elements without a partner fade away.
  for (const el of fromSlide.elements) {
    if (used.has(el.id)) continue;
    const oldNode = from.querySelector(`[data-el-id="${CSS.escape(el.id)}"]`) as HTMLElement | null;
    if (!oldNode) continue;
    const ghost = oldNode.cloneNode(true) as HTMLElement;
    ctx.fx.appendChild(ghost);
    jobs.push(wa(ctx, ghost, [{ opacity: 1 }, { opacity: 0, offset: 0.6 }, { opacity: 0 }], { duration: D }));
  }
  return all(jobs);
}
