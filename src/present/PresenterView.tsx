import { useEffect, useState } from 'react';
import type { Presentation } from '../model/types';
import { SlideThumb } from '../render/SlideView';
import { showChannel, type ShowCommand, type ShowState } from './channel';

function fmt(ms: number): string {
  const s = Math.floor(ms / 1000);
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  return `${h ? `${h}:` : ''}${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`;
}

/** Second window for the speaker: current and next slide, notes, timer and controls. */
export function PresenterView({ pres }: { pres: Presentation }) {
  const [state, setState] = useState<ShowState | null>(null);
  const [start, setStart] = useState(Date.now());
  const [now, setNow] = useState(Date.now());
  const [paused, setPaused] = useState<number | null>(null);
  const [noteSize, setNoteSize] = useState(22);

  useEffect(() => {
    document.title = `Presenter view: ${pres.title}`;
    const ch = showChannel();
    if (!ch) return;
    const onMsg = (e: MessageEvent<ShowState>) => {
      if (e.data?.type === 'state' && e.data.docId === pres.id) setState(e.data);
    };
    ch.addEventListener('message', onMsg);
    ch.postMessage({ type: 'hello', docId: pres.id });
    return () => ch.removeEventListener('message', onMsg);
  }, [pres]);

  useEffect(() => {
    const t = window.setInterval(() => setNow(Date.now()), 500);
    return () => window.clearInterval(t);
  }, []);

  const send = (cmd: ShowCommand['cmd'], index?: number) => {
    showChannel()?.postMessage({ type: 'cmd', docId: pres.id, cmd, index } satisfies ShowCommand);
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (['ArrowRight', 'ArrowDown', 'PageDown', ' ', 'Enter', 'n'].includes(e.key)) { e.preventDefault(); send('next'); }
      if (['ArrowLeft', 'ArrowUp', 'PageUp', 'Backspace', 'p'].includes(e.key)) { e.preventDefault(); send('prev'); }
      if (e.key === 'b' || e.key === '.') send('black');
      if (e.key === 'w' || e.key === ',') send('white');
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  });

  const index = state?.index ?? 0;
  const cur = pres.slides[index];
  let nextIndex = index + 1;
  while (nextIndex < pres.slides.length && pres.slides[nextIndex].hidden) nextIndex++;
  const nextSlide = pres.slides[nextIndex];
  const elapsed = (paused ?? now) - start;

  if (!state || state.closed) {
    return (
      <div className="pv pv-wait">
        <h2>Presenter view</h2>
        <p>{state?.closed ? 'The slide show has ended. You can close this window.' : 'Waiting for the slide show to start in the main window.'}</p>
      </div>
    );
  }

  return (
    <div className="pv">
      <header className="pv-top">
        <div className="pv-timer">
          <span className="pv-big">{fmt(elapsed)}</span>
          <button onClick={() => setPaused(paused ? null : Date.now())}>{paused ? 'Resume' : 'Pause'}</button>
          <button onClick={() => { setStart(Date.now()); setPaused(null); }}>Reset</button>
        </div>
        <div className="pv-clock">{new Date(now).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</div>
        <div className="pv-count">Slide {index + 1} of {pres.slides.length}{state.steps ? `, step ${state.step} of ${state.steps}` : ''}{state.ended ? ' (end)' : ''}</div>
      </header>
      <main className="pv-main">
        <section className="pv-current">
          {cur && <SlideThumb slide={cur} W={pres.width} H={pres.height} width={Math.min(760, window.innerWidth * 0.55)} slideNumber={index + 1} />}
          <div className="pv-nav">
            <button onClick={() => send('prev')}>Previous</button>
            <button className="primary" onClick={() => send('next')}>Next</button>
            <button onClick={() => send('black')}>Black screen</button>
            <button onClick={() => send('end')}>End show</button>
          </div>
        </section>
        <aside className="pv-side">
          <h3>Next</h3>
          {nextSlide ? <SlideThumb slide={nextSlide} W={pres.width} H={pres.height} width={340} slideNumber={nextIndex + 1} /> : <div className="pv-none">End of show</div>}
          <div className="pv-notes-head">
            <h3>Notes</h3>
            <span>
              <button onClick={() => setNoteSize((s) => Math.max(12, s - 2))} aria-label="Smaller notes">A-</button>
              <button onClick={() => setNoteSize((s) => Math.min(48, s + 2))} aria-label="Larger notes">A+</button>
            </span>
          </div>
          <div className="pv-notes" style={{ fontSize: noteSize }}>{cur?.notes || 'No notes for this slide.'}</div>
        </aside>
      </main>
      <footer className="pv-strip">
        {pres.slides.map((s, i) => (
          <button key={s.id} className={`pv-thumb ${i === index ? 'on' : ''}`} onClick={() => send('goto', i)}>
            <SlideThumb slide={s} W={pres.width} H={pres.height} width={120} slideNumber={i + 1} />
          </button>
        ))}
      </footer>
    </div>
  );
}
