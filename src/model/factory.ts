import type {
  Background, ChartElement, ImageElement, Presentation, ShapeElement, ShapeKind, Slide,
  SlideElement, TableElement, TextElement, TextRole, TextStyle, Theme, Transition, VideoElement,
} from './types';
import { getTheme, titleTextColor } from './themes';
import { escapeHtml, uid } from './util';

export const SLIDE_SIZES = [
  { id: '16:9', label: 'Widescreen 16:9', width: 1920, height: 1080 },
  { id: '4:3', label: 'Standard 4:3', width: 1440, height: 1080 },
  { id: '16:10', label: 'Widescreen 16:10', width: 1920, height: 1200 },
  { id: '1:1', label: 'Square post 1:1', width: 1080, height: 1080 },
  { id: '9:16', label: 'Story 9:16', width: 1080, height: 1920 },
  { id: 'A4', label: 'A4 landscape', width: 1754, height: 1240 },
];

export const defaultTransition = (): Transition => ({
  type: 'none',
  duration: 800,
  easing: 'ease-in-out',
  advanceOnClick: true,
  advanceAfter: null,
});

export function textStyle(patch: Partial<TextStyle> = {}): TextStyle {
  return {
    fontFamily: 'Inter',
    fontSize: 36,
    color: '#1f2937',
    bold: false,
    italic: false,
    underline: false,
    strike: false,
    align: 'left',
    vAlign: 'top',
    lineHeight: 1.25,
    letterSpacing: 0,
    ...patch,
  };
}

const baseEl = (x: number, y: number, w: number, h: number) => ({
  id: uid(), x, y, w, h, rot: 0, opacity: 1,
});

export function createText(
  x: number, y: number, w: number, h: number, html: string,
  style: Partial<TextStyle> = {}, role?: TextRole,
): TextElement {
  return {
    ...baseEl(x, y, w, h),
    type: 'text',
    html,
    style: textStyle(style),
    fill: { type: 'none', color: '#ffffff' },
    stroke: { color: '#000000', width: 0, dash: 'solid' },
    padding: 12,
    role,
  };
}

export function createShape(shape: ShapeKind, x: number, y: number, w: number, h: number, color: string): ShapeElement {
  const isLine = shape === 'line' || shape === 'arrowLine';
  return {
    ...baseEl(x, y, w, isLine ? Math.max(h, 20) : h),
    type: 'shape',
    shape,
    fill: { type: isLine ? 'none' : 'solid', color },
    stroke: { color: isLine ? color : '#00000000', width: isLine ? 6 : 0, dash: 'solid' },
    radius: 24,
    html: '',
    style: textStyle({ color: '#ffffff', align: 'center', vAlign: 'middle', fontSize: 32 }),
  };
}

export function createImage(src: string, x: number, y: number, w: number, h: number): ImageElement {
  return {
    ...baseEl(x, y, w, h),
    type: 'image',
    src,
    fit: 'cover',
    radius: 0,
    stroke: { color: '#000000', width: 0, dash: 'solid' },
    filters: { brightness: 100, contrast: 100, saturate: 100, grayscale: 0, sepia: 0, blur: 0, hue: 0 },
  };
}

export function createTable(rows: number, cols: number, x: number, y: number, w: number, theme: Theme): TableElement {
  const data = Array.from({ length: rows }, (_, r) =>
    Array.from({ length: cols }, (_, c) => (r === 0 ? `Header ${c + 1}` : '')));
  return {
    ...baseEl(x, y, w, rows * 70),
    type: 'table',
    rows: data,
    style: textStyle({ fontSize: 26, fontFamily: theme.bodyFont, color: theme.colors.text }),
    headerRow: true,
    bandedRows: true,
    borderColor: '#cbd5e1',
    headerFill: theme.colors.accents[0],
    headerColor: '#ffffff',
    cellFill: '#ffffff',
    bandFill: '#f1f5f9',
  };
}

export function createChart(chartType: ChartElement['chartType'], x: number, y: number, w: number, h: number, theme: Theme): ChartElement {
  const a = theme.colors.accents;
  return {
    ...baseEl(x, y, w, h),
    type: 'chart',
    chartType,
    title: 'Quarterly results',
    labels: ['Q1', 'Q2', 'Q3', 'Q4'],
    series: [
      { name: 'Revenue', values: [42, 55, 61, 78], color: a[0] },
      { name: 'Costs', values: [30, 34, 40, 44], color: a[1] },
    ],
    showLegend: true,
    showValues: false,
    showGrid: true,
    fontFamily: theme.bodyFont,
    color: theme.colors.text,
  };
}

export function createVideo(src: string, x: number, y: number, w: number, h: number): VideoElement {
  return { ...baseEl(x, y, w, h), type: 'video', src, autoplay: false, loop: false, muted: false };
}

export interface LayoutDef {
  id: string;
  name: string;
  build: (theme: Theme, W: number, H: number) => { elements: SlideElement[]; title?: boolean };
}

const P = (s: string) => `<div>${escapeHtml(s)}</div>`;
const bullets = (items: string[]) => `<ul>${items.map((i) => `<li>${escapeHtml(i)}</li>`).join('')}</ul>`;

function titleEl(theme: Theme, x: number, y: number, w: number, h: number, text: string, size: number, align: TextStyle['align'] = 'left', color?: string) {
  return createText(x, y, w, h, P(text), {
    fontFamily: theme.headingFont, fontSize: size, bold: true, align, vAlign: 'bottom',
    color: color ?? theme.colors.text, lineHeight: 1.1,
  }, 'title');
}

function bodyEl(theme: Theme, x: number, y: number, w: number, h: number, html: string, size = 36, align: TextStyle['align'] = 'left', color?: string) {
  return createText(x, y, w, h, html, {
    fontFamily: theme.bodyFont, fontSize: size, align, color: color ?? theme.colors.text, lineHeight: 1.4,
  }, 'body');
}

export const LAYOUTS: LayoutDef[] = [
  {
    id: 'title', name: 'Title slide',
    build: (t, W, H) => ({
      title: true,
      elements: [
        titleEl(t, W * 0.1, H * 0.28, W * 0.8, H * 0.26, 'Presentation title', Math.round(H * 0.1), 'center', titleTextColor(t)),
        createText(W * 0.15, H * 0.56, W * 0.7, H * 0.12, P('Subtitle or presenter name'), {
          fontFamily: t.bodyFont, fontSize: Math.round(H * 0.04), align: 'center', color: titleTextColor(t),
        }, 'subtitle'),
      ],
    }),
  },
  {
    id: 'title-content', name: 'Title and content',
    build: (t, W, H) => ({
      elements: [
        titleEl(t, W * 0.06, H * 0.05, W * 0.88, H * 0.16, 'Slide title', Math.round(H * 0.065)),
        bodyEl(t, W * 0.06, H * 0.25, W * 0.88, H * 0.66, bullets(['First key point', 'Second key point', 'Third key point']), Math.round(H * 0.036)),
      ],
    }),
  },
  {
    id: 'section', name: 'Section header',
    build: (t, W, H) => ({
      title: true,
      elements: [
        createShape('rect', W * 0.08, H * 0.62, W * 0.12, 10, t.colors.accents[0]),
        titleEl(t, W * 0.08, H * 0.3, W * 0.84, H * 0.3, 'Section title', Math.round(H * 0.09), 'left', titleTextColor(t)),
        createText(W * 0.08, H * 0.66, W * 0.84, H * 0.1, P('Short description of this section'), {
          fontFamily: t.bodyFont, fontSize: Math.round(H * 0.035), color: titleTextColor(t),
        }, 'subtitle'),
      ],
    }),
  },
  {
    id: 'two-content', name: 'Two content',
    build: (t, W, H) => ({
      elements: [
        titleEl(t, W * 0.06, H * 0.05, W * 0.88, H * 0.16, 'Two column title', Math.round(H * 0.065)),
        bodyEl(t, W * 0.06, H * 0.26, W * 0.42, H * 0.64, bullets(['Left point one', 'Left point two']), Math.round(H * 0.034)),
        bodyEl(t, W * 0.52, H * 0.26, W * 0.42, H * 0.64, bullets(['Right point one', 'Right point two']), Math.round(H * 0.034)),
      ],
    }),
  },
  {
    id: 'comparison', name: 'Comparison',
    build: (t, W, H) => ({
      elements: [
        titleEl(t, W * 0.06, H * 0.05, W * 0.88, H * 0.14, 'Comparison', Math.round(H * 0.065)),
        createShape('roundRect', W * 0.06, H * 0.24, W * 0.42, H * 0.1, t.colors.accents[0]),
        createShape('roundRect', W * 0.52, H * 0.24, W * 0.42, H * 0.1, t.colors.accents[1]),
        bodyEl(t, W * 0.06, H * 0.37, W * 0.42, H * 0.55, bullets(['Benefit', 'Benefit', 'Benefit']), Math.round(H * 0.032)),
        bodyEl(t, W * 0.52, H * 0.37, W * 0.42, H * 0.55, bullets(['Benefit', 'Benefit', 'Benefit']), Math.round(H * 0.032)),
      ].map((el, i) => {
        if (el.type === 'shape') el.html = P(i === 1 ? 'Option A' : 'Option B');
        return el;
      }),
    }),
  },
  {
    id: 'title-only', name: 'Title only',
    build: (t, W, H) => ({
      elements: [titleEl(t, W * 0.06, H * 0.05, W * 0.88, H * 0.16, 'Slide title', Math.round(H * 0.065))],
    }),
  },
  {
    id: 'image-caption', name: 'Picture with caption',
    build: (t, W, H) => {
      const ph = createShape('roundRect', W * 0.06, H * 0.08, W * 0.5, H * 0.84, t.colors.background2);
      ph.html = P('Insert a picture');
      ph.style.color = t.colors.textMuted;
      ph.radius = 16;
      return {
        elements: [
          ph,
          titleEl(t, W * 0.6, H * 0.12, W * 0.34, H * 0.3, 'Picture title', Math.round(H * 0.055)),
          bodyEl(t, W * 0.6, H * 0.46, W * 0.34, H * 0.42, P('Describe the picture and why it matters.'), Math.round(H * 0.03)),
        ],
      };
    },
  },
  {
    id: 'quote', name: 'Quote',
    build: (t, W, H) => ({
      elements: [
        createText(W * 0.08, H * 0.12, W * 0.2, H * 0.3, P('“'), {
          fontFamily: t.headingFont, fontSize: Math.round(H * 0.3), color: t.colors.accents[0], lineHeight: 1,
        }),
        createText(W * 0.14, H * 0.3, W * 0.72, H * 0.34, P('A memorable quote that captures the idea.'), {
          fontFamily: t.headingFont, fontSize: Math.round(H * 0.055), italic: true, color: t.colors.text, align: 'center', vAlign: 'middle',
        }, 'title'),
        createText(W * 0.14, H * 0.68, W * 0.72, H * 0.08, P('Author name'), {
          fontFamily: t.bodyFont, fontSize: Math.round(H * 0.03), color: t.colors.textMuted, align: 'center',
        }, 'caption'),
      ],
    }),
  },
  {
    id: 'big-number', name: 'Big number',
    build: (t, W, H) => ({
      elements: [
        createText(W * 0.1, H * 0.18, W * 0.8, H * 0.4, P('87%'), {
          fontFamily: t.headingFont, fontSize: Math.round(H * 0.28), bold: true, color: t.colors.accents[0], align: 'center', vAlign: 'middle', lineHeight: 1,
        }, 'title'),
        createText(W * 0.15, H * 0.62, W * 0.7, H * 0.14, P('of people remember a statistic shown this way'), {
          fontFamily: t.bodyFont, fontSize: Math.round(H * 0.04), color: t.colors.text, align: 'center',
        }, 'body'),
      ],
    }),
  },
  { id: 'blank', name: 'Blank', build: () => ({ elements: [] }) },
];

export function themeBackground(theme: Theme, title: boolean): Background {
  const bg = title && theme.titleBackground ? theme.titleBackground : theme.background;
  return structuredClone(bg);
}

export function createSlide(theme: Theme, layoutId: string, W: number, H: number): Slide {
  const layout = LAYOUTS.find((l) => l.id === layoutId) ?? LAYOUTS[1];
  const built = layout.build(theme, W, H);
  return {
    id: uid(),
    elements: built.elements,
    background: themeBackground(theme, !!built.title),
    notes: '',
    transition: defaultTransition(),
    animations: [],
    layout: layout.id,
  };
}

export function createPresentation(opts: { title?: string; themeId?: string; width?: number; height?: number; sample?: boolean } = {}): Presentation {
  const theme = getTheme(opts.themeId ?? 'classic');
  const W = opts.width ?? 1920;
  const H = opts.height ?? 1080;
  const slides = opts.sample ? sampleSlides(theme, W, H) : [createSlide(theme, 'title', W, H)];
  return {
    id: uid(),
    title: opts.title ?? 'Untitled presentation',
    width: W,
    height: H,
    themeId: theme.id,
    slides,
    settings: { loop: false, showSlideNumbers: false },
    createdAt: Date.now(),
    updatedAt: Date.now(),
  };
}

/** A short starter deck that shows off layouts, transitions and animations. */
function sampleSlides(theme: Theme, W: number, H: number): Slide[] {
  const s1 = createSlide(theme, 'title', W, H);
  (s1.elements[0] as TextElement).html = P(`${theme.name} template`);
  (s1.elements[1] as TextElement).html = P('Made with Visary PRSNT');
  s1.transition = { ...defaultTransition(), type: 'fade' };

  const s2 = createSlide(theme, 'title-content', W, H);
  (s2.elements[0] as TextElement).html = P('Agenda');
  (s2.elements[1] as TextElement).html = bullets(['Where we are', 'What we learned', 'Where we go next']);
  s2.transition = { ...defaultTransition(), type: 'push', direction: 'right' };
  s2.animations = [{
    id: uid(), elementId: s2.elements[1].id, category: 'entrance', effect: 'float', trigger: 'after',
    delay: 0, duration: 600, direction: 'up', repeat: 1,
  }];

  const s3 = createSlide(theme, 'big-number', W, H);
  s3.transition = { ...defaultTransition(), type: 'cube', direction: 'right', duration: 1200 };
  s3.animations = [{
    id: uid(), elementId: s3.elements[0].id, category: 'entrance', effect: 'zoom', trigger: 'after',
    delay: 0, duration: 700, repeat: 1,
  }];

  const s4 = createSlide(theme, 'title-only', W, H);
  (s4.elements[0] as TextElement).html = P('Results');
  s4.elements.push(createChart('column', W * 0.08, H * 0.26, W * 0.84, H * 0.66, theme));
  s4.transition = { ...defaultTransition(), type: 'gallery', direction: 'right', duration: 1200 };

  const s5 = createSlide(theme, 'section', W, H);
  (s5.elements[1] as TextElement).html = P('Thank you');
  (s5.elements[2] as TextElement).html = P('Questions?');
  s5.transition = { ...defaultTransition(), type: 'vortex', duration: 1600 };
  return [s1, s2, s3, s4, s5];
}
