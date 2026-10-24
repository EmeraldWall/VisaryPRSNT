import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { Slide } from '../model/types';
import { getTheme } from '../model/themes';
import { SlideContent } from '../render/SlideView';
import { createController, runTransition } from '../present/transitions';
import { buildSteps, initiallyHidden, resetSlideAnimations, runStep } from '../present/animations';
import { ScaledBox, useFitScale } from '../present/Stage';
import { currentIndex, currentSlide, getState, useStore } from '../store/store';

const BLACK: Slide = {
  id: 'preview-black', elements: [], background: { fill: { type: 'solid', color: '#000000' } }, notes: '',
  transition: { type: 'none', duration: 0, easing: 'linear', advanceOnClick: true, advanceAfter: null }, animations: [],
};

/** Plays the current slide's transition over the canvas. */
export function TransitionPreview({ onDone }: { onDone: () => void }) {
  const { pres } = getState();
  const index = currentIndex();
  const to = currentSlide();
  const from = index > 0 ? pres.slides[index - 1] : BLACK;
  const [host, setHost] = useState<HTMLDivElement | null>(null);
  const scale = useFitScale(host, pres.width, pres.height);
  const fromRef = useRef<HTMLDivElement>(null);
  const toRef = useRef<HTMLDivElement>(null);
  const fxRef = useRef<HTMLDivElement>(null);
  const ctl = useRef(createController());

  useLayoutEffect(() => {
    if (!fromRef.current || !toRef.current || !fxRef.current) return;
    const t = to.transition;
    let cancelled = false;
    const c = ctl.current;
    void runTransition({
      from: fromRef.current, to: toRef.current, fx: fxRef.current, W: pres.width, H: pres.height,
      duration: t.duration, dir: t.direction ?? '', easing: t.easing, accent: getTheme(pres.themeId).colors.accents[0],
      ctl: c, fromSlide: from, toSlide: to, cleanups: [],
    }, t.type).then(() => { if (!cancelled) window.setTimeout(onDone, 350); });
    return () => { cancelled = true; c.skip(); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="preview-overlay" ref={setHost} onPointerDown={() => { ctl.current.skip(); onDone(); }} title="Click to close">
      <ScaledBox W={pres.width} H={pres.height} scale={scale * 0.92} className="preview-box">
        <div className="tr-layer" ref={fromRef} style={{ zIndex: 1 }}>
          <SlideContent slide={from} W={pres.width} H={pres.height} mode="thumb" slideNumber={index} />
        </div>
        <div className="tr-layer" ref={toRef} style={{ zIndex: 2 }}>
          <SlideContent slide={to} W={pres.width} H={pres.height} mode="thumb" slideNumber={index + 1} hiddenIds={initiallyHidden(to)} />
        </div>
        <div className="tr-fx" ref={fxRef} />
      </ScaledBox>
    </div>
  );
}

/** Plays every animation of the current slide directly on the canvas, then restores it. */
export function useAnimationPreview(): void {
  const counter = useStore((s) => s.ui.previewAnimations);
  useEffect(() => {
    if (!counter) return;
    const root = document.querySelector('.stage .slide-content') as HTMLElement | null;
    const slide = currentSlide();
    const { pres } = getState();
    if (!root || !slide.animations.length) return;
    let stopped = false;
    const hidden = initiallyHidden(slide);
    resetSlideAnimations(root, hidden);
    const running: Animation[] = [];
    (async () => {
      for (const step of buildSteps(slide.animations)) {
        if (stopped) return;
        const r = runStep(root, slide, pres.width, pres.height, step);
        running.push(...r.animations);
        await r.promise;
        await new Promise((res) => setTimeout(res, 350));
      }
      await new Promise((res) => setTimeout(res, 600));
      if (!stopped) resetSlideAnimations(root, new Set());
    })();
    return () => {
      stopped = true;
      running.forEach((a) => a.cancel());
      resetSlideAnimations(root, new Set());
    };
  }, [counter]);
}
