import type { AnimCategory, Animation, Slide } from '../model/types';
import type { DirOption } from './transitions';
import { easingCss } from './transitions';

export interface EffectInput {
  dir: string;
  /** Element box in slide coordinates. */
  box: { x: number; y: number; w: number; h: number; rot: number };
  W: number;
  H: number;
}

export interface EffectDef {
  id: string;
  name: string;
  category: AnimCategory;
  directions?: DirOption[];
  defaultDuration?: number;
  easing?: string;
  build(i: EffectInput): Keyframe[];
}

const FROM4: DirOption[] = [
  { value: 'bottom', label: 'From bottom' }, { value: 'top', label: 'From top' },
  { value: 'left', label: 'From left' }, { value: 'right', label: 'From right' },
  { value: 'bottom-left', label: 'From bottom left' }, { value: 'top-right', label: 'From top right' },
];
const UPDOWN: DirOption[] = [{ value: 'up', label: 'Up' }, { value: 'down', label: 'Down' }];
const SPLITS: DirOption[] = [
  { value: 'vertical', label: 'Vertical out' }, { value: 'horizontal', label: 'Horizontal out' },
];
const WIPES: DirOption[] = [
  { value: 'left', label: 'From left' }, { value: 'right', label: 'From right' },
  { value: 'top', label: 'From top' }, { value: 'bottom', label: 'From bottom' },
];
const SHAPES: DirOption[] = [
  { value: 'circle', label: 'Circle' }, { value: 'diamond', label: 'Diamond' }, { value: 'box', label: 'Box' },
];

/** Rotates a slide space offset into the element's own (possibly rotated) frame. */
function local(dx: number, dy: number, rot: number): [number, number] {
  const a = (-rot * Math.PI) / 180;
  return [dx * Math.cos(a) - dy * Math.sin(a), dx * Math.sin(a) + dy * Math.cos(a)];
}

function offSlide(i: EffectInput): [number, number] {
  const { box, W, H } = i;
  let dx = 0, dy = 0;
  if (i.dir.includes('bottom')) dy = H - box.y + 20;
  if (i.dir.includes('top')) dy = -(box.y + box.h + 20);
  if (i.dir.includes('left')) dx = -(box.x + box.w + 20);
  if (i.dir.includes('right')) dx = W - box.x + 20;
  if (!dx && !dy) dy = H - box.y + 20;
  return local(dx, dy, box.rot);
}

const t = (x: number, y: number) => `translate(${x}px, ${y}px)`;

function wipeStart(dir: string) {
  switch (dir) {
    case 'right': return 'inset(0 0 0 100%)';
    case 'top': return 'inset(0 0 100% 0)';
    case 'bottom': return 'inset(100% 0 0 0)';
    default: return 'inset(0 100% 0 0)';
  }
}

function shapeClosed(dir: string) {
  if (dir === 'diamond') return ['polygon(50% 50%, 50% 50%, 50% 50%, 50% 50%)', 'polygon(50% -50%, 150% 50%, 50% 150%, -50% 50%)'];
  if (dir === 'box') return ['inset(50% 50% 50% 50%)', 'inset(0 0 0 0)'];
  return ['circle(0% at 50% 50%)', 'circle(75% at 50% 50%)'];
}

function wheelFrames(): Keyframe[] {
  const N = 24;
  const frames: Keyframe[] = [];
  for (let f = 0; f <= 8; f++) {
    const sweep = (f / 8) * 360;
    const pts = ['50% 50%'];
    for (let k = 0; k <= N; k++) {
      const a = ((-90 + (sweep * k) / N) * Math.PI) / 180;
      pts.push(`${(50 + 75 * Math.cos(a)).toFixed(2)}% ${(50 + 75 * Math.sin(a)).toFixed(2)}%`);
    }
    frames.push({ clipPath: `polygon(${pts.join(',')})`, offset: f / 8 });
  }
  return frames;
}

/** Random bars as a clip path with a fixed command structure so the browser can interpolate it. */
function barsFrames(i: EffectInput, vertical: boolean): Keyframe[] {
  const n = 14;
  const { w, h } = i.box;
  let seed = 9;
  const rand = () => ((seed = (seed * 16807) % 2147483647) - 1) / 2147483646;
  const starts = Array.from({ length: n }, () => rand() * 0.7);
  const frames: Keyframe[] = [];
  for (let f = 0; f <= 10; f++) {
    const p = f / 10;
    let d = '';
    for (let k = 0; k < n; k++) {
      const lp = Math.min(1, Math.max(0, (p - starts[k]) / 0.3));
      if (vertical) {
        const bw = w / n;
        d += `M${(k * bw).toFixed(1)} 0h${(bw * lp + 0.01).toFixed(2)}v${h}h${(-(bw * lp + 0.01)).toFixed(2)}Z`;
      } else {
        const bh = h / n;
        d += `M0 ${(k * bh).toFixed(1)}h${w}v${(bh * lp + 0.01).toFixed(2)}h${-w}Z`;
      }
    }
    frames.push({ clipPath: `path("${d}")`, offset: p });
  }
  return frames;
}

const ENTRANCES: EffectDef[] = [
  { id: 'appear', name: 'Appear', category: 'entrance', defaultDuration: 1, easing: 'linear', build: () => [{ opacity: 0 }, { opacity: 0, offset: 0.99 }, { opacity: 1 }] },
  { id: 'fade', name: 'Fade', category: 'entrance', build: () => [{ opacity: 0 }, { opacity: 1 }] },
  {
    id: 'fly', name: 'Fly in', category: 'entrance', directions: FROM4, easing: 'ease-out',
    build: (i) => { const [x, y] = offSlide(i); return [{ transform: t(x, y) }, { transform: 'none' }]; },
  },
  {
    id: 'float', name: 'Float in', category: 'entrance', directions: UPDOWN, easing: 'ease-out',
    build: (i) => [{ opacity: 0, transform: `translateY(${i.dir === 'down' ? -60 : 60}px)` }, { opacity: 1, transform: 'none' }],
  },
  {
    id: 'split', name: 'Split', category: 'entrance', directions: SPLITS,
    build: (i) => [{ clipPath: i.dir === 'horizontal' ? 'inset(50% 0 50% 0)' : 'inset(0 50% 0 50%)' }, { clipPath: 'inset(0 0 0 0)' }],
  },
  { id: 'wipe', name: 'Wipe', category: 'entrance', directions: WIPES, build: (i) => [{ clipPath: wipeStart(i.dir) }, { clipPath: 'inset(0 0 0 0)' }] },
  { id: 'shape', name: 'Shape', category: 'entrance', directions: SHAPES, build: (i) => { const [a, b] = shapeClosed(i.dir); return [{ clipPath: a }, { clipPath: b }]; } },
  { id: 'wheel', name: 'Wheel', category: 'entrance', easing: 'linear', build: () => wheelFrames() },
  {
    id: 'random-bars', name: 'Random bars', category: 'entrance', easing: 'linear',
    directions: [{ value: 'horizontal', label: 'Horizontal' }, { value: 'vertical', label: 'Vertical' }],
    build: (i) => barsFrames(i, i.dir === 'vertical'),
  },
  { id: 'grow-turn', name: 'Grow and turn', category: 'entrance', easing: 'ease-out', build: () => [{ opacity: 0, transform: 'scale(.1) rotate(-90deg)' }, { opacity: 1, transform: 'none' }] },
  { id: 'zoom', name: 'Zoom', category: 'entrance', easing: 'ease-out', build: () => [{ opacity: 0, transform: 'scale(.3)' }, { opacity: 1, transform: 'none' }] },
  { id: 'swivel', name: 'Swivel', category: 'entrance', build: () => [{ opacity: 0, transform: 'perspective(800px) rotateY(-540deg)' }, { opacity: 1, transform: 'perspective(800px) rotateY(0deg)' }] },
  {
    id: 'bounce', name: 'Bounce', category: 'entrance', defaultDuration: 1000, easing: 'linear',
    build: () => [
      { opacity: 0, transform: 'translateY(-300px)', easing: 'cubic-bezier(.55,0,1,.45)' },
      { opacity: 1, transform: 'translateY(0)', offset: 0.45, easing: 'cubic-bezier(0,.55,.45,1)' },
      { transform: 'translateY(-70px)', offset: 0.65, easing: 'cubic-bezier(.55,0,1,.45)' },
      { transform: 'translateY(0)', offset: 0.82, easing: 'cubic-bezier(0,.55,.45,1)' },
      { transform: 'translateY(-18px)', offset: 0.91, easing: 'cubic-bezier(.55,0,1,.45)' },
      { opacity: 1, transform: 'none' },
    ],
  },
  {
    id: 'drop', name: 'Drop', category: 'entrance', easing: 'ease-in',
    build: (i) => { const [x, y] = local(0, -(i.box.y + i.box.h + 20), i.box.rot); return [{ transform: t(x, y) }, { transform: 'none' }]; },
  },
  { id: 'pop', name: 'Pop', category: 'entrance', easing: 'ease-out', build: () => [{ opacity: 0, transform: 'scale(0)' }, { opacity: 1, transform: 'scale(1.15)', offset: 0.7 }, { opacity: 1, transform: 'none' }] },
  { id: 'spin-in', name: 'Spin in', category: 'entrance', easing: 'ease-out', build: () => [{ opacity: 0, transform: 'rotate(-360deg) scale(0)' }, { opacity: 1, transform: 'none' }] },
  { id: 'flip-in', name: 'Flip', category: 'entrance', easing: 'ease-out', build: () => [{ opacity: 0, transform: 'perspective(800px) rotateX(90deg)' }, { opacity: 1, transform: 'perspective(800px) rotateX(0deg)' }] },
  { id: 'rise', name: 'Rise up', category: 'entrance', easing: 'ease-out', build: () => [{ opacity: 0, transform: 'translateY(120px)' }, { opacity: 1, transform: 'translateY(-12px)', offset: 0.8 }, { opacity: 1, transform: 'none' }] },
  { id: 'blur-in', name: 'Blur', category: 'entrance', build: () => [{ opacity: 0, filter: 'blur(24px)' }, { opacity: 1, filter: 'blur(0px)' }] },
  { id: 'expand', name: 'Expand', category: 'entrance', easing: 'ease-out', build: () => [{ opacity: 0, transform: 'scaleX(0)' }, { opacity: 1, transform: 'none' }] },
  { id: 'stretch', name: 'Stretch', category: 'entrance', easing: 'ease-out', build: () => [{ transform: 'scaleY(0)' }, { transform: 'scaleY(1.1)', offset: 0.7 }, { transform: 'none' }] },
  { id: 'typewriter', name: 'Typewriter', category: 'entrance', defaultDuration: 1500, easing: 'steps(18, end)', build: () => [{ clipPath: 'inset(0 100% 0 0)' }, { clipPath: 'inset(0 0 0 0)' }] },
  {
    id: 'pan-in', name: 'Pan', category: 'entrance', directions: WIPES, easing: 'ease-out',
    build: (i) => {
      const d = 80;
      const [x, y] = local(i.dir === 'left' ? -d : i.dir === 'right' ? d : 0, i.dir === 'top' ? -d : i.dir === 'bottom' ? d : 0, i.box.rot);
      return [{ opacity: 0, transform: t(x, y) }, { opacity: 1, transform: 'none' }];
    },
  },
  { id: 'elastic', name: 'Elastic', category: 'entrance', easing: 'linear', build: () => [{ transform: 'scale(0)' }, { transform: 'scale(1.25)', offset: 0.4 }, { transform: 'scale(.9)', offset: 0.6 }, { transform: 'scale(1.05)', offset: 0.8 }, { transform: 'none' }] },
];

/** Exit effects mirror the entrance effects (played backwards). */
const EXITS: EffectDef[] = ENTRANCES.map((e) => ({
  ...e,
  id: e.id,
  name: e.name.replace(' in', ' out'),
  category: 'exit',
  easing: e.easing === 'ease-out' ? 'ease-in' : e.easing,
  directions: e.directions?.map((d) => ({ ...d, label: d.label.replace('From', 'To') })),
  build: (i: EffectInput) => {
    const frames = e.build(i);
    return frames.slice().reverse().map((f, idx, arr) => {
      const copy: Keyframe = { ...f };
      if (typeof f.offset === 'number') copy.offset = 1 - f.offset;
      else if (idx === 0) delete copy.offset;
      if (idx === arr.length - 1) delete copy.offset;
      return copy;
    });
  },
}));

const EMPHASIS: EffectDef[] = [
  { id: 'pulse', name: 'Pulse', category: 'emphasis', build: () => [{ transform: 'none' }, { transform: 'scale(1.12)' }, { transform: 'none' }] },
  { id: 'color-pulse', name: 'Colour pulse', category: 'emphasis', build: () => [{ filter: 'none' }, { filter: 'brightness(1.5) saturate(1.8) hue-rotate(25deg)' }, { filter: 'none' }] },
  { id: 'teeter', name: 'Teeter', category: 'emphasis', build: () => [{ transform: 'none' }, { transform: 'rotate(5deg)' }, { transform: 'rotate(-5deg)' }, { transform: 'rotate(5deg)' }, { transform: 'rotate(-5deg)' }, { transform: 'none' }] },
  { id: 'spin', name: 'Spin', category: 'emphasis', defaultDuration: 1500, build: () => [{ transform: 'rotate(0deg)' }, { transform: 'rotate(360deg)' }] },
  { id: 'grow', name: 'Grow or shrink', category: 'emphasis', directions: [{ value: 'grow', label: 'Grow 150%' }, { value: 'shrink', label: 'Shrink 50%' }], build: (i) => [{ transform: 'none' }, { transform: `scale(${i.dir === 'shrink' ? 0.5 : 1.5})` }] },
  { id: 'shake', name: 'Shake', category: 'emphasis', build: () => [0, -14, 14, -10, 10, -6, 6, 0].map((x) => ({ transform: `translateX(${x}px)` })) },
  { id: 'heartbeat', name: 'Heartbeat', category: 'emphasis', build: () => [{ transform: 'none' }, { transform: 'scale(1.18)', offset: 0.15 }, { transform: 'none', offset: 0.3 }, { transform: 'scale(1.18)', offset: 0.45 }, { transform: 'none' }] },
  { id: 'wobble', name: 'Wobble', category: 'emphasis', build: () => [{ transform: 'none' }, { transform: 'translateX(-25%) rotate(-5deg)', offset: 0.15 }, { transform: 'translateX(20%) rotate(3deg)', offset: 0.3 }, { transform: 'translateX(-15%) rotate(-3deg)', offset: 0.45 }, { transform: 'translateX(10%) rotate(2deg)', offset: 0.6 }, { transform: 'translateX(-5%) rotate(-1deg)', offset: 0.75 }, { transform: 'none' }] },
  { id: 'flicker', name: 'Flicker', category: 'emphasis', easing: 'linear', build: () => [1, 0.2, 1, 0.4, 1, 0.1, 1].map((o) => ({ opacity: o })) },
  { id: 'blink', name: 'Blink', category: 'emphasis', easing: 'steps(1, end)', build: () => [{ opacity: 1 }, { opacity: 0 }, { opacity: 1 }, { opacity: 0 }, { opacity: 1 }] },
  { id: 'transparency', name: 'Transparency', category: 'emphasis', build: () => [{ opacity: 1 }, { opacity: 0.4 }] },
  { id: 'desaturate', name: 'Desaturate', category: 'emphasis', build: () => [{ filter: 'grayscale(0)' }, { filter: 'grayscale(1)' }] },
  { id: 'darken', name: 'Darken', category: 'emphasis', build: () => [{ filter: 'brightness(1)' }, { filter: 'brightness(.55)' }] },
  { id: 'lighten', name: 'Lighten', category: 'emphasis', build: () => [{ filter: 'brightness(1)' }, { filter: 'brightness(1.5)' }] },
  { id: 'bold-flash', name: 'Bold flash', category: 'emphasis', build: () => [{ filter: 'none', transform: 'none' }, { filter: 'contrast(1.6) brightness(1.2)', transform: 'scale(1.05)' }, { filter: 'none', transform: 'none' }] },
  { id: 'jello', name: 'Jello', category: 'emphasis', build: () => [0, -12.5, 6.25, -3.125, 1.56, 0].map((s) => ({ transform: `skew(${s}deg, ${s}deg)` })) },
  { id: 'wave', name: 'Wave', category: 'emphasis', build: () => [0, -20, 0, -10, 0].map((y) => ({ transform: `translateY(${y}px)` })) },
  { id: 'swing', name: 'Swing', category: 'emphasis', build: () => [0, 15, -10, 5, -5, 0].map((r) => ({ transform: `rotate(${r}deg)`, transformOrigin: '50% 0%' })) },
  { id: 'rubber', name: 'Rubber band', category: 'emphasis', build: () => [[1, 1], [1.25, 0.75], [0.75, 1.25], [1.15, 0.85], [0.95, 1.05], [1, 1]].map(([x, y]) => ({ transform: `scale(${x}, ${y})` })) },
  { id: 'glow', name: 'Glow', category: 'emphasis', build: () => [{ filter: 'drop-shadow(0 0 0 rgba(255,215,0,0))' }, { filter: 'drop-shadow(0 0 24px rgba(255,215,0,.95))' }, { filter: 'drop-shadow(0 0 0 rgba(255,215,0,0))' }] },
];

function pathFrames(fn: (p: number) => [number, number], steps = 32): Keyframe[] {
  return Array.from({ length: steps + 1 }, (_, k) => {
    const [x, y] = fn(k / steps);
    return { transform: t(x, y), offset: k / steps };
  });
}

const MOTION: EffectDef[] = [
  {
    id: 'line', name: 'Line', category: 'motion', defaultDuration: 2000,
    directions: [{ value: 'down', label: 'Down' }, { value: 'up', label: 'Up' }, { value: 'left', label: 'Left' }, { value: 'right', label: 'Right' }, { value: 'diagonal', label: 'Diagonal' }],
    build: (i) => {
      const d = Math.max(i.W, i.H) * 0.2;
      const v: Record<string, [number, number]> = { down: [0, d], up: [0, -d], left: [-d, 0], right: [d, 0], diagonal: [d, d] };
      const [x, y] = local(...(v[i.dir] ?? v.down), i.box.rot);
      return [{ transform: 'none' }, { transform: t(x, y) }];
    },
  },
  {
    id: 'arc', name: 'Arc', category: 'motion', defaultDuration: 2000, directions: UPDOWN,
    build: (i) => {
      const d = i.W * 0.2, hgt = i.H * 0.15 * (i.dir === 'down' ? 1 : -1);
      return pathFrames((p) => local(p * d, Math.sin(p * Math.PI) * hgt, i.box.rot));
    },
  },
  {
    id: 'circle', name: 'Circle', category: 'motion', defaultDuration: 2000,
    build: (i) => { const r = Math.min(i.W, i.H) * 0.12; return pathFrames((p) => local(Math.sin(p * 2 * Math.PI) * r, (1 - Math.cos(p * 2 * Math.PI)) * r, i.box.rot)); },
  },
  {
    id: 'loop', name: 'Loop de loop', category: 'motion', defaultDuration: 2500,
    build: (i) => { const r = Math.min(i.W, i.H) * 0.1; return pathFrames((p) => local(p * r * 4 - Math.sin(p * 2 * Math.PI) * r, -(1 - Math.cos(p * 2 * Math.PI)) * r, i.box.rot), 48); },
  },
  {
    id: 'zigzag', name: 'Zigzag', category: 'motion', defaultDuration: 2000,
    build: (i) => { const d = i.W * 0.25, a = i.H * 0.06; return pathFrames((p) => local(p * d, (Math.abs(((p * 4) % 2) - 1) - 0.5) * 2 * a + a, i.box.rot), 40); },
  },
  {
    id: 'sine', name: 'Sine wave', category: 'motion', defaultDuration: 2500,
    build: (i) => { const d = i.W * 0.3, a = i.H * 0.08; return pathFrames((p) => local(p * d, Math.sin(p * Math.PI * 4) * a, i.box.rot), 48); },
  },
  {
    id: 'spiral', name: 'Spiral', category: 'motion', defaultDuration: 2500,
    build: (i) => { const r = Math.min(i.W, i.H) * 0.18; return pathFrames((p) => local(Math.cos(p * 6 * Math.PI) * r * (1 - p) - r, Math.sin(p * 6 * Math.PI) * r * (1 - p), i.box.rot), 60); },
  },
  {
    id: 'bounce-path', name: 'Bounce right', category: 'motion', defaultDuration: 2000,
    build: (i) => { const d = i.W * 0.25, hgt = i.H * 0.2; return pathFrames((p) => local(p * d, -Math.abs(Math.sin(p * Math.PI * 3)) * hgt * (1 - p * 0.6), i.box.rot), 48); },
  },
];

export const EFFECTS: EffectDef[] = [...ENTRANCES, ...EMPHASIS, ...EXITS, ...MOTION];

export function getEffect(category: AnimCategory, id: string): EffectDef | undefined {
  return EFFECTS.find((e) => e.category === category && e.id === id);
}

export function effectsFor(category: AnimCategory): EffectDef[] {
  return EFFECTS.filter((e) => e.category === category);
}

export function defaultDuration(category: AnimCategory, id: string): number {
  const def = getEffect(category, id);
  if (def?.defaultDuration) return def.defaultDuration;
  return category === 'emphasis' ? 1000 : category === 'motion' ? 2000 : 600;
}

/* ------------------------------------------------------------------ */
/* Timeline                                                            */
/* ------------------------------------------------------------------ */

export interface TimedAnim {
  anim: Animation;
  start: number;
}

export interface Step {
  /** True when the step starts by itself (its first item is not on click). */
  auto: boolean;
  items: TimedAnim[];
}

/** Groups a slide's animations into click steps with start times relative to the step. */
export function buildSteps(anims: Animation[]): Step[] {
  const steps: Step[] = [];
  let current: Step | null = null;
  let prevStart = 0;
  let prevEnd = 0;
  for (const anim of anims) {
    const total = anim.delay + anim.duration * Math.max(1, anim.repeat);
    if (anim.trigger === 'click' || !current) {
      current = { auto: anim.trigger !== 'click', items: [] };
      steps.push(current);
      prevStart = 0;
      prevEnd = 0;
    }
    let start = 0;
    if (anim.trigger === 'with') start = prevStart;
    if (anim.trigger === 'after') start = prevEnd;
    current.items.push({ anim, start });
    prevStart = start;
    prevEnd = Math.max(prevEnd, start + total);
  }
  return steps;
}

/** Elements whose first animation is an entrance start hidden. */
export function initiallyHidden(slide: Slide): Set<string> {
  const seen = new Set<string>();
  const hidden = new Set<string>();
  for (const a of slide.animations) {
    if (seen.has(a.elementId)) continue;
    seen.add(a.elementId);
    if (a.category === 'entrance') hidden.add(a.elementId);
  }
  return hidden;
}

export interface StepRunner {
  promise: Promise<void>;
  animations: globalThis.Animation[];
}

/**
 * Plays one step on a rendered slide. `root` is the slide content element
 * whose elements carry data-el-id attributes.
 */
export function runStep(root: HTMLElement, slide: Slide, W: number, H: number, step: Step, instant = false): StepRunner {
  const animations: globalThis.Animation[] = [];
  const jobs: Promise<void>[] = [];
  for (const { anim, start } of step.items) {
    const wrapper = root.querySelector(`[data-el-id="${CSS.escape(anim.elementId)}"]`) as HTMLElement | null;
    const target = wrapper?.querySelector('.el-anim') as HTMLElement | null;
    const el = slide.elements.find((e) => e.id === anim.elementId);
    const def = getEffect(anim.category, anim.effect);
    if (!wrapper || !target || !el || !def) continue;
    const dir = anim.direction ?? def.directions?.[0]?.value ?? '';
    let frames = def.build({ dir, box: { x: el.x, y: el.y, w: el.w, h: el.h, rot: el.rot }, W, H });
    if (anim.category === 'entrance') frames = frames.map((f) => ({ ...f, visibility: 'visible' }));
    const a = target.animate(frames, {
      duration: Math.max(1, anim.duration),
      delay: start + anim.delay,
      iterations: Math.max(1, anim.repeat),
      easing: easingCss(anim.easing ?? def.easing ?? 'ease-in-out'),
      fill: 'both',
      composite: anim.category === 'motion' ? 'add' : 'replace',
    });
    // The wrapper stops hiding the element; the keyframes (which hold during the delay) decide visibility.
    if (anim.category === 'entrance') {
      wrapper.style.visibility = 'visible';
      target.style.visibility = 'hidden';
    }
    animations.push(a);
    jobs.push(a.finished.then(() => {
      if (anim.category === 'exit') wrapper.style.visibility = 'hidden';
    }, () => undefined));
    if (instant) a.finish();
  }
  return { promise: Promise.all(jobs).then(() => undefined), animations };
}

/** Removes every element animation from a rendered slide (used when leaving or re-entering it). */
export function resetSlideAnimations(root: HTMLElement, hidden: Set<string>): void {
  root.querySelectorAll<HTMLElement>('[data-el-id]').forEach((wrapper) => {
    const target = wrapper.querySelector('.el-anim') as HTMLElement | null;
    target?.getAnimations().forEach((a) => a.cancel());
    if (target) target.style.visibility = '';
    wrapper.style.visibility = hidden.has(wrapper.dataset.elId ?? '') ? 'hidden' : '';
  });
}
