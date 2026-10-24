import { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { SlideContent } from '../render/SlideView';
import { setUI, useStore } from '../store/store';

/** Renders every visible slide as its own page and opens the browser print dialog (Save as PDF). */
export function PrintView() {
  const pres = useStore((s) => s.pres);
  const printing = useStore((s) => s.ui.printing);

  useEffect(() => {
    if (!printing) return;
    const style = document.createElement('style');
    style.textContent = `@page { size: ${pres.width}px ${pres.height}px; margin: 0; }`;
    document.head.appendChild(style);
    const done = () => setUI({ printing: false });
    const imgs = [...document.querySelectorAll<HTMLImageElement>('.print-root img')];
    const ready = Promise.all([
      document.fonts?.ready,
      ...imgs.map((img) => (img.complete ? null : new Promise((r) => { img.onload = r; img.onerror = r; }))),
    ]);
    let cancelled = false;
    ready.then(() => {
      if (cancelled) return;
      window.addEventListener('afterprint', done, { once: true });
      window.print();
      // Some browsers do not fire afterprint, so fall back to a timer.
      window.setTimeout(done, 1500);
    });
    return () => { cancelled = true; style.remove(); };
  }, [printing, pres.width, pres.height]);

  if (!printing) return null;
  return createPortal(
    <div className="print-root">
      {pres.slides.filter((s) => !s.hidden).map((s) => (
        <div key={s.id} className="print-page" style={{ width: pres.width, height: pres.height }}>
          <SlideContent slide={s} W={pres.width} H={pres.height} mode="print" slideNumber={pres.slides.indexOf(s) + 1} showSlideNumber={pres.settings.showSlideNumbers} />
        </div>
      ))}
    </div>,
    document.body,
  );
}
