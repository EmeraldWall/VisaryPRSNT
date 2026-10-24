import type {
  ChartElement, Presentation, ShapeKind, Slide, SlideElement, TableElement, TextStyle, Transition,
} from '../model/types';
import { downloadBlob, embedUrl, htmlToText, safeFileName } from '../model/util';

/* PowerPoint export. Text, shapes, pictures, tables, charts, notes, backgrounds,
   hidden slides and slide transitions are written; element animations are not. */

type Pptx = InstanceType<typeof import('pptxgenjs').default>;

function hexColor(c: string | undefined, fallback = '000000'): { color: string; transparency?: number } {
  if (!c) return { color: fallback };
  const s = c.trim();
  const rgb = s.match(/^rgba?\((\d+)[,\s]+(\d+)[,\s]+(\d+)(?:[,\s/]+([\d.]+))?\)$/i);
  if (rgb) {
    const hex = [rgb[1], rgb[2], rgb[3]].map((v) => Number(v).toString(16).padStart(2, '0')).join('');
    const a = rgb[4] !== undefined ? Number(rgb[4]) : 1;
    return a < 1 ? { color: hex, transparency: Math.round((1 - a) * 100) } : { color: hex };
  }
  let h = s.replace('#', '');
  if (h.length === 3) h = h.split('').map((x) => x + x).join('');
  if (h.length === 8) {
    const a = parseInt(h.slice(6), 16) / 255;
    return { color: h.slice(0, 6), transparency: Math.round((1 - a) * 100) };
  }
  return /^[0-9a-f]{6}$/i.test(h) ? { color: h } : { color: fallback };
}

const SHAPE_MAP: Record<ShapeKind, string> = {
  rect: 'rect', roundRect: 'roundRect', ellipse: 'ellipse', triangle: 'triangle', rightTriangle: 'rtTriangle',
  diamond: 'diamond', pentagon: 'pentagon', hexagon: 'hexagon', octagon: 'octagon', parallelogram: 'parallelogram',
  trapezoid: 'trapezoid', star4: 'star4', star5: 'star5', star6: 'star6', star8: 'star8', arrowRight: 'rightArrow',
  arrowLeft: 'leftArrow', arrowUp: 'upArrow', arrowDown: 'downArrow', arrowDouble: 'leftRightArrow', chevron: 'chevron',
  heart: 'heart', callout: 'wedgeRoundRectCallout', cloud: 'cloud', plus: 'mathPlus', lightning: 'lightningBolt',
  moon: 'moon', donut: 'donut', frame: 'frame', cylinder: 'can', line: 'line', arrowLine: 'line', blockArc: 'blockArc',
  wave: 'wave',
};

interface Run {
  text: string;
  options: Record<string, unknown>;
}

/** Converts rich text html into pptxgenjs text runs with paragraphs and bullets. */
export function htmlToRuns(html: string, pt: (px: number) => number): Run[] {
  const doc = new DOMParser().parseFromString(`<div>${html}</div>`, 'text/html');
  const paragraphs: { runs: Run[]; bullet?: 'ul' | 'ol' }[] = [{ runs: [] }];
  const cur = () => paragraphs[paragraphs.length - 1];
  const newPara = (bullet?: 'ul' | 'ol') => {
    if (cur().runs.length || cur().bullet) paragraphs.push({ runs: [], bullet });
    else cur().bullet = bullet;
  };
  const walk = (node: Node, fmt: Record<string, unknown>, list?: 'ul' | 'ol') => {
    if (node.nodeType === Node.TEXT_NODE) {
      const text = node.textContent ?? '';
      if (text) cur().runs.push({ text, options: { ...fmt } });
      return;
    }
    if (!(node instanceof HTMLElement)) return;
    const tag = node.tagName;
    if (tag === 'BR') { newPara(list); return; }
    const f = { ...fmt };
    if (['B', 'STRONG', 'H1', 'H2', 'H3'].includes(tag)) f.bold = true;
    if (['I', 'EM'].includes(tag)) f.italic = true;
    if (tag === 'U') f.underline = { style: 'sng' };
    if (['S', 'STRIKE', 'DEL'].includes(tag)) f.strike = 'sngStrike';
    if (tag === 'SUP') f.superscript = true;
    if (tag === 'SUB') f.subscript = true;
    const st = node.style;
    if (st.fontWeight === 'bold' || Number(st.fontWeight) >= 600) f.bold = true;
    if (st.fontStyle === 'italic') f.italic = true;
    if (st.textDecoration.includes('underline')) f.underline = { style: 'sng' };
    if (st.textDecoration.includes('line-through')) f.strike = 'sngStrike';
    const color = st.color || node.getAttribute('color');
    if (color) f.color = hexColor(color).color;
    if (st.fontSize.endsWith('px')) f.fontSize = pt(parseFloat(st.fontSize));
    const face = st.fontFamily || node.getAttribute('face');
    if (face) f.fontFace = face.split(',')[0].replace(/["']/g, '').trim();
    if (tag === 'A' && node.getAttribute('href')) f.hyperlink = { url: node.getAttribute('href') };
    const block = ['DIV', 'P', 'LI', 'H1', 'H2', 'H3', 'BLOCKQUOTE'].includes(tag);
    const childList = tag === 'UL' ? 'ul' : tag === 'OL' ? 'ol' : list;
    if (block) newPara(tag === 'LI' ? list : undefined);
    node.childNodes.forEach((c) => walk(c, f, childList));
    if (block) newPara(undefined);
  };
  walk(doc.body.firstChild as Node, {});
  const nonEmpty = paragraphs.filter((p, i) => p.runs.length || (i > 0 && i < paragraphs.length - 1));
  const out: Run[] = [];
  nonEmpty.forEach((p, i) => {
    const runs = p.runs.length ? p.runs : [{ text: '', options: {} }];
    runs.forEach((r, j) => {
      const options = { ...r.options };
      if (j === 0 && p.bullet) options.bullet = p.bullet === 'ol' ? { type: 'number' } : true;
      if (j === runs.length - 1 && i < nonEmpty.length - 1) options.breakLine = true;
      out.push({ text: r.text, options });
    });
  });
  if (!out.length) out.push({ text: '', options: {} });
  return out;
}

/* ---------------- transitions ---------------- */

const NS = {
  mc: 'http://schemas.openxmlformats.org/markup-compatibility/2006',
  p14: 'http://schemas.microsoft.com/office/powerpoint/2010/main',
  p15: 'http://schemas.microsoft.com/office/powerpoint/2012/main',
  p159: 'http://schemas.microsoft.com/office/powerpoint/2015/09/main',
};

const dirLRUD = (d?: string) => ({ right: 'l', left: 'r', top: 'd', bottom: 'u' }[d ?? 'right'] ?? 'l');
const dirLR = (d?: string) => (d === 'left' ? 'r' : 'l');
const orient = (d?: string) => (d === 'vertical' ? 'vert' : 'horz');

/** Returns [requiredNamespace, innerXml] for a transition, or null for none. */
export function transitionXml(t: Transition): ['p' | 'p14' | 'p15' | 'p159', string] | null {
  const d = t.direction;
  switch (t.type) {
    case 'none': return null;
    case 'fade': return ['p', d === 'black' ? '<p:fade thruBlk="1"/>' : '<p:fade/>'];
    case 'push': case 'pan': return ['p', `<p:push dir="${dirLRUD(d)}"/>`];
    case 'wipe': case 'smooth-wipe': case 'line-wipe': case 'color-wipe': return ['p', `<p:wipe dir="${dirLRUD(d)}"/>`];
    case 'split': {
      const [o, w] = (d ?? 'vertical-out').split('-');
      return ['p', `<p:split orient="${o === 'horizontal' ? 'horz' : 'vert'}" dir="${w === 'in' ? 'in' : 'out'}"/>`];
    }
    case 'reveal': return ['p14', `<p14:reveal dir="${dirLR(d)}"/>`];
    case 'random-bars': return ['p', `<p:randomBar dir="${orient(d)}"/>`];
    case 'shape': {
      const k = (d ?? 'circle').split('-')[0];
      return ['p', k === 'diamond' ? '<p:diamond/>' : k === 'plus' ? '<p:plus/>' : '<p:circle/>'];
    }
    case 'iris': return ['p', '<p:circle/>'];
    case 'uncover': return ['p', `<p:pull dir="${dirLRUD(d)}"/>`];
    case 'cover': case 'stack': return ['p', `<p:cover dir="${dirLRUD(d)}"/>`];
    case 'flash': return ['p14', '<p14:flash/>'];
    case 'dissolve': case 'static': return ['p', '<p:dissolve/>'];
    case 'morph': return ['p159', '<p159:morph option="byObject"/>'];
    case 'fall-over': return ['p15', `<p15:prstTrans prst="fallOver"${d === 'left' ? ' invX="1"' : ''}/>`];
    case 'drape': return ['p15', `<p15:prstTrans prst="drape"${d === 'left' ? ' invX="1"' : ''}/>`];
    case 'curtains': return ['p15', '<p15:prstTrans prst="curtains"/>'];
    case 'wind': return ['p15', `<p15:prstTrans prst="wind"${d === 'left' ? ' invX="1"' : ''}/>`];
    case 'prestige': return ['p15', '<p15:prstTrans prst="prestige"/>'];
    case 'fracture': return ['p15', '<p15:prstTrans prst="fracture"/>'];
    case 'crush': return ['p15', '<p15:prstTrans prst="crush"/>'];
    case 'peel-off': return ['p15', `<p15:prstTrans prst="peelOff"${d === 'left' ? ' invX="1"' : ''}/>`];
    case 'page-curl': return ['p15', `<p15:prstTrans prst="${d === 'double' ? 'pageCurlDouble' : 'pageCurlSingle'}"${d === 'left' ? ' invX="1"' : ''}/>`];
    case 'airplane': return ['p15', `<p15:prstTrans prst="airplane"${d === 'left' ? ' invX="1"' : ''}/>`];
    case 'origami': return ['p15', `<p15:prstTrans prst="origami"${d === 'left' ? ' invX="1"' : ''}/>`];
    case 'checkerboard': return ['p', `<p:checker dir="${d === 'down' ? 'vert' : 'horz'}"/>`];
    case 'blinds': return ['p', `<p:blinds dir="${orient(d)}"/>`];
    case 'clock':
      if (d === 'wedge') return ['p', '<p:wedge/>'];
      if (d === 'counter') return ['p14', '<p14:wheelReverse spokes="1"/>'];
      return ['p', '<p:wheel spokes="1"/>'];
    case 'wheel': return ['p', `<p:wheel spokes="${Number(d) || 4}"/>`];
    case 'ripple': return ['p14', `<p14:ripple dir="${d === 'center' || !d ? 'center' : d === 'tl' ? 'lu' : d === 'tr' ? 'ru' : d === 'bl' ? 'ld' : 'rd'}"/>`];
    case 'honeycomb': return ['p14', '<p14:honeycomb/>'];
    case 'glitter': return ['p14', `<p14:glitter dir="${(d ?? '').startsWith('left') ? 'r' : 'l'}" pattern="${(d ?? '').endsWith('hex') ? 'hexagon' : 'diamond'}"/>`];
    case 'vortex': return ['p14', `<p14:vortex dir="${dirLRUD(d)}"/>`];
    case 'shred': return ['p14', `<p14:shred pattern="${d === 'particles' ? 'rectangle' : 'strip'}" dir="in"/>`];
    case 'switch': case 'rochade': return ['p14', `<p14:switch dir="${dirLR(d)}"/>`];
    case 'flip': case 'tile-flip': return ['p14', `<p14:flip dir="${dirLR(d)}"/>`];
    case 'gallery': return ['p14', `<p14:gallery dir="${dirLR(d)}"/>`];
    case 'cube': return ['p14', `<p14:prism dir="${dirLRUD(d)}" isContent="1"/>`];
    case 'box': return ['p14', `<p14:prism dir="${dirLRUD(d)}"/>`];
    case 'rotate': return ['p14', `<p14:prism dir="${dirLRUD(d)}" isContent="1" isInverted="1"/>`];
    case 'orbit': return ['p14', `<p14:prism dir="${dirLR(d)}" isInverted="1"/>`];
    case 'doors': return ['p14', `<p14:doors dir="${d === 'horizontal' ? 'horz' : 'vert'}"/>`];
    case 'window': return ['p14', `<p14:window dir="${d === 'horizontal' ? 'horz' : 'vert'}"/>`];
    case 'comb': return ['p', `<p:comb dir="${orient(d)}"/>`];
    case 'zoom': return d === 'rotate' ? ['p', '<p:newsflash/>'] : ['p', `<p:zoom dir="${d === 'out' ? 'out' : 'in'}"/>`];
    case 'diagonal': return ['p', `<p:strips dir="${{ tl: 'rd', tr: 'ld', bl: 'ru', br: 'lu' }[d ?? 'tl'] ?? 'rd'}"/>`];
    case 'ferris': return ['p14', `<p14:ferris dir="${dirLR(d)}"/>`];
    case 'conveyor': return ['p14', `<p14:conveyor dir="${dirLR(d)}"/>`];
    case 'fly-through': return ['p14', `<p14:flythrough dir="${(d ?? 'in').startsWith('out') ? 'out' : 'in'}"${(d ?? '').endsWith('bounce') ? ' hasBounce="1"' : ''}/>`];
    case 'chop': return ['p', '<p:split orient="horz" dir="out"/>'];
    case 'blur': return ['p', '<p:fade/>'];
    case 'random': return ['p', '<p:random/>'];
    default: return ['p', '<p:fade/>'];
  }
}

export function slideTransitionXml(t: Transition): string {
  const res = transitionXml(t);
  if (!res && t.advanceAfter === null && t.advanceOnClick) return '';
  const spd = t.duration < 600 ? 'fast' : t.duration < 1100 ? 'med' : 'slow';
  const adv = `${t.advanceOnClick ? '' : ' advClick="0"'}${t.advanceAfter !== null ? ` advTm="${Math.round(t.advanceAfter * 1000)}"` : ''}`;
  if (!res) return `<p:transition${adv}/>`;
  const [ns, inner] = res;
  if (ns === 'p') {
    return `<mc:AlternateContent xmlns:mc="${NS.mc}"><mc:Choice xmlns:p14="${NS.p14}" Requires="p14"><p:transition spd="${spd}" p14:dur="${Math.round(t.duration)}"${adv}>${inner}</p:transition></mc:Choice><mc:Fallback><p:transition spd="${spd}"${adv}>${inner}</p:transition></mc:Fallback></mc:AlternateContent>`;
  }
  const decl = ns === 'p14' ? `xmlns:p14="${NS.p14}"` : `xmlns:p14="${NS.p14}" xmlns:${ns}="${NS[ns]}"`;
  return `<mc:AlternateContent xmlns:mc="${NS.mc}"><mc:Choice ${decl} Requires="${ns}"><p:transition spd="${spd}" p14:dur="${Math.round(t.duration)}"${adv}>${inner}</p:transition></mc:Choice><mc:Fallback><p:transition spd="${spd}"${adv}><p:fade/></p:transition></mc:Fallback></mc:AlternateContent>`;
}

/* ---------------- elements ---------------- */

function addElement(pptx: Pptx, slide: ReturnType<Pptx['addSlide']>, el: SlideElement, toIn: (v: number) => number, pt: (v: number) => number, pres: Presentation) {
  const box = {
    x: toIn(el.x), y: toIn(el.y), w: toIn(el.w), h: toIn(el.h),
    rotate: el.rot || undefined, flipH: el.flipH || undefined, flipV: el.flipV || undefined,
    objectName: `!!${el.id}`,
  } as Record<string, unknown>;
  if (el.link) {
    if (el.link.kind === 'url') box.hyperlink = { url: el.link.url };
    else {
      const linkSlide = el.link.slideId;
      const idx = pres.slides.findIndex((s) => s.id === linkSlide);
      if (idx >= 0) box.hyperlink = { slide: idx + 1 };
    }
  }
  if (el.shadow?.enabled) {
    box.shadow = { type: 'outer', blur: pt(el.shadow.blur), offset: pt(Math.hypot(el.shadow.x, el.shadow.y)), angle: Math.round((Math.atan2(el.shadow.y, el.shadow.x) * 180) / Math.PI + 360) % 360, ...hexColor(el.shadow.color), opacity: 0.5 };
  }
  const textOpts = (style: TextStyle) => ({
    fontFace: style.fontFamily, fontSize: pt(style.fontSize), ...hexColor(style.color), bold: style.bold, italic: style.italic,
    underline: style.underline ? { style: 'sng' } : undefined, strike: style.strike ? 'sngStrike' : undefined,
    align: style.align, valign: style.vAlign, lineSpacingMultiple: style.lineHeight, charSpacing: style.letterSpacing ? pt(style.letterSpacing) : undefined,
  });
  switch (el.type) {
    case 'text': {
      const fill = el.fill.type !== 'none' ? { fill: hexColor(el.fill.color) } : {};
      const line = el.stroke.width > 0 ? { line: { ...hexColor(el.stroke.color), width: pt(el.stroke.width) } } : {};
      slide.addText(htmlToRuns(el.html, pt) as never, {
        ...box, ...textOpts(el.style), ...fill, ...line, margin: pt(el.padding), fit: 'none', transparency: el.opacity < 1 ? Math.round((1 - el.opacity) * 100) : undefined,
      } as never);
      break;
    }
    case 'shape': {
      const shapeName = SHAPE_MAP[el.shape];
      const isLine = el.shape === 'line' || el.shape === 'arrowLine';
      const shapeOpts: Record<string, unknown> = {
        ...box,
        shape: shapeName,
        fill: el.fill.type === 'none' || isLine ? { type: 'none' } : hexColor(el.fill.color),
        line: el.stroke.width > 0 ? { ...hexColor(el.stroke.color), width: pt(el.stroke.width), dashType: el.stroke.dash === 'dashed' ? 'dash' : el.stroke.dash === 'dotted' ? 'sysDot' : 'solid', endArrowType: el.shape === 'arrowLine' ? 'triangle' : undefined } : undefined,
        rectRadius: el.shape === 'roundRect' ? Math.min(0.5, el.radius / Math.min(el.w, el.h)) : undefined,
      };
      if (isLine) {
        shapeOpts.y = toIn(el.y + el.h / 2);
        shapeOpts.h = 0;
      }
      if (el.html && !isLine) slide.addText(htmlToRuns(el.html, pt) as never, { ...shapeOpts, ...textOpts(el.style) } as never);
      else slide.addShape(shapeName as never, shapeOpts as never);
      break;
    }
    case 'image': {
      const src = el.src.startsWith('data:') ? { data: el.src.slice(5) } : { path: el.src };
      slide.addImage({ ...box, ...src, rounding: el.radius >= Math.min(el.w, el.h) / 2, transparency: el.opacity < 1 ? Math.round((1 - el.opacity) * 100) : undefined } as never);
      break;
    }
    case 'table':
      addTable(slide, el, box, pt, toIn);
      break;
    case 'chart':
      addChart(pptx, slide, el, box, pt);
      break;
    case 'video': {
      const embed = embedUrl(el.src);
      if (embed) slide.addMedia({ ...box, type: 'online', link: embed } as never);
      else if (el.src.startsWith('data:video')) slide.addMedia({ ...box, type: 'video', data: el.src.slice(5) } as never);
      else slide.addText(`Video: ${el.src}`, { ...box, fontSize: 14, color: 'FFFFFF', fill: { color: '000000' }, align: 'center' } as never);
      break;
    }
  }
}

function addTable(slide: ReturnType<Pptx['addSlide']>, el: TableElement, box: Record<string, unknown>, pt: (v: number) => number, toIn: (v: number) => number) {
  const rows = el.rows.map((row, r) => row.map((cell) => {
    const header = el.headerRow && r === 0;
    const fill = header ? el.headerFill : el.bandedRows && r % 2 === 0 ? el.bandFill : el.cellFill;
    return {
      text: htmlToText(cell),
      options: {
        fill: hexColor(fill), ...hexColor(header ? el.headerColor : el.style.color), bold: header || el.style.bold,
        fontFace: el.style.fontFamily, fontSize: pt(el.style.fontSize), align: el.style.align, valign: 'middle',
      },
    };
  }));
  const cols = Math.max(1, ...el.rows.map((r) => r.length));
  slide.addTable(rows as never, {
    ...box, colW: Array.from({ length: cols }, () => toIn(el.w) / cols),
    border: { type: 'solid', pt: 1, ...hexColor(el.borderColor) },
  } as never);
}

function addChart(pptx: Pptx, slide: ReturnType<Pptx['addSlide']>, el: ChartElement, box: Record<string, unknown>, pt: (v: number) => number) {
  const typeMap = { column: 'bar', bar: 'bar', line: 'line', area: 'area', pie: 'pie', doughnut: 'doughnut' } as const;
  const type = (pptx.ChartType as Record<string, unknown>)[typeMap[el.chartType]];
  const data = el.series.map((s) => ({ name: s.name, labels: el.labels, values: s.values }));
  const pie = el.chartType === 'pie' || el.chartType === 'doughnut';
  slide.addChart(type as never, (pie ? data.slice(0, 1) : data) as never, {
    ...box,
    barDir: el.chartType === 'bar' ? 'bar' : 'col',
    chartColors: pie ? undefined : el.series.map((s) => hexColor(s.color).color),
    showLegend: el.showLegend, legendPos: 'b', showValue: el.showValues, showPercent: pie && el.showValues,
    showTitle: !!el.title, title: el.title, titleFontSize: pt(Math.max(12, Math.min(el.w, el.h) * 0.058)),
    valGridLine: el.showGrid ? { style: 'solid', color: 'DDDDDD' } : { style: 'none' },
    catAxisLabelColor: hexColor(el.color).color, valAxisLabelColor: hexColor(el.color).color,
    legendColor: hexColor(el.color).color, titleColor: hexColor(el.color).color,
    holeSize: el.chartType === 'doughnut' ? 55 : undefined,
  } as never);
}

function setBackground(slide: ReturnType<Pptx['addSlide']>, s: Slide) {
  const bg = s.background;
  if (bg.image) {
    slide.background = bg.image.startsWith('data:') ? { data: bg.image.slice(5) } : { path: bg.image };
  } else if (bg.fill.type !== 'none') {
    // Gradients are not supported by the writer, so the first gradient colour is used.
    slide.background = hexColor(bg.fill.color);
  }
}

export async function exportPptx(pres: Presentation): Promise<void> {
  const [{ default: PptxGenJS }, { default: JSZip }] = await Promise.all([import('pptxgenjs'), import('jszip')]);
  const pptx = new PptxGenJS();
  const pxPerIn = pres.height / 7.5;
  const toIn = (v: number) => Math.round((v / pxPerIn) * 1000) / 1000;
  const pt = (v: number) => Math.round(((v * 72) / pxPerIn) * 10) / 10;
  pptx.defineLayout({ name: 'VISARY', width: toIn(pres.width), height: 7.5 });
  pptx.layout = 'VISARY';
  pptx.title = pres.title;
  pptx.company = 'Visary PRSNT';

  for (const s of pres.slides) {
    const slide = pptx.addSlide();
    setBackground(slide, s);
    for (const el of s.elements) {
      try {
        addElement(pptx, slide, el, toIn, pt, pres);
      } catch (err) {
        console.warn('Skipped element in PowerPoint export', el.type, err);
      }
    }
    if (s.notes) slide.addNotes(s.notes);
  }

  const buf = (await pptx.write({ outputType: 'arraybuffer' })) as ArrayBuffer;
  const zip = await JSZip.loadAsync(buf);
  for (let i = 0; i < pres.slides.length; i++) {
    const path = `ppt/slides/slide${i + 1}.xml`;
    const file = zip.file(path);
    if (!file) continue;
    let xml = await file.async('string');
    const s = pres.slides[i];
    const tx = slideTransitionXml(s.transition);
    if (tx) xml = xml.includes('<p:timing') ? xml.replace('<p:timing', `${tx}<p:timing`) : xml.replace('</p:sld>', `${tx}</p:sld>`);
    if (s.hidden) xml = xml.replace('<p:sld ', '<p:sld show="0" ');
    zip.file(path, xml);
  }
  const blob = await zip.generateAsync({ type: 'blob', mimeType: 'application/vnd.openxmlformats-officedocument.presentationml.presentation' });
  downloadBlob(blob, `${safeFileName(pres.title)}.pptx`);
}
