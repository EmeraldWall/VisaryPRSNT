import { useSyncExternalStore } from 'react';
import { produce, type Draft } from 'immer';
import type { Id, Presentation, Slide, SlideElement } from '../model/types';

export type RibbonTab = 'home' | 'insert' | 'design' | 'transitions' | 'animations' | 'slideshow' | 'view';
export type SidePanel = 'format' | 'design' | 'transitions' | 'animations';

export interface UIState {
  currentSlideId: Id;
  selection: Id[];
  editingId: Id | null;
  zoom: number | 'fit';
  view: 'normal' | 'sorter';
  tab: RibbonTab;
  panel: SidePanel;
  showGrid: boolean;
  snap: boolean;
  showNotes: boolean;
  appTheme: 'light' | 'dark';
  formatPainter: Partial<SlideElement> | null;
  presenting: { startIndex: number } | null;
  previewTransition: number;
  previewAnimations: number;
  printing: boolean;
  modal: null | 'find' | 'shortcuts' | 'video' | 'image-url';
}

export interface State {
  pres: Presentation;
  ui: UIState;
  dirty: number;
}

type Listener = () => void;

let state: State;
const listeners = new Set<Listener>();
let past: Presentation[] = [];
let future: Presentation[] = [];
let gestureBase: Presentation | null = null;
let lastCoalesce: { key: string; time: number } | null = null;
const HISTORY_LIMIT = 200;

function emit() {
  for (const l of listeners) l();
}

export function initStore(pres: Presentation, ui: Partial<UIState> = {}): void {
  past = [];
  future = [];
  gestureBase = null;
  let appTheme: 'light' | 'dark' = 'light';
  try {
    if (localStorage.getItem('visary-app-theme') === 'dark') appTheme = 'dark';
  } catch {
    /* storage unavailable */
  }
  state = {
    pres,
    dirty: 0,
    ui: {
      currentSlideId: pres.slides[0]?.id ?? '',
      selection: [],
      editingId: null,
      zoom: 'fit',
      view: 'normal',
      tab: 'home',
      panel: 'format',
      showGrid: false,
      snap: true,
      showNotes: true,
      appTheme,
      formatPainter: null,
      presenting: null,
      previewTransition: 0,
      previewAnimations: 0,
      printing: false,
      modal: null,
      ...ui,
    },
  };
  emit();
}

export function getState(): State {
  return state;
}

export function subscribe(l: Listener): () => void {
  listeners.add(l);
  return () => listeners.delete(l);
}

export function useStore<T>(selector: (s: State) => T): T {
  return useSyncExternalStore(subscribe, () => selector(state));
}

export function setUI(patch: Partial<UIState> | ((ui: UIState) => Partial<UIState>)): void {
  const p = typeof patch === 'function' ? patch(state.ui) : patch;
  state = { ...state, ui: { ...state.ui, ...p } };
  emit();
}

export interface UpdateOptions {
  /** false skips history entirely (used during drag gestures). */
  history?: boolean;
  /** Edits with the same key within a short window merge into one undo step. */
  coalesce?: string;
}

export function update(recipe: (draft: Draft<Presentation>) => void, opts: UpdateOptions = {}): void {
  const prev = state.pres;
  const next = produce(prev, recipe);
  if (next === prev) return;
  const now = Date.now();
  const recordHistory = opts.history !== false && !gestureBase;
  if (recordHistory) {
    const merge = opts.coalesce && lastCoalesce && lastCoalesce.key === opts.coalesce && now - lastCoalesce.time < 1200;
    if (!merge) {
      past.push(prev);
      if (past.length > HISTORY_LIMIT) past.shift();
    }
    future = [];
    lastCoalesce = opts.coalesce ? { key: opts.coalesce, time: now } : null;
  }
  state = { ...state, pres: { ...next, updatedAt: now }, dirty: state.dirty + 1 };
  fixUI();
  emit();
}

/** Starts a continuous gesture (drag, resize). Intermediate updates are not recorded. */
export function beginGesture(): void {
  gestureBase = state.pres;
}

export function endGesture(): void {
  if (gestureBase && gestureBase !== state.pres) {
    past.push(gestureBase);
    if (past.length > HISTORY_LIMIT) past.shift();
    future = [];
  }
  gestureBase = null;
  lastCoalesce = null;
}

export function undo(): void {
  const prev = past.pop();
  if (!prev) return;
  future.push(state.pres);
  state = { ...state, pres: prev, dirty: state.dirty + 1 };
  lastCoalesce = null;
  fixUI();
  emit();
}

export function redo(): void {
  const next = future.pop();
  if (!next) return;
  past.push(state.pres);
  state = { ...state, pres: next, dirty: state.dirty + 1 };
  lastCoalesce = null;
  fixUI();
  emit();
}

export const canUndo = () => past.length > 0;
export const canRedo = () => future.length > 0;

/** Keeps UI references valid after the document changes. */
function fixUI() {
  const { pres, ui } = state;
  let patch: Partial<UIState> | null = null;
  const slide = pres.slides.find((s) => s.id === ui.currentSlideId);
  if (!slide) {
    patch = { currentSlideId: pres.slides[0]?.id ?? '', selection: [], editingId: null };
  } else {
    const ids = new Set(slide.elements.map((e) => e.id));
    const sel = ui.selection.filter((id) => ids.has(id));
    if (sel.length !== ui.selection.length) patch = { selection: sel };
    if (ui.editingId && !ids.has(ui.editingId)) patch = { ...patch, editingId: null };
  }
  if (patch) state = { ...state, ui: { ...ui, ...patch } };
}

/* ---------- selectors and helpers ---------- */

export function currentSlide(s: State = state): Slide {
  return s.pres.slides.find((sl) => sl.id === s.ui.currentSlideId) ?? s.pres.slides[0];
}

export function currentIndex(s: State = state): number {
  return Math.max(0, s.pres.slides.findIndex((sl) => sl.id === s.ui.currentSlideId));
}

export function selectedElements(s: State = state): SlideElement[] {
  const slide = currentSlide(s);
  if (!slide) return [];
  const sel = new Set(s.ui.selection);
  return slide.elements.filter((e) => sel.has(e.id));
}

/** Runs a mutation against the current slide draft. */
export function updateSlide(fn: (slide: Draft<Slide>) => void, opts?: UpdateOptions): void {
  const id = state.ui.currentSlideId;
  update((d) => {
    const slide = d.slides.find((s) => s.id === id);
    if (slide) fn(slide);
  }, opts);
}

/** Runs a mutation against each listed element on the current slide. */
export function updateElements(ids: Id[], fn: (el: Draft<SlideElement>) => void, opts?: UpdateOptions): void {
  const set = new Set(ids);
  updateSlide((slide) => {
    for (const el of slide.elements) if (set.has(el.id)) fn(el);
  }, opts);
}

export function updateSelected(fn: (el: Draft<SlideElement>) => void, opts?: UpdateOptions): void {
  updateElements(state.ui.selection, fn, opts);
}
