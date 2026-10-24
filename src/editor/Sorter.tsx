import { useState } from 'react';
import { SlideThumb } from '../render/SlideView';
import { setUI, useStore } from '../store/store';
import { getTransition } from '../present/transitions';
import { moveSlide } from './actions';

export function Sorter() {
  const pres = useStore((s) => s.pres);
  const currentId = useStore((s) => s.ui.currentSlideId);
  const [dragId, setDragId] = useState<string | null>(null);
  const [dropAt, setDropAt] = useState<number | null>(null);
  return (
    <div className="sorter" onDragOver={(e) => e.preventDefault()}>
      {pres.slides.map((s, i) => (
        <div
          key={s.id}
          className={`sorter-item ${s.id === currentId ? 'current' : ''} ${s.hidden ? 'is-hidden' : ''} ${dropAt === i ? 'drop-before' : ''}`}
          draggable
          onDragStart={() => setDragId(s.id)}
          onDragOver={(e) => {
            e.preventDefault();
            const r = e.currentTarget.getBoundingClientRect();
            setDropAt(e.clientX < r.left + r.width / 2 ? i : i + 1);
          }}
          onDrop={() => { if (dragId && dropAt !== null) moveSlide(dragId, dropAt); setDragId(null); setDropAt(null); }}
          onDragEnd={() => { setDragId(null); setDropAt(null); }}
          onClick={() => setUI({ currentSlideId: s.id })}
          onDoubleClick={() => setUI({ currentSlideId: s.id, view: 'normal' })}
        >
          <SlideThumb slide={s} W={pres.width} H={pres.height} width={260} slideNumber={i + 1} showSlideNumber={pres.settings.showSlideNumbers} />
          <div className="sorter-meta">
            <span>{i + 1}</span>
            <span>{s.transition.type !== 'none' ? getTransition(s.transition.type).name : ''}{s.transition.advanceAfter !== null ? `, ${s.transition.advanceAfter}s` : ''}</span>
          </div>
        </div>
      ))}
    </div>
  );
}
