import { useState } from 'react';
import type { ChartType, ShapeKind, TextStyle } from '../model/types';
import { FONTS, THEMES } from '../model/themes';
import { LAYOUTS, SLIDE_SIZES } from '../model/factory';
import { SHAPES, shapePath } from '../model/shapes';
import { fileToDataUrl } from '../model/util';
import { pickFile } from '../io/file';
import {
  canRedo, canUndo, currentSlide, redo, selectedElements, setUI, undo, update, useStore, type RibbonTab,
} from '../store/store';
import {
  addSlide, align, applyTheme, arrange, copySelected, cutSelected, deleteSlide, duplicateSlide, flip,
  groupSelected, insertChart, insertEmoji, insertImage, insertShape, insertSlideNumber, insertTable, insertText,
  paste, pickFormat, resizeSlides, selectAll, setTextStyle, toggleHideSlide, toggleLock, ungroupSelected,
} from './actions';
import { growFont, insertLink, setAlign, setColor, setFont, setFontSize, setHighlight, toggleList, toggleStyle } from './text';
import { startShow } from './show';
import { ColorField, Dropdown, IconButton, NumberField, SelectField } from '../ui/controls';
import { Icon } from '../ui/Icon';

const TABS: { id: RibbonTab; label: string }[] = [
  { id: 'home', label: 'Home' },
  { id: 'insert', label: 'Insert' },
  { id: 'design', label: 'Design' },
  { id: 'transitions', label: 'Transitions' },
  { id: 'animations', label: 'Animations' },
  { id: 'slideshow', label: 'Slide Show' },
  { id: 'view', label: 'View' },
];

function Group({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rgroup">
      <div className="rgroup-body">{children}</div>
      <div className="rgroup-title">{title}</div>
    </div>
  );
}

export function ShapeGallery({ onPick }: { onPick: (k: ShapeKind) => void }) {
  const groups = [...new Set(SHAPES.map((s) => s.group))];
  return (
    <div className="shape-gallery">
      {groups.map((g) => (
        <div key={g}>
          <div className="pop-title">{g}</div>
          <div className="shape-grid">
            {SHAPES.filter((s) => s.group === g).map((s) => (
              <button key={s.kind} title={s.name} aria-label={s.name} onClick={() => onPick(s.kind)}>
                <svg viewBox="-2 -2 28 28" width="26" height="26">
                  <path d={shapePath(s.kind, 24, s.kind === 'line' || s.kind === 'arrowLine' ? 24 : 24, 6)} fill={s.group === 'Lines' ? 'none' : 'currentColor'} fillOpacity={0.25} stroke="currentColor" strokeWidth={1.5} fillRule="evenodd" />
                </svg>
              </button>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

function TablePicker({ onPick }: { onPick: (r: number, c: number) => void }) {
  const [hover, setHover] = useState<[number, number]>([0, 0]);
  return (
    <div className="table-picker">
      <div className="pop-title">{hover[0] ? `${hover[0]} x ${hover[1]} table` : 'Insert table'}</div>
      <div className="tp-grid" onMouseLeave={() => setHover([0, 0])}>
        {Array.from({ length: 8 }, (_, r) => Array.from({ length: 10 }, (_, c) => (
          <button
            key={`${r}-${c}`}
            className={r < hover[0] && c < hover[1] ? 'on' : ''}
            onMouseEnter={() => setHover([r + 1, c + 1])}
            onClick={() => onPick(r + 1, c + 1)}
            aria-label={`${r + 1} by ${c + 1}`}
          />
        )))}
      </div>
    </div>
  );
}

const EMOJIS = ['😀', '😂', '😍', '🤔', '😎', '🥳', '👍', '👏', '🙌', '💪', '🔥', '✨', '⭐', '🎉', '🚀', '💡', '📈', '📉', '📊', '✅', '❌', '⚠️', '❤️', '💬', '📌', '🎯', '🏆', '⏰', '📅', '🌍', '☀️', '🌙', '🍀', '💰', '🔒', '🔑', '📷', '🎵', '🧠', '🤝'];

const CHARTS: { id: ChartType; label: string }[] = [
  { id: 'column', label: 'Column' }, { id: 'bar', label: 'Bar' }, { id: 'line', label: 'Line' },
  { id: 'area', label: 'Area' }, { id: 'pie', label: 'Pie' }, { id: 'doughnut', label: 'Doughnut' },
];

async function uploadImage() {
  const f = await pickFile('image/*');
  if (f) await insertImage(await fileToDataUrl(f));
}

function HomeTab() {
  const sel = useStore((s) => s.ui.selection);
  const editing = useStore((s) => s.ui.editingId);
  const painter = useStore((s) => s.ui.formatPainter);
  useStore((s) => s.pres);
  const [first] = selectedElements().filter((e) => 'style' in e) as { style: TextStyle }[];
  const st = first?.style;
  const hasSel = sel.length > 0;
  return (
    <>
      <Group title="Slides">
        <Dropdown label="New slide" icon="plus">
          {(close) => (
            <div className="menu-list">
              {LAYOUTS.map((l) => <button key={l.id} className="menu-item" onClick={() => { addSlide(l.id); close(); }}>{l.name}</button>)}
            </div>
          )}
        </Dropdown>
        <IconButton icon="duplicate" label="Duplicate slide" onClick={() => duplicateSlide()} />
        <IconButton icon="trash" label="Delete slide" onClick={() => deleteSlide()} />
      </Group>
      <Group title="Clipboard">
        <IconButton icon="paste" label="Paste (Ctrl+V)" onClick={() => paste()} />
        <IconButton icon="cut" label="Cut (Ctrl+X)" onClick={cutSelected} disabled={!hasSel} />
        <IconButton icon="copy" label="Copy (Ctrl+C)" onClick={copySelected} disabled={!hasSel} />
        <IconButton icon="brush" label="Format painter" onClick={() => (painter ? setUI({ formatPainter: null }) : pickFormat())} active={!!painter} disabled={!hasSel && !painter} />
      </Group>
      <Group title="Font">
        <div className="rrow">
          <SelectField value={st?.fontFamily ?? 'Inter'} options={FONTS.map((f) => ({ value: f, label: f }))} onChange={setFont} width={150} />
          <NumberField value={st?.fontSize ?? 36} onChange={setFontSize} min={6} max={600} width={70} />
          <IconButton icon="zoomIn" label="Increase font size" onClick={() => growFont(1)} disabled={!st} />
          <IconButton icon="zoomOut" label="Decrease font size" onClick={() => growFont(-1)} disabled={!st} />
        </div>
        <div className="rrow">
          <IconButton icon="bold" label="Bold (Ctrl+B)" onClick={() => toggleStyle('bold')} active={st?.bold} disabled={!st} />
          <IconButton icon="italic" label="Italic (Ctrl+I)" onClick={() => toggleStyle('italic')} active={st?.italic} disabled={!st} />
          <IconButton icon="underline" label="Underline (Ctrl+U)" onClick={() => toggleStyle('underline')} active={st?.underline} disabled={!st} />
          <IconButton icon="strike" label="Strikethrough" onClick={() => toggleStyle('strike')} active={st?.strike} disabled={!st} />
          <ColorField value={st?.color ?? '#000000'} onChange={setColor} />
          {editing && (
            <Dropdown label="Highlight" icon="highlight" showLabel={false}>
              {(close) => (
                <div className="swatches pad">
                  {['transparent', '#fef08a', '#bbf7d0', '#bfdbfe', '#fbcfe8', '#fed7aa'].map((c) => (
                    <button key={c} className="swatch" style={{ background: c }} aria-label={`Highlight ${c}`} onClick={() => { setHighlight(c); close(); }} />
                  ))}
                </div>
              )}
            </Dropdown>
          )}
        </div>
      </Group>
      <Group title="Paragraph">
        <div className="rrow">
          <IconButton icon="listBullet" label="Bullets" onClick={() => toggleList('ul')} disabled={!st} />
          <IconButton icon="listNumber" label="Numbering" onClick={() => toggleList('ol')} disabled={!st} />
          <SelectField
            value={String(st?.lineHeight ?? 1.25)}
            options={['1', '1.15', '1.25', '1.4', '1.5', '2', '2.5'].map((v) => ({ value: v, label: `Line ${v}` }))}
            onChange={(v) => setTextStyle({ lineHeight: Number(v) })}
            width={90}
          />
        </div>
        <div className="rrow">
          <IconButton icon="alignLeft" label="Align left" onClick={() => setAlign('left')} active={st?.align === 'left'} disabled={!st} />
          <IconButton icon="alignCenter" label="Center" onClick={() => setAlign('center')} active={st?.align === 'center'} disabled={!st} />
          <IconButton icon="alignRight" label="Align right" onClick={() => setAlign('right')} active={st?.align === 'right'} disabled={!st} />
          <IconButton icon="alignJustify" label="Justify" onClick={() => setAlign('justify')} active={st?.align === 'justify'} disabled={!st} />
          <IconButton icon="alignTop" label="Text top" onClick={() => setTextStyle({ vAlign: 'top' })} active={st?.vAlign === 'top'} disabled={!st} />
          <IconButton icon="alignMiddle" label="Text middle" onClick={() => setTextStyle({ vAlign: 'middle' })} active={st?.vAlign === 'middle'} disabled={!st} />
          <IconButton icon="alignBottom" label="Text bottom" onClick={() => setTextStyle({ vAlign: 'bottom' })} active={st?.vAlign === 'bottom'} disabled={!st} />
        </div>
      </Group>
      <Group title="Drawing">
        <Dropdown label="Shapes" icon="shape">{(close) => <ShapeGallery onPick={(k) => { insertShape(k); close(); }} />}</Dropdown>
        <ArrangeMenu />
      </Group>
      <Group title="Editing">
        <IconButton icon="search" label="Find and replace (Ctrl+F)" onClick={() => setUI({ modal: 'find' })} />
        <IconButton icon="sorter" label="Select all (Ctrl+A)" onClick={selectAll} />
      </Group>
    </>
  );
}

export function ArrangeMenu() {
  return (
    <Dropdown label="Arrange" icon="front">
      {(close) => {
        const run = (fn: () => void) => () => { fn(); close(); };
        return (
          <div className="menu-list two-col">
            <div>
              <div className="pop-title">Order</div>
              <button className="menu-item" onClick={run(() => arrange('front'))}><Icon name="front" size={16} />Bring to front</button>
              <button className="menu-item" onClick={run(() => arrange('forward'))}><Icon name="front" size={16} />Bring forward</button>
              <button className="menu-item" onClick={run(() => arrange('backward'))}><Icon name="back" size={16} />Send backward</button>
              <button className="menu-item" onClick={run(() => arrange('back'))}><Icon name="back" size={16} />Send to back</button>
              <div className="pop-title">Group</div>
              <button className="menu-item" onClick={run(groupSelected)}><Icon name="group" size={16} />Group</button>
              <button className="menu-item" onClick={run(ungroupSelected)}><Icon name="group" size={16} />Ungroup</button>
              <button className="menu-item" onClick={run(toggleLock)}><Icon name="lock" size={16} />Lock or unlock</button>
            </div>
            <div>
              <div className="pop-title">Align</div>
              <button className="menu-item" onClick={run(() => align('left'))}><Icon name="alignHLeft" size={16} />Align left</button>
              <button className="menu-item" onClick={run(() => align('center'))}><Icon name="alignHCenter" size={16} />Align center</button>
              <button className="menu-item" onClick={run(() => align('right'))}><Icon name="alignHRight" size={16} />Align right</button>
              <button className="menu-item" onClick={run(() => align('top'))}><Icon name="alignTop" size={16} />Align top</button>
              <button className="menu-item" onClick={run(() => align('middle'))}><Icon name="alignMiddle" size={16} />Align middle</button>
              <button className="menu-item" onClick={run(() => align('bottom'))}><Icon name="alignBottom" size={16} />Align bottom</button>
              <button className="menu-item" onClick={run(() => align('distribute-h'))}><Icon name="distH" size={16} />Distribute horizontally</button>
              <button className="menu-item" onClick={run(() => align('distribute-v'))}><Icon name="distV" size={16} />Distribute vertically</button>
              <button className="menu-item" onClick={run(() => flip('h'))}><Icon name="flipH" size={16} />Flip horizontal</button>
              <button className="menu-item" onClick={run(() => flip('v'))}><Icon name="flipV" size={16} />Flip vertical</button>
            </div>
          </div>
        );
      }}
    </Dropdown>
  );
}

function InsertTab() {
  return (
    <>
      <Group title="Slides">
        <IconButton icon="plus" label="New slide" onClick={() => addSlide()} showLabel />
      </Group>
      <Group title="Text">
        <IconButton icon="text" label="Text box" onClick={() => insertText('box')} showLabel />
        <IconButton icon="text" label="Heading" onClick={() => insertText('title')} showLabel />
        <IconButton icon="hash" label="Slide number" onClick={insertSlideNumber} showLabel />
      </Group>
      <Group title="Media">
        <IconButton icon="image" label="Picture" onClick={uploadImage} showLabel />
        <IconButton icon="link" label="Picture from URL" onClick={() => setUI({ modal: 'image-url' })} showLabel />
        <IconButton icon="video" label="Video" onClick={() => setUI({ modal: 'video' })} showLabel />
      </Group>
      <Group title="Illustrations">
        <Dropdown label="Shapes" icon="shape">{(close) => <ShapeGallery onPick={(k) => { insertShape(k); close(); }} />}</Dropdown>
        <Dropdown label="Table" icon="table">{(close) => <TablePicker onPick={(r, c) => { insertTable(r, c); close(); }} />}</Dropdown>
        <Dropdown label="Chart" icon="chart">
          {(close) => (
            <div className="menu-list">
              {CHARTS.map((c) => <button key={c.id} className="menu-item" onClick={() => { insertChart(c.id); close(); }}>{c.label} chart</button>)}
            </div>
          )}
        </Dropdown>
        <Dropdown label="Emoji" icon="emoji">
          {(close) => (
            <div className="emoji-grid">
              {EMOJIS.map((e) => <button key={e} onClick={() => { insertEmoji(e); close(); }} aria-label={`Insert ${e}`}>{e}</button>)}
            </div>
          )}
        </Dropdown>
      </Group>
      <Group title="Links">
        <IconButton
          icon="link"
          label="Link"
          showLabel
          onClick={() => {
            const url = window.prompt('Link address (https://...)');
            if (url && /^(https?:|mailto:)/i.test(url.trim())) insertLink(url.trim());
          }}
        />
      </Group>
    </>
  );
}

function DesignTab() {
  const pres = useStore((s) => s.pres);
  return (
    <>
      <Group title="Themes">
        <div className="theme-strip">
          {THEMES.map((t) => (
            <button key={t.id} className={`theme-chip ${pres.themeId === t.id ? 'on' : ''}`} title={t.name} onClick={() => applyTheme(t.id)}
              style={{ background: t.titleBackground ? `linear-gradient(135deg, ${t.titleBackground.fill.color}, ${t.titleBackground.fill.color2 ?? t.titleBackground.fill.color})` : t.colors.background }}>
              <span style={{ color: t.titleBackground ? '#fff' : t.colors.text, fontFamily: t.headingFont }}>Aa</span>
              <i style={{ background: t.colors.accents[0] }} />
            </button>
          ))}
        </div>
      </Group>
      <Group title="Customize">
        <Dropdown label="Slide size" icon="layout">
          {(close) => (
            <div className="menu-list">
              {SLIDE_SIZES.map((s) => (
                <button key={s.id} className={`menu-item ${pres.width === s.width && pres.height === s.height ? 'on' : ''}`} onClick={() => { resizeSlides(s.width, s.height); close(); }}>{s.label}</button>
              ))}
            </div>
          )}
        </Dropdown>
        <IconButton icon="image" label="Format background" onClick={() => setUI({ panel: 'design' })} showLabel />
      </Group>
    </>
  );
}

function SlideShowTab() {
  const pres = useStore((s) => s.pres);
  const slide = useStore((s) => currentSlide(s));
  return (
    <>
      <Group title="Start slide show">
        <IconButton icon="play" label="From beginning (F5)" onClick={() => startShow('start')} showLabel />
        <IconButton icon="play" label="From current slide (Shift+F5)" onClick={() => startShow('current')} showLabel />
        <IconButton icon="presenter" label="Presenter view" onClick={() => startShow('current', true)} showLabel />
      </Group>
      <Group title="Set up">
        <IconButton icon={slide.hidden ? 'eye' : 'eyeOff'} label={slide.hidden ? 'Unhide slide' : 'Hide slide'} onClick={() => toggleHideSlide()} showLabel active={slide.hidden} />
        <IconButton icon="undo" label="Loop until Esc" onClick={() => update((d) => { d.settings.loop = !d.settings.loop; })} showLabel active={pres.settings.loop} />
        <IconButton icon="hash" label="Slide numbers" onClick={() => update((d) => { d.settings.showSlideNumbers = !d.settings.showSlideNumbers; })} showLabel active={pres.settings.showSlideNumbers} />
      </Group>
    </>
  );
}

function ViewTab() {
  const ui = useStore((s) => s.ui);
  return (
    <>
      <Group title="Presentation views">
        <IconButton icon="layout" label="Normal" onClick={() => setUI({ view: 'normal' })} active={ui.view === 'normal'} showLabel />
        <IconButton icon="sorter" label="Slide sorter" onClick={() => setUI({ view: 'sorter' })} active={ui.view === 'sorter'} showLabel />
        <IconButton icon="notes" label="Notes" onClick={() => setUI({ showNotes: !ui.showNotes })} active={ui.showNotes} showLabel />
      </Group>
      <Group title="Show">
        <IconButton icon="grid" label="Gridlines" onClick={() => setUI({ showGrid: !ui.showGrid })} active={ui.showGrid} showLabel />
        <IconButton icon="alignHCenter" label="Smart guides" onClick={() => setUI({ snap: !ui.snap })} active={ui.snap} showLabel />
      </Group>
      <Group title="Zoom">
        <IconButton icon="zoomIn" label="Zoom in" onClick={() => setUI((u) => ({ zoom: Math.min(4, (typeof u.zoom === 'number' ? u.zoom : 0.5) * 1.2) }))} />
        <IconButton icon="zoomOut" label="Zoom out" onClick={() => setUI((u) => ({ zoom: Math.max(0.1, (typeof u.zoom === 'number' ? u.zoom : 0.5) / 1.2) }))} />
        <IconButton icon="layout" label="Fit to window" onClick={() => setUI({ zoom: 'fit' })} active={ui.zoom === 'fit'} showLabel />
      </Group>
      <Group title="App">
        <IconButton
          icon={ui.appTheme === 'dark' ? 'sun' : 'moon'}
          label={ui.appTheme === 'dark' ? 'Light mode' : 'Dark mode'}
          showLabel
          onClick={() => {
            const next = ui.appTheme === 'dark' ? 'light' : 'dark';
            try { localStorage.setItem('visary-app-theme', next); } catch { /* ignore */ }
            setUI({ appTheme: next });
          }}
        />
        <IconButton icon="keyboard" label="Shortcuts" onClick={() => setUI({ modal: 'shortcuts' })} showLabel />
      </Group>
    </>
  );
}

export function Ribbon() {
  const tab = useStore((s) => s.ui.tab);
  useStore((s) => s.dirty);
  const choose = (t: RibbonTab) => {
    const panel = t === 'transitions' ? 'transitions' : t === 'animations' ? 'animations' : t === 'design' ? 'design' : undefined;
    setUI(panel ? { tab: t, panel } : { tab: t });
  };
  return (
    <div className="ribbon">
      <div className="ribbon-tabs" role="tablist">
        <IconButton icon="undo" label="Undo (Ctrl+Z)" onClick={undo} disabled={!canUndo()} />
        <IconButton icon="redo" label="Redo (Ctrl+Y)" onClick={redo} disabled={!canRedo()} />
        <span className="tab-sep" />
        {TABS.map((t) => (
          <button key={t.id} role="tab" aria-selected={tab === t.id} className={`rtab ${tab === t.id ? 'on' : ''}`} onClick={() => choose(t.id)}>{t.label}</button>
        ))}
      </div>
      <div className="ribbon-body">
        {tab === 'home' && <HomeTab />}
        {tab === 'insert' && <InsertTab />}
        {tab === 'design' && <DesignTab />}
        {tab === 'transitions' && <TransitionsRibbon />}
        {tab === 'animations' && <AnimationsRibbon />}
        {tab === 'slideshow' && <SlideShowTab />}
        {tab === 'view' && <ViewTab />}
      </div>
    </div>
  );
}

function TransitionsRibbon() {
  const slide = useStore((s) => currentSlide(s));
  return (
    <>
      <Group title="Preview">
        <IconButton icon="play" label="Preview transition" onClick={() => setUI((u) => ({ previewTransition: u.previewTransition + 1 }))} showLabel />
      </Group>
      <Group title="Current">
        <div className="ribbon-note">Pick an effect, direction, speed and timing in the Transitions pane on the right. Current: <strong>{slide.transition.type}</strong></div>
      </Group>
    </>
  );
}

function AnimationsRibbon() {
  const sel = useStore((s) => s.ui.selection);
  return (
    <>
      <Group title="Preview">
        <IconButton icon="play" label="Preview animations" onClick={() => setUI((u) => ({ previewAnimations: u.previewAnimations + 1 }))} showLabel />
      </Group>
      <Group title="Add">
        <div className="ribbon-note">{sel.length ? 'Choose an entrance, emphasis, exit or motion path effect in the Animation pane.' : 'Select an object on the slide to animate it.'}</div>
      </Group>
    </>
  );
}
