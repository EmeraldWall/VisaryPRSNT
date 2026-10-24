import { useState } from 'react';
import { SlideThumb } from '../render/SlideView';
import { setUI, useStore } from '../store/store';
import { addSlide, deleteSlide, duplicateSlide, moveSlide, toggleHideSlide } from './actions';
import { IconButton } from '../ui/controls';
import { Icon } from '../ui/Icon';
import { LAYOUTS } from '../model/factory';

export function SlidePanel() {
  const pres = useStore((s) => s.pres);
  const currentId = useStore((s) => s.ui.currentSlideId);
  const [dragId, setDragId] = useState<string | null>(null);
  const [dropAt, setDropAt] = useState<number | null>(null);
  const [menu, setMenu] = useState<{ id: string; x: number; y: number } | null>(null);
  const [showLayouts, setShowLayouts] = useState(false);

  return (
    <aside className="slide-panel" aria-label="Slides">
      <div className="slide-panel-head">
        <button className="new-slide-btn" onClick={() => addSlide()}><Icon name="plus" size={16} /> New slide</button>
        <IconButton icon="layout" label="New slide with layout" onClick={() => setShowLayouts((v) => !v)} active={showLayouts} />
      </div>
      {showLayouts && (
        <div className="layout-list">
          {LAYOUTS.map((l) => (
            <button key={l.id} onClick={() => { addSlide(l.id); setShowLayouts(false); }}>{l.name}</button>
          ))}
        </div>
      )}
      <ol className="thumbs" onDragOver={(e) => e.preventDefault()}>
        {pres.slides.map((s, i) => (
          <li
            key={s.id}
            className={`thumb-item ${s.id === currentId ? 'current' : ''} ${s.hidden ? 'is-hidden' : ''} ${dropAt === i ? 'drop-before' : ''} ${dropAt === i + 1 && i === pres.slides.length - 1 ? 'drop-after' : ''}`}
            draggable
            onDragStart={(e) => { setDragId(s.id); e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', s.id); }}
            onDragOver={(e) => {
              e.preventDefault();
              const r = e.currentTarget.getBoundingClientRect();
              setDropAt(e.clientY < r.top + r.height / 2 ? i : i + 1);
            }}
            onDragEnd={() => { setDragId(null); setDropAt(null); }}
            onDrop={(e) => {
              e.preventDefault();
              if (dragId !== null && dropAt !== null) moveSlide(dragId, dropAt);
              setDragId(null);
              setDropAt(null);
            }}
            onClick={() => setUI({ currentSlideId: s.id, selection: [], editingId: null })}
            onContextMenu={(e) => { e.preventDefault(); setUI({ currentSlideId: s.id }); setMenu({ id: s.id, x: e.clientX, y: e.clientY }); }}
          >
            <span className="thumb-num">{i + 1}</span>
            <div className="thumb-frame">
              <SlideThumb slide={s} W={pres.width} H={pres.height} width={168} slideNumber={i + 1} showSlideNumber={pres.settings.showSlideNumbers} />
              {s.transition.type !== 'none' && <span className="thumb-badge" title={`Transition: ${s.transition.type}`}><Icon name="transition" size={12} /></span>}
              {s.animations.length > 0 && <span className="thumb-badge anim" title={`${s.animations.length} animations`}><Icon name="star" size={12} /></span>}
            </div>
          </li>
        ))}
      </ol>
      {menu && (
        <div className="context-menu popover" style={{ left: menu.x, top: menu.y }} onMouseLeave={() => setMenu(null)}>
          <div className="menu-group">
            <button className="menu-item" onClick={() => { addSlide(); setMenu(null); }}>New slide</button>
            <button className="menu-item" onClick={() => { duplicateSlide(menu.id); setMenu(null); }}>Duplicate slide</button>
            <button className="menu-item" onClick={() => { deleteSlide(menu.id); setMenu(null); }}>Delete slide</button>
            <button className="menu-item" onClick={() => { toggleHideSlide(menu.id); setMenu(null); }}>Hide or show slide</button>
          </div>
        </div>
      )}
    </aside>
  );
}
