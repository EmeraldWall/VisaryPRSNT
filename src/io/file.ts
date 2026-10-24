import type { Presentation, Slide, SlideElement } from '../model/types';
import { defaultTransition, textStyle } from '../model/factory';
import { downloadBlob, safeFileName, sanitizeHtml, uid } from '../model/util';

export const FILE_EXT = '.visary.json';
const FORMAT = 'visary-prsnt';
const VERSION = 1;

export function exportJson(pres: Presentation): void {
  const body = JSON.stringify({ format: FORMAT, version: VERSION, presentation: pres });
  downloadBlob(new Blob([body], { type: 'application/json' }), `${safeFileName(pres.title)}${FILE_EXT}`);
}

function num(v: unknown, d: number): number {
  return typeof v === 'number' && Number.isFinite(v) ? v : d;
}

function normalizeElement(raw: SlideElement): SlideElement | null {
  if (!raw || typeof raw !== 'object' || !('type' in raw)) return null;
  const el = { ...raw } as SlideElement;
  el.id = typeof el.id === 'string' ? el.id : uid();
  el.x = num(el.x, 0);
  el.y = num(el.y, 0);
  el.w = Math.max(1, num(el.w, 100));
  el.h = Math.max(1, num(el.h, 100));
  el.rot = num(el.rot, 0);
  el.opacity = num(el.opacity, 1);
  switch (el.type) {
    case 'text':
    case 'shape':
      el.html = sanitizeHtml(String(el.html ?? ''));
      el.style = textStyle(el.style);
      break;
    case 'table':
      el.rows = Array.isArray(el.rows) ? el.rows.map((r) => (Array.isArray(r) ? r.map((c) => sanitizeHtml(String(c ?? ''))) : [])) : [['']];
      el.style = textStyle(el.style);
      break;
    case 'image':
      if (typeof el.src !== 'string' || /^\s*javascript:/i.test(el.src)) return null;
      break;
    case 'video':
      if (typeof el.src !== 'string' || /^\s*javascript:/i.test(el.src)) return null;
      break;
    case 'chart':
      el.labels = Array.isArray(el.labels) ? el.labels.map(String) : [];
      el.series = Array.isArray(el.series) ? el.series : [];
      break;
    default:
      return null;
  }
  if (el.link?.kind === 'url' && /^\s*javascript:/i.test(el.link.url)) delete el.link;
  return el;
}

function normalizeSlide(raw: Slide): Slide {
  const s = { ...raw };
  s.id = typeof s.id === 'string' ? s.id : uid();
  s.elements = (Array.isArray(s.elements) ? s.elements : []).map(normalizeElement).filter(Boolean) as SlideElement[];
  s.background = s.background?.fill ? s.background : { fill: { type: 'solid', color: '#ffffff' } };
  s.notes = typeof s.notes === 'string' ? s.notes : '';
  s.transition = { ...defaultTransition(), ...(s.transition ?? {}) };
  const ids = new Set(s.elements.map((e) => e.id));
  s.animations = (Array.isArray(s.animations) ? s.animations : []).filter((a) => a && ids.has(a.elementId));
  return s;
}

/** Validates and repairs a presentation loaded from an untrusted file. */
export function normalizePresentation(raw: unknown): Presentation {
  const obj = raw as { format?: string; presentation?: Presentation } & Presentation;
  const p = (obj?.format === FORMAT ? obj.presentation : obj) as Presentation | undefined;
  if (!p || !Array.isArray(p.slides)) throw new Error('This file is not a Visary presentation.');
  return {
    id: typeof p.id === 'string' ? p.id : uid(),
    title: typeof p.title === 'string' ? p.title : 'Imported presentation',
    width: num(p.width, 1920),
    height: num(p.height, 1080),
    themeId: typeof p.themeId === 'string' ? p.themeId : 'classic',
    slides: p.slides.map(normalizeSlide),
    settings: { loop: !!p.settings?.loop, showSlideNumbers: !!p.settings?.showSlideNumbers },
    createdAt: num(p.createdAt, Date.now()),
    updatedAt: Date.now(),
  };
}

export function pickFile(accept: string): Promise<File | null> {
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = accept;
    input.onchange = () => resolve(input.files?.[0] ?? null);
    input.click();
  });
}

export async function importJsonFile(file: File): Promise<Presentation> {
  const text = await file.text();
  return normalizePresentation(JSON.parse(text));
}
