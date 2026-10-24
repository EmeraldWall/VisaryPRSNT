import { useEffect, useState, type ReactNode } from 'react';

/** Fits a W x H logical canvas into its parent, centered with letterboxing. */
export function useFitScale(el: HTMLElement | null, W: number, H: number): number {
  const [scale, setScale] = useState(1);
  useEffect(() => {
    if (!el) return;
    const measure = () => {
      const r = el.getBoundingClientRect();
      setScale(Math.max(0.01, Math.min(r.width / W, r.height / H)));
    };
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, [el, W, H]);
  return scale;
}

export function ScaledBox({ W, H, scale, children, className }: { W: number; H: number; scale: number; children: ReactNode; className?: string }) {
  return (
    <div className={className} style={{ width: W * scale, height: H * scale, position: 'relative', flex: 'none' }}>
      <div className="tr-stage" style={{ width: W, height: H, transform: `scale(${scale})`, transformOrigin: '0 0' }}>{children}</div>
    </div>
  );
}
