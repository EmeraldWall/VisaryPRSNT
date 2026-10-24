import type { Draft } from 'immer';
import type {
  Background, ChartElement, Fill, ImageElement, ShapeElement, ShapeKind, SlideElement, Stroke, TableElement,
  TextElement, VideoElement,
} from '../model/types';
import { FONTS, getTheme } from '../model/themes';
import { SHAPES } from '../model/shapes';
import { fileToDataUrl } from '../model/util';
import { pickFile } from '../io/file';
import { currentSlide, getState, update, updateSelected, updateSlide, useStore } from '../store/store';
import { ColorField, NumberField, RangeField, Section, SelectField, Toggle } from '../ui/controls';
import { align, arrange, flip, toggleLock } from './actions';
import { IconButton } from '../ui/controls';

type Upd = (fn: (el: Draft<SlideElement>) => void, key?: string) => void;

const upd: Upd = (fn, key) => updateSelected(fn, key ? { coalesce: key } : undefined);

function themeColors(): string[] {
  const t = getTheme(getState().pres.themeId);
  return [t.colors.background, t.colors.background2, t.colors.text, t.colors.textMuted, ...t.colors.accents];
}

export function FillEditor({ fill, onChange, allowNone = true }: { fill: Fill; onChange: (f: Fill) => void; allowNone?: boolean }) {
  const types: { value: Fill['type']; label: string }[] = [
    ...(allowNone ? [{ value: 'none' as const, label: 'No fill' }] : []),
    { value: 'solid', label: 'Solid' },
    { value: 'linear', label: 'Linear gradient' },
    { value: 'radial', label: 'Radial gradient' },
  ];
  return (
    <div className="stack">
      <SelectField label="Fill" value={fill.type} options={types} onChange={(type) => onChange({ ...fill, type, color2: fill.color2 ?? '#ffffff' })} />
      {fill.type !== 'none' && <ColorField label={fill.type === 'solid' ? 'Colour' : 'Start colour'} value={fill.color} onChange={(color) => onChange({ ...fill, color })} extra={themeColors()} />}
      {(fill.type === 'linear' || fill.type === 'radial') && (
        <ColorField label="End colour" value={fill.color2 ?? '#ffffff'} onChange={(color2) => onChange({ ...fill, color2 })} extra={themeColors()} />
      )}
      {fill.type === 'linear' && <RangeField label="Angle" value={fill.angle ?? 135} min={0} max={360} suffix="deg" onChange={(angle) => onChange({ ...fill, angle })} />}
    </div>
  );
}

function StrokeEditor({ stroke, onChange }: { stroke: Stroke; onChange: (s: Stroke) => void }) {
  return (
    <div className="stack">
      <div className="row2">
        <NumberField label="Width" value={stroke.width} min={0} max={100} onChange={(width) => onChange({ ...stroke, width })} suffix="px" />
        <SelectField label="Style" value={stroke.dash} options={[{ value: 'solid', label: 'Solid' }, { value: 'dashed', label: 'Dashed' }, { value: 'dotted', label: 'Dotted' }]} onChange={(dash) => onChange({ ...stroke, dash })} />
      </div>
      {stroke.width > 0 && <ColorField label="Colour" value={stroke.color} onChange={(color) => onChange({ ...stroke, color })} extra={themeColors()} />}
    </div>
  );
}

function Geometry({ el }: { el: SlideElement }) {
  return (
    <Section title="Size and position">
      <div className="row2">
        <NumberField label="X" value={el.x} onChange={(x) => upd((e) => { e.x = x; }, 'x')} />
        <NumberField label="Y" value={el.y} onChange={(y) => upd((e) => { e.y = y; }, 'y')} />
        <NumberField label="Width" value={el.w} min={1} onChange={(w) => upd((e) => { e.w = w; }, 'w')} />
        <NumberField label="Height" value={el.h} min={1} onChange={(h) => upd((e) => { e.h = h; }, 'h')} />
      </div>
      <RangeField label="Rotation" value={el.rot} min={0} max={360} suffix="deg" onChange={(rot) => upd((e) => { e.rot = rot; }, 'rot')} />
      <RangeField label="Opacity" value={Math.round(el.opacity * 100)} min={0} max={100} suffix="%" onChange={(v) => upd((e) => { e.opacity = v / 100; }, 'op')} />
      <div className="btn-row">
        <IconButton icon="alignHLeft" label="Align left" onClick={() => align('left')} />
        <IconButton icon="alignHCenter" label="Align center" onClick={() => align('center')} />
        <IconButton icon="alignHRight" label="Align right" onClick={() => align('right')} />
        <IconButton icon="alignTop" label="Align top" onClick={() => align('top')} />
        <IconButton icon="alignMiddle" label="Align middle" onClick={() => align('middle')} />
        <IconButton icon="alignBottom" label="Align bottom" onClick={() => align('bottom')} />
      </div>
      <div className="btn-row">
        <IconButton icon="front" label="Bring to front" onClick={() => arrange('front')} />
        <IconButton icon="back" label="Send to back" onClick={() => arrange('back')} />
        <IconButton icon="flipH" label="Flip horizontal" onClick={() => flip('h')} />
        <IconButton icon="flipV" label="Flip vertical" onClick={() => flip('v')} />
        <IconButton icon={el.locked ? 'lock' : 'unlock'} label={el.locked ? 'Unlock' : 'Lock'} onClick={toggleLock} active={el.locked} />
      </div>
    </Section>
  );
}

function Effects({ el }: { el: SlideElement }) {
  const sh = el.shadow ?? { enabled: false, x: 8, y: 12, blur: 24, color: 'rgba(0,0,0,0.35)' };
  const set = (patch: Partial<typeof sh>) => upd((e) => { e.shadow = { ...sh, ...patch }; }, 'shadow');
  return (
    <Section title="Effects" defaultOpen={false}>
      <Toggle label="Drop shadow" checked={sh.enabled} onChange={(enabled) => set({ enabled })} />
      {sh.enabled && (
        <>
          <div className="row2">
            <NumberField label="Offset X" value={sh.x} onChange={(x) => set({ x })} />
            <NumberField label="Offset Y" value={sh.y} onChange={(y) => set({ y })} />
          </div>
          <RangeField label="Blur" value={sh.blur} min={0} max={120} onChange={(blur) => set({ blur })} />
          <ColorField label="Colour" value={sh.color} onChange={(color) => set({ color })} />
        </>
      )}
    </Section>
  );
}

function LinkEditor({ el }: { el: SlideElement }) {
  const slides = useStore((s) => s.pres.slides);
  const kind = el.link?.kind ?? 'none';
  return (
    <Section title="Link" defaultOpen={false}>
      <SelectField
        label="Link to"
        value={kind}
        options={[{ value: 'none', label: 'Nothing' }, { value: 'url', label: 'Web address' }, { value: 'slide', label: 'Slide in this deck' }]}
        onChange={(k) => upd((e) => {
          if (k === 'none') delete e.link;
          else if (k === 'url') e.link = { kind: 'url', url: 'https://' };
          else e.link = { kind: 'slide', slideId: slides[0].id };
        })}
      />
      {el.link?.kind === 'url' && (
        <label className="field">
          <span className="field-label">Address</span>
          <input type="url" value={el.link.url} onChange={(e) => { const url = e.target.value; upd((x) => { if (!/^\s*javascript:/i.test(url)) x.link = { kind: 'url', url }; }, 'link'); }} />
        </label>
      )}
      {el.link?.kind === 'slide' && (
        <SelectField label="Slide" value={el.link.slideId} options={slides.map((s, i) => ({ value: s.id, label: `Slide ${i + 1}` }))} onChange={(slideId) => upd((x) => { x.link = { kind: 'slide', slideId }; })} />
      )}
      <p className="hint">Links work during the slide show and in PowerPoint exports.</p>
    </Section>
  );
}

function TextOptions({ el }: { el: TextElement | ShapeElement }) {
  const s = el.style;
  const setStyle = (patch: Partial<typeof s>) => upd((e) => { if ('style' in e) Object.assign(e.style, patch); }, `st-${Object.keys(patch)}`);
  return (
    <Section title="Text">
      <SelectField label="Font" value={s.fontFamily} options={FONTS.map((f) => ({ value: f, label: f }))} onChange={(fontFamily) => setStyle({ fontFamily })} />
      <div className="row2">
        <NumberField label="Size" value={s.fontSize} min={6} max={600} onChange={(fontSize) => setStyle({ fontSize })} suffix="px" />
        <NumberField label="Letter spacing" value={s.letterSpacing} min={-20} max={100} onChange={(letterSpacing) => setStyle({ letterSpacing })} />
      </div>
      <ColorField label="Colour" value={s.color} onChange={(color) => setStyle({ color })} extra={themeColors()} />
      <RangeField label="Line spacing" value={s.lineHeight} min={0.8} max={3} step={0.05} onChange={(lineHeight) => setStyle({ lineHeight })} />
      {el.type === 'text' && (
        <>
          <NumberField label="Inner padding" value={el.padding} min={0} max={200} onChange={(padding) => upd((e) => { if (e.type === 'text') e.padding = padding; }, 'pad')} />
          <SelectField
            label="Role (follows theme fonts)"
            value={el.role ?? 'none'}
            options={[{ value: 'none', label: 'None' }, { value: 'title', label: 'Title' }, { value: 'subtitle', label: 'Subtitle' }, { value: 'body', label: 'Body' }, { value: 'caption', label: 'Caption' }]}
            onChange={(r) => upd((e) => { if (e.type === 'text') { if (r === 'none') delete e.role; else e.role = r; } })}
          />
        </>
      )}
    </Section>
  );
}

function ShapeOptions({ el }: { el: ShapeElement }) {
  const isLine = el.shape === 'line' || el.shape === 'arrowLine';
  return (
    <>
      <Section title="Shape">
        <SelectField label="Shape type" value={el.shape} options={SHAPES.map((s) => ({ value: s.kind, label: s.name }))} onChange={(shape: ShapeKind) => upd((e) => { if (e.type === 'shape') e.shape = shape; })} />
        {!isLine && <FillEditor fill={el.fill} onChange={(fill) => upd((e) => { if (e.type === 'shape') e.fill = fill; }, 'fill')} />}
        {el.shape === 'roundRect' && <RangeField label="Corner radius" value={el.radius} min={0} max={Math.min(el.w, el.h) / 2} onChange={(radius) => upd((e) => { if (e.type === 'shape') e.radius = radius; }, 'rad')} />}
      </Section>
      <Section title={isLine ? 'Line' : 'Outline'}>
        <StrokeEditor stroke={el.stroke} onChange={(stroke) => upd((e) => { if (e.type === 'shape') e.stroke = stroke; }, 'stroke')} />
      </Section>
      {!isLine && <TextOptions el={el} />}
    </>
  );
}

const FILTER_PRESETS: { name: string; f: ImageElement['filters'] }[] = [
  { name: 'Original', f: { brightness: 100, contrast: 100, saturate: 100, grayscale: 0, sepia: 0, blur: 0, hue: 0 } },
  { name: 'Mono', f: { brightness: 100, contrast: 110, saturate: 100, grayscale: 100, sepia: 0, blur: 0, hue: 0 } },
  { name: 'Sepia', f: { brightness: 100, contrast: 100, saturate: 100, grayscale: 0, sepia: 80, blur: 0, hue: 0 } },
  { name: 'Vintage', f: { brightness: 105, contrast: 90, saturate: 80, grayscale: 0, sepia: 35, blur: 0, hue: -10 } },
  { name: 'Vivid', f: { brightness: 105, contrast: 115, saturate: 160, grayscale: 0, sepia: 0, blur: 0, hue: 0 } },
  { name: 'Cool', f: { brightness: 100, contrast: 100, saturate: 110, grayscale: 0, sepia: 0, blur: 0, hue: 20 } },
  { name: 'Warm', f: { brightness: 105, contrast: 100, saturate: 120, grayscale: 0, sepia: 25, blur: 0, hue: -15 } },
  { name: 'Dramatic', f: { brightness: 90, contrast: 150, saturate: 90, grayscale: 0, sepia: 0, blur: 0, hue: 0 } },
  { name: 'Fade', f: { brightness: 115, contrast: 80, saturate: 70, grayscale: 0, sepia: 0, blur: 0, hue: 0 } },
  { name: 'Soft focus', f: { brightness: 105, contrast: 95, saturate: 100, grayscale: 0, sepia: 0, blur: 3, hue: 0 } },
];

function ImageOptions({ el }: { el: ImageElement }) {
  const setF = (patch: Partial<ImageElement['filters']>) => upd((e) => { if (e.type === 'image') Object.assign(e.filters, patch); }, `f-${Object.keys(patch)}`);
  const f = el.filters;
  return (
    <>
      <Section title="Picture">
        <div className="btn-row">
          <button className="btn" onClick={async () => {
            const file = await pickFile('image/*');
            if (file) { const src = await fileToDataUrl(file); upd((e) => { if (e.type === 'image') e.src = src; }); }
          }}>Replace picture</button>
        </div>
        <SelectField label="Fit" value={el.fit} options={[{ value: 'cover', label: 'Fill and crop' }, { value: 'contain', label: 'Fit inside' }, { value: 'fill', label: 'Stretch' }]} onChange={(fit) => upd((e) => { if (e.type === 'image') e.fit = fit; })} />
        <RangeField label="Corner radius" value={el.radius} min={0} max={Math.round(Math.min(el.w, el.h) / 2)} onChange={(radius) => upd((e) => { if (e.type === 'image') e.radius = radius; }, 'rad')} />
        <StrokeEditor stroke={el.stroke} onChange={(stroke) => upd((e) => { if (e.type === 'image') e.stroke = stroke; }, 'stroke')} />
      </Section>
      <Section title="Filters">
        <div className="filter-presets">
          {FILTER_PRESETS.map((p) => (
            <button key={p.name} className="filter-chip" onClick={() => upd((e) => { if (e.type === 'image') e.filters = { ...p.f }; })}>
              <img src={el.src} alt="" style={{ filter: `brightness(${p.f.brightness}%) contrast(${p.f.contrast}%) saturate(${p.f.saturate}%) grayscale(${p.f.grayscale}%) sepia(${p.f.sepia}%) hue-rotate(${p.f.hue}deg)` }} />
              <span>{p.name}</span>
            </button>
          ))}
        </div>
        <RangeField label="Brightness" value={f.brightness} min={0} max={200} suffix="%" onChange={(brightness) => setF({ brightness })} />
        <RangeField label="Contrast" value={f.contrast} min={0} max={200} suffix="%" onChange={(contrast) => setF({ contrast })} />
        <RangeField label="Saturation" value={f.saturate} min={0} max={300} suffix="%" onChange={(saturate) => setF({ saturate })} />
        <RangeField label="Greyscale" value={f.grayscale} min={0} max={100} suffix="%" onChange={(grayscale) => setF({ grayscale })} />
        <RangeField label="Sepia" value={f.sepia} min={0} max={100} suffix="%" onChange={(sepia) => setF({ sepia })} />
        <RangeField label="Hue" value={f.hue} min={-180} max={180} suffix="deg" onChange={(hue) => setF({ hue })} />
        <RangeField label="Blur" value={f.blur} min={0} max={30} suffix="px" onChange={(blur) => setF({ blur })} />
      </Section>
    </>
  );
}

function TableOptions({ el }: { el: TableElement }) {
  const t = (fn: (e: Draft<TableElement>) => void, key?: string) => upd((e) => { if (e.type === 'table') fn(e); }, key);
  const cols = el.rows[0]?.length ?? 0;
  return (
    <Section title="Table">
      <div className="btn-row wrap">
        <button className="btn" onClick={() => t((e) => { e.rows.push(Array(cols).fill('')); e.h += e.h / Math.max(1, e.rows.length - 1); })}>Add row</button>
        <button className="btn" onClick={() => t((e) => { if (e.rows.length > 1) { e.rows.pop(); e.h -= e.h / (e.rows.length + 1); } })}>Remove row</button>
        <button className="btn" onClick={() => t((e) => { e.rows.forEach((r, i) => r.push(i === 0 && e.headerRow ? `Header ${r.length + 1}` : '')); })}>Add column</button>
        <button className="btn" onClick={() => t((e) => { if (cols > 1) e.rows.forEach((r) => r.pop()); })}>Remove column</button>
      </div>
      <Toggle label="Header row" checked={el.headerRow} onChange={(v) => t((e) => { e.headerRow = v; })} />
      <Toggle label="Banded rows" checked={el.bandedRows} onChange={(v) => t((e) => { e.bandedRows = v; })} />
      <ColorField label="Header fill" value={el.headerFill} onChange={(v) => t((e) => { e.headerFill = v; }, 'hf')} extra={themeColors()} />
      <ColorField label="Header text" value={el.headerColor} onChange={(v) => t((e) => { e.headerColor = v; }, 'hc')} extra={themeColors()} />
      <ColorField label="Cell fill" value={el.cellFill} onChange={(v) => t((e) => { e.cellFill = v; }, 'cf')} extra={themeColors()} />
      <ColorField label="Band fill" value={el.bandFill} onChange={(v) => t((e) => { e.bandFill = v; }, 'bf')} extra={themeColors()} />
      <ColorField label="Borders" value={el.borderColor} onChange={(v) => t((e) => { e.borderColor = v; }, 'bc')} />
      <SelectField label="Font" value={el.style.fontFamily} options={FONTS.map((f) => ({ value: f, label: f }))} onChange={(v) => t((e) => { e.style.fontFamily = v; })} />
      <div className="row2">
        <NumberField label="Font size" value={el.style.fontSize} min={6} max={200} onChange={(v) => t((e) => { e.style.fontSize = v; }, 'fs')} />
        <ColorField label="Text" value={el.style.color} onChange={(v) => t((e) => { e.style.color = v; }, 'tc')} />
      </div>
      <p className="hint">Double click the table to type in its cells.</p>
    </Section>
  );
}

function ChartOptions({ el }: { el: ChartElement }) {
  const c = (fn: (e: Draft<ChartElement>) => void, key?: string) => upd((e) => { if (e.type === 'chart') fn(e); }, key);
  const csv = [['', ...el.labels].join(','), ...el.series.map((s) => [s.name, ...s.values].join(','))].join('\n');
  return (
    <Section title="Chart">
      <SelectField label="Type" value={el.chartType} options={['column', 'bar', 'line', 'area', 'pie', 'doughnut'].map((v) => ({ value: v as ChartElement['chartType'], label: v[0].toUpperCase() + v.slice(1) }))} onChange={(v) => c((e) => { e.chartType = v; })} />
      <label className="field">
        <span className="field-label">Title</span>
        <input type="text" value={el.title} onChange={(e) => { const v = e.target.value; c((x) => { x.title = v; }, 'title'); }} />
      </label>
      <label className="field">
        <span className="field-label">Data (first row: categories, then one row per series)</span>
        <textarea
          rows={5}
          defaultValue={csv}
          key={el.id}
          onBlur={(e) => {
            const lines = e.target.value.split(/\r?\n/).map((l) => l.split(',').map((x) => x.trim())).filter((l) => l.some(Boolean));
            if (lines.length < 2) return;
            const labels = lines[0].slice(1);
            c((x) => {
              x.labels = labels;
              x.series = lines.slice(1).map((l, i) => ({
                name: l[0] || `Series ${i + 1}`,
                values: labels.map((_, j) => Number(l[j + 1]) || 0),
                color: x.series[i]?.color ?? getTheme(getState().pres.themeId).colors.accents[i % 6],
              }));
            });
          }}
        />
      </label>
      {el.series.map((s, i) => (
        <ColorField key={i} label={s.name} value={s.color} onChange={(v) => c((x) => { x.series[i].color = v; }, `sc${i}`)} extra={themeColors()} />
      ))}
      <Toggle label="Legend" checked={el.showLegend} onChange={(v) => c((x) => { x.showLegend = v; })} />
      <Toggle label="Data labels" checked={el.showValues} onChange={(v) => c((x) => { x.showValues = v; })} />
      <Toggle label="Gridlines" checked={el.showGrid} onChange={(v) => c((x) => { x.showGrid = v; })} />
      <ColorField label="Text colour" value={el.color} onChange={(v) => c((x) => { x.color = v; }, 'cc')} extra={themeColors()} />
    </Section>
  );
}

function VideoOptions({ el }: { el: VideoElement }) {
  const v = (fn: (e: Draft<VideoElement>) => void, key?: string) => upd((e) => { if (e.type === 'video') fn(e); }, key);
  return (
    <Section title="Video">
      <label className="field">
        <span className="field-label">Video address (YouTube, Vimeo or a video file URL)</span>
        <input type="url" value={el.src.startsWith('data:') ? '(uploaded file)' : el.src} onChange={(e) => { const src = e.target.value; v((x) => { x.src = src; }, 'src'); }} />
      </label>
      <Toggle label="Start automatically" checked={el.autoplay} onChange={(b) => v((x) => { x.autoplay = b; })} />
      <Toggle label="Loop" checked={el.loop} onChange={(b) => v((x) => { x.loop = b; })} />
      <Toggle label="Muted" checked={el.muted} onChange={(b) => v((x) => { x.muted = b; })} />
      <p className="hint">Videos play during the slide show.</p>
    </Section>
  );
}

export function BackgroundEditor() {
  const slide = useStore((s) => currentSlide(s));
  const bg = slide.background;
  const set = (patch: Partial<Background>, key = 'bg') => updateSlide((s) => { Object.assign(s.background, patch); }, { coalesce: key });
  return (
    <Section title="Background">
      <FillEditor fill={bg.fill} allowNone={false} onChange={(fill) => set({ fill }, 'bgfill')} />
      <div className="btn-row wrap">
        <button className="btn" onClick={async () => {
          const f = await pickFile('image/*');
          if (f) set({ image: await fileToDataUrl(f), imageFit: 'cover' });
        }}>{bg.image ? 'Change picture' : 'Picture background'}</button>
        {bg.image && <button className="btn" onClick={() => updateSlide((s) => { delete s.background.image; })}>Remove picture</button>}
      </div>
      {bg.image && (
        <>
          <SelectField label="Picture fit" value={bg.imageFit ?? 'cover'} options={[{ value: 'cover', label: 'Fill' }, { value: 'contain', label: 'Fit' }]} onChange={(imageFit) => set({ imageFit })} />
          <RangeField label="Darken" value={Math.round((bg.overlay ?? 0) * 100)} min={0} max={90} suffix="%" onChange={(v) => set({ overlay: v / 100 }, 'overlay')} />
        </>
      )}
      <button className="btn wide" onClick={() => update((d) => { for (const s of d.slides) s.background = structuredClone(bg); })}>Apply to all slides</button>
    </Section>
  );
}

function NotesHint() {
  return <p className="hint">Select an object to format it, or change the slide background below. Right click for more options.</p>;
}

export function Inspector() {
  const selection = useStore((s) => s.ui.selection);
  const slide = useStore((s) => currentSlide(s));
  const els = slide.elements.filter((e) => selection.includes(e.id));
  if (!els.length) {
    return (
      <div className="inspector">
        <h3 className="panel-title">Format slide</h3>
        <NotesHint />
        <BackgroundEditor />
      </div>
    );
  }
  const el = els[0];
  const multi = els.length > 1;
  return (
    <div className="inspector">
      <h3 className="panel-title">{multi ? `${els.length} objects` : `Format ${labelFor(el)}`}</h3>
      {!multi && (
        <label className="field">
          <span className="field-label">Name (used to pair objects for Morph)</span>
          <input type="text" value={el.name ?? ''} placeholder={labelFor(el)} onChange={(e) => { const name = e.target.value; upd((x) => { x.name = name || undefined; }, 'name'); }} />
        </label>
      )}
      {el.type === 'text' && (
        <>
          <TextOptions el={el} />
          <Section title="Box fill and outline" defaultOpen={false}>
            <FillEditor fill={el.fill} onChange={(fill) => upd((e) => { if (e.type === 'text') e.fill = fill; }, 'fill')} />
            <StrokeEditor stroke={el.stroke} onChange={(stroke) => upd((e) => { if (e.type === 'text') e.stroke = stroke; }, 'stroke')} />
            <RangeField label="Corner radius" value={el.radius ?? 0} min={0} max={200} onChange={(radius) => upd((e) => { if (e.type === 'text') e.radius = radius; }, 'rad')} />
          </Section>
        </>
      )}
      {el.type === 'shape' && <ShapeOptions el={el} />}
      {el.type === 'image' && <ImageOptions el={el} />}
      {el.type === 'table' && <TableOptions el={el} />}
      {el.type === 'chart' && <ChartOptions el={el} />}
      {el.type === 'video' && <VideoOptions el={el} />}
      <Geometry el={el} />
      <Effects el={el} />
      <LinkEditor el={el} />
    </div>
  );
}

function labelFor(el: SlideElement): string {
  switch (el.type) {
    case 'text': return 'text box';
    case 'shape': return SHAPES.find((s) => s.kind === el.shape)?.name.toLowerCase() ?? 'shape';
    case 'image': return 'picture';
    case 'table': return 'table';
    case 'chart': return 'chart';
    case 'video': return 'video';
  }
}
