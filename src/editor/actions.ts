import type { Draft } from 'immer';
import type {
  Animation, AnimCategory, ChartElement, Id, ShapeKind, Slide, SlideElement, TextElement, Transition,
} from '../model/types';
import {
  createChart, createImage, createShape, createSlide, createTable, createText, createVideo, themeBackground,
} from '../model/factory';
import { getTheme } from '../model/themes';
import { escapeHtml, loadImageSize, uid } from '../model/util';
import { defaultDuration, getEffect } from '../present/animations';
import {
  currentIndex, currentSlide, getState, selectedElements, setUI, update, updateSelected, updateSlide,
} from '../store/store';

const theme = () => getState().pres && getTheme(getState().pres.themeId);
const size = () => ({ W: getState().pres.width, H: getState().pres.height });

/* ---------------- slides ---------------- */

export function addSlide(layoutId = 'title-content', at?: number): void {
  const { W, H } = size();
  const slide = createSlide(theme(), layoutId, W, H);
  const index = at ?? currentIndex() + 1;
  const prev = currentSlide();
  if (prev) slide.transition = { ...prev.transition };
  update((d) => { d.slides.splice(index, 0, slide); });
  setUI({ currentSlideId: slide.id, selection: [], editingId: null });
}

/** Duplicates keep element ids so Morph can pair the elements across both slides. */
export function duplicateSlide(id = getState().ui.currentSlideId): void {
  const pres = getState().pres;
  const index = pres.slides.findIndex((s) => s.id === id);
  if (index < 0) return;
  const copy: Slide = structuredClone(pres.slides[index]);
  copy.id = uid();
  copy.animations = copy.animations.map((a) => ({ ...a, id: uid() }));
  update((d) => { d.slides.splice(index + 1, 0, copy); });
  setUI({ currentSlideId: copy.id, selection: [] });
}

export function deleteSlide(id = getState().ui.currentSlideId): void {
  const pres = getState().pres;
  if (pres.slides.length <= 1) {
    const { W, H } = size();
    const fresh = createSlide(theme(), 'blank', W, H);
    update((d) => { d.slides = [fresh]; });
    setUI({ currentSlideId: fresh.id, selection: [] });
    return;
  }
  const index = pres.slides.findIndex((s) => s.id === id);
  const nextId = pres.slides[index + 1]?.id ?? pres.slides[index - 1]?.id;
  update((d) => { d.slides = d.slides.filter((s) => s.id !== id); });
  setUI({ currentSlideId: nextId, selection: [] });
}

export function moveSlide(id: Id, toIndex: number): void {
  update((d) => {
    const from = d.slides.findIndex((s) => s.id === id);
    if (from < 0) return;
    const [s] = d.slides.splice(from, 1);
    d.slides.splice(Math.max(0, Math.min(d.slides.length, toIndex > from ? toIndex - 1 : toIndex)), 0, s);
  });
}

export function toggleHideSlide(id = getState().ui.currentSlideId): void {
  update((d) => {
    const s = d.slides.find((x) => x.id === id);
    if (s) s.hidden = !s.hidden;
  });
}

export function goToSlide(delta: number): void {
  const { pres } = getState();
  const i = Math.max(0, Math.min(pres.slides.length - 1, currentIndex() + delta));
  setUI({ currentSlideId: pres.slides[i].id, selection: [], editingId: null });
}

/* ---------------- inserting ---------------- */

function insert(el: SlideElement): void {
  updateSlide((s) => { s.elements.push(el as Draft<SlideElement>); });
  setUI({ selection: [el.id], editingId: null });
}

function centered(w: number, h: number) {
  const { W, H } = size();
  const n = currentSlide().elements.length % 6;
  return { x: (W - w) / 2 + n * 20, y: (H - h) / 2 + n * 20 };
}

export function insertText(kind: 'box' | 'title' | 'body' = 'box'): void {
  const t = theme();
  const { W, H } = size();
  const w = W * 0.4, h = H * 0.12;
  const { x, y } = centered(w, h);
  const el = kind === 'title'
    ? createText(x, y, w, h, '<div>Heading</div>', { fontFamily: t.headingFont, fontSize: Math.round(H * 0.06), bold: true, color: t.colors.text }, 'title')
    : createText(x, y, w, h, '<div>Type your text</div>', { fontFamily: t.bodyFont, fontSize: Math.round(H * 0.035), color: t.colors.text }, 'body');
  insert(el);
  setUI({ editingId: el.id });
}

export function insertShape(kind: ShapeKind): void {
  const { H } = size();
  const t = theme();
  const s = H * 0.25;
  const isLine = kind === 'line' || kind === 'arrowLine';
  const w = isLine ? s * 1.6 : kind.startsWith('arrow') || kind === 'chevron' ? s * 1.4 : s;
  const h = isLine ? 24 : s;
  const { x, y } = centered(w, h);
  insert(createShape(kind, x, y, w, h, t.colors.accents[0]));
}

export async function insertImage(src: string, at?: { x: number; y: number }): Promise<void> {
  const { W, H } = size();
  const nat = await loadImageSize(src);
  const k = Math.min((W * 0.6) / nat.w, (H * 0.6) / nat.h, 1);
  const w = Math.round(nat.w * k), h = Math.round(nat.h * k);
  const pos = at ? { x: at.x - w / 2, y: at.y - h / 2 } : centered(w, h);
  insert(createImage(src, pos.x, pos.y, w, h));
}

export function insertTable(rows: number, cols: number): void {
  const { W } = size();
  const w = Math.min(W * 0.8, cols * 260);
  const el = createTable(rows, cols, 0, 0, w, theme());
  const { x, y } = centered(el.w, el.h);
  el.x = x; el.y = y;
  insert(el);
}

export function insertChart(type: ChartElement['chartType']): void {
  const { W, H } = size();
  const w = W * 0.55, h = H * 0.55;
  const { x, y } = centered(w, h);
  insert(createChart(type, x, y, w, h, theme()));
}

export function insertVideo(src: string): void {
  const { W } = size();
  const w = W * 0.5, h = (w * 9) / 16;
  const { x, y } = centered(w, h);
  insert(createVideo(src, x, y, w, h));
}

export function insertEmoji(emoji: string): void {
  const { H } = size();
  const s = H * 0.18;
  const { x, y } = centered(s * 1.3, s * 1.3);
  insert(createText(x, y, s * 1.3, s * 1.3, `<div>${escapeHtml(emoji)}</div>`, { fontSize: Math.round(s), align: 'center', vAlign: 'middle', lineHeight: 1 }));
}

export function insertSlideNumber(): void {
  const { W, H } = size();
  const t = theme();
  insert(createText(W - W * 0.1, H - H * 0.09, W * 0.07, H * 0.06, '<div><span data-field="slide-number">#</span></div>', {
    fontFamily: t.bodyFont, fontSize: Math.round(H * 0.022), color: t.colors.textMuted, align: 'right', vAlign: 'middle',
  }));
}

export function insertPlainText(text: string): void {
  const t = theme();
  const { W, H } = size();
  const w = W * 0.5, h = H * 0.2;
  const { x, y } = centered(w, h);
  const html = text.split(/\r?\n/).map((l) => `<div>${escapeHtml(l) || '<br>'}</div>`).join('');
  insert(createText(x, y, w, h, html, { fontFamily: t.bodyFont, fontSize: Math.round(H * 0.03), color: t.colors.text }));
}

/* ---------------- selection based edits ---------------- */

export function deleteSelected(): void {
  const sel = new Set(getState().ui.selection);
  if (!sel.size) return;
  updateSlide((s) => {
    s.elements = s.elements.filter((e) => !sel.has(e.id));
    s.animations = s.animations.filter((a) => !sel.has(a.elementId));
  });
  setUI({ selection: [], editingId: null });
}

let clipboard: { elements: SlideElement[]; animations: Animation[] } | null = null;

export function copySelected(): void {
  const els = selectedElements();
  if (!els.length) return;
  const ids = new Set(els.map((e) => e.id));
  clipboard = {
    elements: structuredClone(els),
    animations: structuredClone(currentSlide().animations.filter((a) => ids.has(a.elementId))),
  };
  try {
    localStorage.setItem('visary-clipboard', JSON.stringify(clipboard));
  } catch {
    /* too large or unavailable, the in memory copy still works */
  }
}

export function cutSelected(): void {
  copySelected();
  deleteSelected();
}

export function hasClipboard(): boolean {
  if (clipboard) return true;
  try {
    return !!localStorage.getItem('visary-clipboard');
  } catch {
    return false;
  }
}

export function paste(offset = 30): void {
  let data = clipboard;
  if (!data) {
    try {
      const raw = localStorage.getItem('visary-clipboard');
      if (raw) data = JSON.parse(raw);
    } catch {
      data = null;
    }
  }
  if (!data?.elements.length) return;
  const idMap = new Map<string, string>();
  const groupMap = new Map<string, string>();
  const els = data.elements.map((e) => {
    const c = structuredClone(e);
    c.id = uid();
    idMap.set(e.id, c.id);
    if (c.groupId) {
      if (!groupMap.has(c.groupId)) groupMap.set(c.groupId, uid());
      c.groupId = groupMap.get(c.groupId);
    }
    c.x += offset;
    c.y += offset;
    return c;
  });
  const anims = data.animations.map((a) => ({ ...a, id: uid(), elementId: idMap.get(a.elementId) ?? a.elementId }));
  updateSlide((s) => {
    s.elements.push(...(els as Draft<SlideElement>[]));
    s.animations.push(...anims);
  });
  setUI({ selection: els.map((e) => e.id), editingId: null });
  // Next paste lands a step further so copies do not stack exactly.
  if (clipboard) clipboard = { ...clipboard, elements: clipboard.elements.map((e) => ({ ...e, x: e.x + offset, y: e.y + offset })) };
}

export function duplicateSelected(): void {
  copySelected();
  paste();
}

export function selectAll(): void {
  setUI({ selection: currentSlide().elements.map((e) => e.id), editingId: null });
}

export function nudge(dx: number, dy: number): void {
  updateSelected((el) => {
    if (el.locked) return;
    el.x += dx;
    el.y += dy;
  }, { coalesce: 'nudge' });
}

export type ZOrder = 'front' | 'back' | 'forward' | 'backward';

export function arrange(order: ZOrder): void {
  const sel = new Set(getState().ui.selection);
  if (!sel.size) return;
  updateSlide((s) => {
    const picked = s.elements.filter((e) => sel.has(e.id));
    const rest = s.elements.filter((e) => !sel.has(e.id));
    if (order === 'front') s.elements = [...rest, ...picked];
    else if (order === 'back') s.elements = [...picked, ...rest];
    else {
      const arr = [...s.elements];
      const step = order === 'forward' ? 1 : -1;
      const indices = arr.map((e, i) => (sel.has(e.id) ? i : -1)).filter((i) => i >= 0);
      if (step > 0) indices.reverse();
      for (const i of indices) {
        const j = i + step;
        if (j < 0 || j >= arr.length || sel.has(arr[j].id)) continue;
        [arr[i], arr[j]] = [arr[j], arr[i]];
      }
      s.elements = arr;
    }
  });
}

export function groupSelected(): void {
  const sel = getState().ui.selection;
  if (sel.length < 2) return;
  const gid = uid();
  updateSelected((el) => { el.groupId = gid; });
}

export function ungroupSelected(): void {
  updateSelected((el) => { delete el.groupId; });
}

export type AlignKind = 'left' | 'center' | 'right' | 'top' | 'middle' | 'bottom' | 'distribute-h' | 'distribute-v';

export function align(kind: AlignKind): void {
  const els = selectedElements();
  if (!els.length) return;
  const { W, H } = size();
  const toSlide = els.length === 1;
  const minX = toSlide ? 0 : Math.min(...els.map((e) => e.x));
  const minY = toSlide ? 0 : Math.min(...els.map((e) => e.y));
  const maxX = toSlide ? W : Math.max(...els.map((e) => e.x + e.w));
  const maxY = toSlide ? H : Math.max(...els.map((e) => e.y + e.h));
  if (kind === 'distribute-h' || kind === 'distribute-v') {
    if (els.length < 3) return;
    const horiz = kind === 'distribute-h';
    const sorted = [...els].sort((a, b) => (horiz ? a.x - b.x : a.y - b.y));
    const total = sorted.reduce((sum, e) => sum + (horiz ? e.w : e.h), 0);
    const span = horiz ? maxX - minX : maxY - minY;
    const gap = (span - total) / (sorted.length - 1);
    let pos = horiz ? minX : minY;
    const target = new Map<string, number>();
    for (const e of sorted) {
      target.set(e.id, pos);
      pos += (horiz ? e.w : e.h) + gap;
    }
    updateSelected((el) => {
      const v = target.get(el.id);
      if (v === undefined) return;
      if (horiz) el.x = v; else el.y = v;
    });
    return;
  }
  updateSelected((el) => {
    if (kind === 'left') el.x = minX;
    if (kind === 'center') el.x = (minX + maxX) / 2 - el.w / 2;
    if (kind === 'right') el.x = maxX - el.w;
    if (kind === 'top') el.y = minY;
    if (kind === 'middle') el.y = (minY + maxY) / 2 - el.h / 2;
    if (kind === 'bottom') el.y = maxY - el.h;
  });
}

export function toggleLock(): void {
  const els = selectedElements();
  const lock = !els.every((e) => e.locked);
  updateSelected((el) => { el.locked = lock; });
}

export function flip(axis: 'h' | 'v'): void {
  updateSelected((el) => {
    if (axis === 'h') el.flipH = !el.flipH; else el.flipV = !el.flipV;
  });
}

/* ---------------- format painter ---------------- */

export function pickFormat(): void {
  const [el] = selectedElements();
  if (!el) return;
  const style: Partial<SlideElement> = { opacity: el.opacity, shadow: el.shadow };
  const s = style as Record<string, unknown>;
  if ('fill' in el) s.fill = el.fill;
  if ('stroke' in el) s.stroke = el.stroke;
  if ('style' in el) s.style = el.style;
  setUI({ formatPainter: structuredClone(style) });
}

export function applyFormat(id: Id): void {
  const fmt = getState().ui.formatPainter as Record<string, unknown> | null;
  if (!fmt) return;
  updateSlide((s) => {
    const el = s.elements.find((e) => e.id === id) as unknown as Record<string, unknown> | undefined;
    if (!el) return;
    for (const [k, v] of Object.entries(fmt)) if (k in el || k === 'shadow') el[k] = structuredClone(v);
  });
  setUI({ formatPainter: null });
}

/* ---------------- text style for selection ---------------- */

export function setTextStyle(patch: Partial<TextElement['style']>): void {
  updateSelected((el) => {
    if ('style' in el) Object.assign(el.style, patch);
  }, { coalesce: `style-${Object.keys(patch).join()}` });
}

/* ---------------- design ---------------- */

export function applyTheme(themeId: string): void {
  const t = getTheme(themeId);
  update((d) => {
    d.themeId = themeId;
    for (const s of d.slides) {
      const isTitle = s.layout === 'title' || s.layout === 'section';
      s.background = themeBackground(t, isTitle);
      const onDark = isTitle && !!t.titleBackground;
      for (const el of s.elements) {
        if (el.type === 'text' && el.role) {
          el.style.fontFamily = el.role === 'title' ? t.headingFont : t.bodyFont;
          if (el.role === 'caption') el.style.color = t.colors.textMuted;
          else el.style.color = onDark && (el.role === 'title' || el.role === 'subtitle') ? titleColor(t) : t.colors.text;
        }
        if (el.type === 'table') {
          el.headerFill = t.colors.accents[0];
          el.style.fontFamily = t.bodyFont;
          el.style.color = t.colors.text;
        }
        if (el.type === 'chart') {
          el.series.forEach((ser, i) => { ser.color = t.colors.accents[i % t.colors.accents.length]; });
          el.fontFamily = t.bodyFont;
          el.color = t.colors.text;
        }
      }
    }
  });
}

function titleColor(t: ReturnType<typeof getTheme>) {
  const c = t.titleBackground?.fill.color ?? '#ffffff';
  const m = c.replace('#', '');
  const lum = 0.299 * parseInt(m.slice(0, 2), 16) + 0.587 * parseInt(m.slice(2, 4), 16) + 0.114 * parseInt(m.slice(4, 6), 16);
  return lum < 140 ? '#ffffff' : t.colors.text;
}

export function resizeSlides(width: number, height: number): void {
  const { W, H } = size();
  const kx = width / W, ky = height / H;
  const k = Math.min(kx, ky);
  update((d) => {
    d.width = width;
    d.height = height;
    for (const s of d.slides) {
      for (const el of s.elements) {
        // Keep proportions: scale by the smaller factor and re-center along the other axis.
        const cx = (el.x + el.w / 2) * kx;
        const cy = (el.y + el.h / 2) * ky;
        el.w *= k;
        el.h *= k;
        el.x = cx - el.w / 2;
        el.y = cy - el.h / 2;
        if ('style' in el) el.style.fontSize = Math.round(el.style.fontSize * k);
      }
    }
  });
}

/* ---------------- transitions and animations ---------------- */

export function setTransition(patch: Partial<Transition>, all = false): void {
  if (all) {
    update((d) => { for (const s of d.slides) Object.assign(s.transition, patch); });
  } else {
    updateSlide((s) => { Object.assign(s.transition, patch); }, { coalesce: `tr-${Object.keys(patch).join()}` });
  }
}

export function applyTransitionToAll(): void {
  const t = currentSlide().transition;
  update((d) => { for (const s of d.slides) s.transition = { ...t }; });
}

export function addAnimation(category: AnimCategory, effect: string): void {
  const sel = getState().ui.selection;
  if (!sel.length) return;
  const def = getEffect(category, effect);
  const anims: Animation[] = sel.map((elementId, i) => ({
    id: uid(),
    elementId,
    category,
    effect,
    trigger: i === 0 ? 'click' : 'with',
    delay: 0,
    duration: defaultDuration(category, effect),
    direction: def?.directions?.[0]?.value,
    repeat: 1,
  }));
  updateSlide((s) => { s.animations.push(...anims); });
}

export function updateAnimation(id: Id, patch: Partial<Animation>): void {
  updateSlide((s) => {
    const a = s.animations.find((x) => x.id === id);
    if (a) Object.assign(a, patch);
  }, { coalesce: `anim-${id}-${Object.keys(patch).join()}` });
}

export function removeAnimation(id: Id): void {
  updateSlide((s) => { s.animations = s.animations.filter((a) => a.id !== id); });
}

export function moveAnimation(id: Id, delta: number): void {
  updateSlide((s) => {
    const i = s.animations.findIndex((a) => a.id === id);
    const j = i + delta;
    if (i < 0 || j < 0 || j >= s.animations.length) return;
    [s.animations[i], s.animations[j]] = [s.animations[j], s.animations[i]];
  });
}

/* ---------------- find and replace ---------------- */

function replaceInHtml(html: string, find: RegExp, repl: string): string {
  const doc = new DOMParser().parseFromString(`<div>${html}</div>`, 'text/html');
  const walker = doc.createTreeWalker(doc.body, NodeFilter.SHOW_TEXT);
  let n: Node | null;
  while ((n = walker.nextNode())) n.textContent = (n.textContent ?? '').replace(find, repl);
  return (doc.body.firstChild as HTMLElement).innerHTML;
}

export function findMatches(query: string, matchCase: boolean): { slideId: Id; elementId: Id }[] {
  if (!query) return [];
  const q = matchCase ? query : query.toLowerCase();
  const out: { slideId: Id; elementId: Id }[] = [];
  for (const s of getState().pres.slides) {
    for (const el of s.elements) {
      const texts = el.type === 'text' || el.type === 'shape' ? [el.html] : el.type === 'table' ? el.rows.flat() : [];
      const hit = texts.some((h) => {
        const t = new DOMParser().parseFromString(h, 'text/html').body.textContent ?? '';
        return (matchCase ? t : t.toLowerCase()).includes(q);
      });
      if (hit) out.push({ slideId: s.id, elementId: el.id });
    }
  }
  return out;
}

export function replaceAll(query: string, replacement: string, matchCase: boolean): number {
  if (!query) return 0;
  const count = findMatches(query, matchCase).length;
  const re = new RegExp(query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), matchCase ? 'g' : 'gi');
  update((d) => {
    for (const s of d.slides) {
      for (const el of s.elements) {
        if (el.type === 'text' || el.type === 'shape') el.html = replaceInHtml(el.html, re, replacement);
        if (el.type === 'table') el.rows = el.rows.map((r) => r.map((c) => replaceInHtml(c, re, replacement)));
      }
    }
  });
  return count;
}
