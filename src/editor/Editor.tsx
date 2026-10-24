import { useEffect, useState } from 'react';
import { getTheme } from '../model/themes';
import { fileToDataUrl } from '../model/util';
import { exportJson, importJsonFile, pickFile, FILE_EXT } from '../io/file';
import { saveDoc } from '../io/storage';
import {
  currentIndex, currentSlide, getState, redo, setUI, subscribe, undo, update, updateSlide, useStore, type SidePanel,
} from '../store/store';
import { SlideShow } from '../present/SlideShow';
import {
  addSlide, arrange, copySelected, cutSelected, deleteSelected, duplicateSelected, goToSlide, groupSelected,
  insertImage, insertPlainText, nudge, paste, selectAll, ungroupSelected,
} from './actions';
import { toggleStyle } from './text';
import { startShow } from './show';
import { Canvas } from './Canvas';
import { SlidePanel } from './SlidePanel';
import { Ribbon } from './Ribbon';
import { Inspector } from './Inspector';
import { TransitionsPanel } from './TransitionsPanel';
import { AnimationsPanel } from './AnimationsPanel';
import { DesignPanel } from './DesignPanel';
import { Sorter } from './Sorter';
import { Dialogs } from './Dialogs';
import { PrintView } from './PrintView';
import { TransitionPreview, useAnimationPreview } from './Previews';
import { Dropdown, IconButton } from '../ui/controls';
import { Icon } from '../ui/Icon';

const CLIP_MARK = 'visary-elements';

function isTyping(target: EventTarget | null): boolean {
  const el = target as HTMLElement | null;
  if (!el) return false;
  return el.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(el.tagName);
}

function useAutosave(): 'saved' | 'saving' | 'error' {
  const [status, setStatus] = useState<'saved' | 'saving' | 'error'>('saved');
  useEffect(() => {
    let timer: number | undefined;
    let last = getState().dirty;
    const unsub = subscribe(() => {
      const d = getState().dirty;
      if (d === last) return;
      last = d;
      setStatus('saving');
      window.clearTimeout(timer);
      timer = window.setTimeout(() => {
        saveDoc(getState().pres).then(() => setStatus('saved'), () => setStatus('error'));
      }, 700);
    });
    const flush = () => { void saveDoc(getState().pres); };
    window.addEventListener('beforeunload', flush);
    return () => { unsub(); window.clearTimeout(timer); window.removeEventListener('beforeunload', flush); flush(); };
  }, []);
  return status;
}

function useShortcuts() {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const ui = getState().ui;
      if (ui.presenting || ui.modal) return;
      const mod = e.ctrlKey || e.metaKey;
      const k = e.key.toLowerCase();
      if (e.key === 'F5') {
        e.preventDefault();
        void startShow(e.shiftKey ? 'current' : 'start');
        return;
      }
      if (mod && k === 's') { e.preventDefault(); exportJson(getState().pres); return; }
      if (mod && k === 'f') { e.preventDefault(); setUI({ modal: 'find' }); return; }
      if (mod && k === 'm') { e.preventDefault(); addSlide(); return; }
      if (isTyping(e.target)) {
        if (e.key === 'Escape' && ui.editingId) {
          (e.target as HTMLElement).blur();
          setUI({ editingId: null });
        }
        return;
      }
      if (mod && k === 'z') { e.preventDefault(); if (e.shiftKey) redo(); else undo(); return; }
      if (mod && k === 'y') { e.preventDefault(); redo(); return; }
      if (mod && k === 'a') { e.preventDefault(); selectAll(); return; }
      if (mod && k === 'd') { e.preventDefault(); duplicateSelected(); return; }
      if (mod && k === 'g') { e.preventDefault(); if (e.shiftKey) ungroupSelected(); else groupSelected(); return; }
      if (mod && k === 'b') { e.preventDefault(); toggleStyle('bold'); return; }
      if (mod && k === 'i') { e.preventDefault(); toggleStyle('italic'); return; }
      if (mod && k === 'u') { e.preventDefault(); toggleStyle('underline'); return; }
      if (mod && e.key === ']') { e.preventDefault(); arrange(e.shiftKey ? 'front' : 'forward'); return; }
      if (mod && e.key === '[') { e.preventDefault(); arrange(e.shiftKey ? 'back' : 'backward'); return; }
      if (e.key === 'Delete' || e.key === 'Backspace') {
        if (ui.selection.length) { e.preventDefault(); deleteSelected(); }
        return;
      }
      if (e.key === 'Escape') { setUI({ selection: [], editingId: null, formatPainter: null }); return; }
      if (e.key === 'Enter' && ui.selection.length === 1) {
        const el = currentSlide().elements.find((x) => x.id === ui.selection[0]);
        if (el && (el.type === 'text' || el.type === 'shape' || el.type === 'table')) { e.preventDefault(); setUI({ editingId: el.id }); }
        return;
      }
      if (e.key === 'PageDown') { e.preventDefault(); goToSlide(1); return; }
      if (e.key === 'PageUp') { e.preventDefault(); goToSlide(-1); return; }
      if (e.key.startsWith('Arrow')) {
        const step = e.shiftKey ? 10 : 1;
        const dx = e.key === 'ArrowLeft' ? -step : e.key === 'ArrowRight' ? step : 0;
        const dy = e.key === 'ArrowUp' ? -step : e.key === 'ArrowDown' ? step : 0;
        if (ui.selection.length) { e.preventDefault(); nudge(dx, dy); }
        else if (dy || dx) { e.preventDefault(); goToSlide(dx + dy > 0 ? 1 : -1); }
      }
    };
    const onCopy = (e: ClipboardEvent) => {
      if (isTyping(e.target) || !getState().ui.selection.length || getState().ui.presenting) return;
      e.preventDefault();
      e.clipboardData?.setData('text/plain', CLIP_MARK);
      copySelected();
    };
    const onCut = (e: ClipboardEvent) => {
      if (isTyping(e.target) || !getState().ui.selection.length || getState().ui.presenting) return;
      e.preventDefault();
      e.clipboardData?.setData('text/plain', CLIP_MARK);
      cutSelected();
    };
    const onPaste = async (e: ClipboardEvent) => {
      if (isTyping(e.target) || getState().ui.presenting) return;
      const data = e.clipboardData;
      if (!data) return;
      const image = [...data.files].find((f) => f.type.startsWith('image/'));
      e.preventDefault();
      if (image) { await insertImage(await fileToDataUrl(image)); return; }
      const text = data.getData('text/plain');
      if (!text || text === CLIP_MARK) { paste(); return; }
      insertPlainText(text);
    };
    window.addEventListener('keydown', onKey);
    window.addEventListener('copy', onCopy);
    window.addEventListener('cut', onCut);
    window.addEventListener('paste', onPaste);
    return () => {
      window.removeEventListener('keydown', onKey);
      window.removeEventListener('copy', onCopy);
      window.removeEventListener('cut', onCut);
      window.removeEventListener('paste', onPaste);
    };
  }, []);
}

function Notes() {
  const slide = useStore((s) => currentSlide(s));
  return (
    <div className="notes">
      <textarea
        aria-label="Speaker notes"
        placeholder="Click to add speaker notes"
        value={slide.notes}
        onChange={(e) => { const notes = e.target.value; updateSlide((s) => { s.notes = notes; }, { coalesce: `notes-${slide.id}` }); }}
      />
    </div>
  );
}

function StatusBar({ saveStatus }: { saveStatus: string }) {
  const pres = useStore((s) => s.pres);
  const index = useStore((s) => currentIndex(s));
  const zoom = useStore((s) => s.ui.zoom);
  const view = useStore((s) => s.ui.view);
  const showNotes = useStore((s) => s.ui.showNotes);
  return (
    <footer className="statusbar">
      <span>Slide {index + 1} of {pres.slides.length}</span>
      <span>{getTheme(pres.themeId).name}</span>
      <span className={`save-${saveStatus}`}>{saveStatus === 'saving' ? 'Saving...' : saveStatus === 'error' ? 'Could not save in this browser' : 'All changes saved in this browser'}</span>
      <span className="grow" />
      <IconButton icon="notes" label="Notes" onClick={() => setUI({ showNotes: !showNotes })} active={showNotes} />
      <IconButton icon="layout" label="Normal view" onClick={() => setUI({ view: 'normal' })} active={view === 'normal'} />
      <IconButton icon="sorter" label="Slide sorter" onClick={() => setUI({ view: 'sorter' })} active={view === 'sorter'} />
      <IconButton icon="play" label="Slide show from current slide" onClick={() => startShow('current')} />
      <input
        type="range"
        min={10}
        max={300}
        value={Math.round((typeof zoom === 'number' ? zoom : 0.5) * 100)}
        onChange={(e) => setUI({ zoom: Number(e.target.value) / 100 })}
        aria-label="Zoom"
      />
      <button className="zoom-label" onClick={() => setUI({ zoom: 'fit' })} title="Fit slide to window">{zoom === 'fit' ? 'Fit' : `${Math.round(zoom * 100)}%`}</button>
    </footer>
  );
}

function TopBar({ onHome, onOpen }: { onHome: () => void; onOpen: (id: string) => void }) {
  const title = useStore((s) => s.pres.title);
  const [exporting, setExporting] = useState(false);
  return (
    <header className="topbar">
      <button className="logo" onClick={onHome} title="My presentations"><span className="logo-mark">V</span><span className="logo-text">Visary</span></button>
      <input
        className="doc-title"
        value={title}
        aria-label="Presentation title"
        onChange={(e) => { const t = e.target.value; update((d) => { d.title = t; }, { coalesce: 'title' }); }}
      />
      <Dropdown label="File" icon="file">
        {(close) => (
          <div className="menu-list">
            <button className="menu-item" onClick={() => { close(); onHome(); }}><Icon name="home" size={16} />My presentations and templates</button>
            <button className="menu-item" onClick={async () => {
              close();
              const f = await pickFile(`${FILE_EXT},.json,application/json`);
              if (!f) return;
              try {
                const p = await importJsonFile(f);
                await saveDoc(p);
                onOpen(p.id);
              } catch (err) {
                window.alert((err as Error).message || 'Could not open this file.');
              }
            }}><Icon name="folder" size={16} />Open from computer ({FILE_EXT})</button>
            <button className="menu-item" onClick={() => { close(); exportJson(getState().pres); }}><Icon name="download" size={16} />Save a copy to computer</button>
            <div className="pop-title">Export</div>
            <button className="menu-item" disabled={exporting} onClick={async () => {
              close();
              setExporting(true);
              try {
                const { exportPptx } = await import('../io/pptx');
                await exportPptx(getState().pres);
              } catch (err) {
                console.error(err);
                window.alert('PowerPoint export failed. See the browser console for details.');
              } finally {
                setExporting(false);
              }
            }}><Icon name="download" size={16} />PowerPoint (.pptx)</button>
            <button className="menu-item" onClick={() => { close(); setUI({ printing: true }); }}><Icon name="download" size={16} />PDF or print</button>
          </div>
        )}
      </Dropdown>
      {exporting && <span className="hint">Exporting...</span>}
      <span className="grow" />
      <div className="present-group">
        <button className="btn primary present-btn" onClick={() => startShow('current')}><Icon name="play" size={16} /> Present</button>
        <Dropdown label="Present options" showLabel={false} className="present-more">
          {(close) => (
            <div className="menu-list">
              <button className="menu-item" onClick={() => { close(); void startShow('start'); }}>From beginning (F5)</button>
              <button className="menu-item" onClick={() => { close(); void startShow('current'); }}>From current slide (Shift+F5)</button>
              <button className="menu-item" onClick={() => { close(); void startShow('current', true); }}>Presenter view (opens a second window)</button>
            </div>
          )}
        </Dropdown>
      </div>
    </header>
  );
}

const PANELS: { id: SidePanel; label: string }[] = [
  { id: 'format', label: 'Format' },
  { id: 'design', label: 'Design' },
  { id: 'transitions', label: 'Transitions' },
  { id: 'animations', label: 'Animate' },
];

export function Editor({ onHome, onOpen }: { onHome: () => void; onOpen: (id: string) => void }) {
  const view = useStore((s) => s.ui.view);
  const panel = useStore((s) => s.ui.panel);
  const showNotes = useStore((s) => s.ui.showNotes);
  const presenting = useStore((s) => s.ui.presenting);
  const appTheme = useStore((s) => s.ui.appTheme);
  const previewTransition = useStore((s) => s.ui.previewTransition);
  const pres = useStore((s) => s.pres);
  const [previewing, setPreviewing] = useState(0);
  const saveStatus = useAutosave();
  useShortcuts();
  useAnimationPreview();

  useEffect(() => { if (previewTransition) setPreviewing(previewTransition); }, [previewTransition]);
  useEffect(() => { document.title = `${pres.title} - Visary PRSNT`; }, [pres.title]);
  useEffect(() => { document.documentElement.dataset.theme = appTheme; }, [appTheme]);

  return (
    <div className="editor">
      <TopBar onHome={onHome} onOpen={onOpen} />
      <Ribbon />
      <div className="workspace">
        {view === 'normal' && <SlidePanel />}
        <main className="main-area">
          {view === 'normal' ? (
            <>
              <Canvas />
              {showNotes && <Notes />}
            </>
          ) : <Sorter />}
          {previewing > 0 && <TransitionPreview key={previewing} onDone={() => setPreviewing(0)} />}
        </main>
        <aside className="side-panel">
          <div className="side-tabs" role="tablist">
            {PANELS.map((p) => (
              <button key={p.id} role="tab" aria-selected={panel === p.id} className={panel === p.id ? 'on' : ''} onClick={() => setUI({ panel: p.id })}>{p.label}</button>
            ))}
          </div>
          <div className="side-body">
            {panel === 'format' && <Inspector />}
            {panel === 'design' && <DesignPanel />}
            {panel === 'transitions' && <TransitionsPanel />}
            {panel === 'animations' && <AnimationsPanel />}
          </div>
        </aside>
      </div>
      <StatusBar saveStatus={saveStatus} />
      <Dialogs />
      <PrintView />
      {presenting && (
        <SlideShow
          pres={pres}
          startIndex={presenting.startIndex}
          onExit={(last) => setUI({ presenting: null, currentSlideId: getState().pres.slides[last]?.id ?? getState().ui.currentSlideId })}
        />
      )}
    </div>
  );
}
