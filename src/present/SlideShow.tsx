import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import type { Presentation, SlideElement } from '../model/types';
import { getTheme } from '../model/themes';
import { SlideContent, SlideThumb } from '../render/SlideView';
import { buildSteps, initiallyHidden, resetSlideAnimations, runStep, type StepRunner } from './animations';
import { createController, runTransition, type Controller } from './transitions';
import { ScaledBox, useFitScale } from './Stage';
import { showChannel, type ShowCommand, type ShowState } from './channel';

interface Props {
  pres: Presentation;
  startIndex: number;
  onExit: (lastIndex: number) => void;
}

interface View {
  cur: number;
  prev: number | null;
  token: number;
  /** How the slide was entered: forward plays the transition, back shows it fully built. */
  mode: 'forward' | 'back' | 'jump';
}

export function SlideShow({ pres, startIndex, onExit }: Props) {
  const W = pres.width, H = pres.height;
  const [host, setHost] = useState<HTMLDivElement | null>(null);
  const scale = useFitScale(host, W, H);
  const [view, setView] = useState<View>({ cur: startIndex, prev: null, token: 1, mode: 'forward' });
  const [ended, setEnded] = useState(false);
  const [blank, setBlank] = useState<null | 'black' | 'white'>(null);
  const [laser, setLaser] = useState(false);
  const [pen, setPen] = useState(false);
  const [penColor, setPenColor] = useState('#ef4444');
  const [overview, setOverview] = useState(false);
  const [pointer, setPointer] = useState<{ x: number; y: number } | null>(null);
  const [idle, setIdle] = useState(false);
  const layerRefs = useRef(new Map<string, HTMLDivElement>());
  const fxRef = useRef<HTMLDivElement>(null);
  const ctlRef = useRef<Controller | null>(null);
  const stepRef = useRef(0);
  const runningRef = useRef<StepRunner | null>(null);
  const timerRef = useRef<number | null>(null);
  const digits = useRef('');
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const accent = getTheme(pres.themeId).colors.accents[0];

  const visible = useMemo(() => pres.slides.map((s, i) => (s.hidden && i !== startIndex ? -1 : i)).filter((i) => i >= 0), [pres.slides, startIndex]);
  const slide = pres.slides[view.cur];
  const steps = useMemo(() => buildSteps(slide?.animations ?? []), [slide]);
  const viewRef = useRef(view);
  viewRef.current = view;

  const keyFor = (index: number, token: number) => `${index}:${token}`;
  const curKey = keyFor(view.cur, view.token);
  const prevKey = view.prev !== null ? keyFor(view.prev, view.token - 1) : null;

  const contentOf = (key: string | null) => {
    if (!key) return null;
    return layerRefs.current.get(key)?.querySelector('.slide-content') as HTMLElement | null;
  };

  const clearTimer = () => {
    if (timerRef.current) window.clearTimeout(timerRef.current);
    timerRef.current = null;
  };

  const broadcast = useCallback((state: Partial<ShowState> = {}) => {
    const v = viewRef.current;
    showChannel()?.postMessage({
      type: 'state', docId: pres.id, index: v.cur, step: stepRef.current,
      steps: buildSteps(pres.slides[v.cur]?.animations ?? []).length, ended: false, ...state,
    } satisfies ShowState);
  }, [pres]);

  const scheduleAdvance = useCallback(() => {
    clearTimer();
    const after = pres.slides[viewRef.current.cur]?.transition.advanceAfter;
    if (after !== null && after !== undefined && after >= 0) {
      timerRef.current = window.setTimeout(() => nextRef.current(), after * 1000);
    }
  }, [pres]);

  const playStep = useCallback(async (index: number) => {
    const root = contentOf(keyFor(viewRef.current.cur, viewRef.current.token));
    const s = pres.slides[viewRef.current.cur];
    const st = buildSteps(s.animations)[index];
    if (!root || !st) return;
    stepRef.current = index + 1;
    broadcast();
    const runner = runStep(root, s, W, H, st);
    runningRef.current = runner;
    await runner.promise;
    if (runningRef.current === runner) runningRef.current = null;
  }, [pres, W, H, broadcast]);

  // Enter a slide: run its transition, then any animations that start on their own.
  useLayoutEffect(() => {
    const token = view.token;
    const to = layerRefs.current.get(curKey);
    const toContent = contentOf(curKey);
    if (!to || !toContent || !slide) return;
    const hidden = initiallyHidden(slide);
    resetSlideAnimations(toContent, hidden);
    stepRef.current = 0;
    clearTimer();
    canvasRef.current?.getContext('2d')?.clearRect(0, 0, canvasRef.current.width, canvasRef.current.height);

    if (view.mode === 'back') {
      steps.forEach((st) => runStep(toContent, slide, W, H, st, true));
      stepRef.current = steps.length;
      broadcast();
      return;
    }
    const from = prevKey ? layerRefs.current.get(prevKey) ?? null : null;
    const ctl = createController();
    ctlRef.current = ctl;
    const fx = fxRef.current!;
    const tr = slide.transition;
    const isFirst = view.prev === null && view.token === 1;
    const type = isFirst && tr.type === 'morph' ? 'fade' : tr.type;
    broadcast();
    void runTransition({
      from, to, fx, W, H, duration: tr.duration, dir: tr.direction ?? '', easing: tr.easing, accent, ctl,
      fromSlide: view.prev !== null ? pres.slides[view.prev] : null, toSlide: slide, cleanups: [],
    }, type).then(async () => {
      if (viewRef.current.token !== token) return;
      if (ctlRef.current === ctl) ctlRef.current = null;
      setView((v) => (v.token === token ? { ...v, prev: null } : v));
      if (steps[0]?.auto) await playStep(0);
      if (viewRef.current.token === token) scheduleAdvance();
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [view.token]);

  const go = useCallback((index: number, mode: View['mode']) => {
    clearTimer();
    ctlRef.current?.skip();
    runningRef.current?.animations.forEach((a) => a.finish());
    setEnded(false);
    setView((v) => ({ cur: index, prev: mode === 'back' ? null : v.cur, token: v.token + 1, mode }));
  }, []);

  const next = useCallback(() => {
    if (ended) { onExit(viewRef.current.cur); return; }
    if (ctlRef.current) { ctlRef.current.skip(); return; }
    if (runningRef.current) {
      runningRef.current.animations.forEach((a) => a.finish());
      runningRef.current = null;
      return;
    }
    const s = pres.slides[viewRef.current.cur];
    const st = buildSteps(s.animations);
    if (stepRef.current < st.length) {
      void playStep(stepRef.current).then(scheduleAdvance);
      return;
    }
    const pos = visible.indexOf(viewRef.current.cur);
    if (pos >= 0 && pos < visible.length - 1) go(visible[pos + 1], 'forward');
    else if (pres.settings.loop && visible.length) go(visible[0], 'forward');
    else {
      clearTimer();
      setEnded(true);
      showChannel()?.postMessage({ type: 'state', docId: pres.id, index: viewRef.current.cur, step: stepRef.current, steps: st.length, ended: true } satisfies ShowState);
    }
  }, [ended, onExit, pres, visible, go, playStep, scheduleAdvance]);

  const prev = useCallback(() => {
    if (ended) { setEnded(false); broadcast(); return; }
    ctlRef.current?.skip();
    runningRef.current?.animations.forEach((a) => a.finish());
    runningRef.current = null;
    const s = pres.slides[viewRef.current.cur];
    const st = buildSteps(s.animations);
    const minStep = st[0]?.auto ? 1 : 0;
    if (stepRef.current > minStep) {
      const root = contentOf(keyFor(viewRef.current.cur, viewRef.current.token));
      if (root) {
        resetSlideAnimations(root, initiallyHidden(s));
        const target = stepRef.current - 1;
        for (let i = 0; i < target; i++) runStep(root, s, W, H, st[i], true);
        stepRef.current = target;
        broadcast();
      }
      return;
    }
    const pos = visible.indexOf(viewRef.current.cur);
    if (pos > 0) go(visible[pos - 1], 'back');
    else if (pres.settings.loop && visible.length > 1) go(visible[visible.length - 1], 'back');
  }, [ended, pres, visible, go, W, H, broadcast]);

  const nextRef = useRef(next);
  nextRef.current = next;

  const gotoSlide = useCallback((index: number) => {
    if (index >= 0 && index < pres.slides.length) go(index, 'jump');
  }, [go, pres.slides.length]);

  const onLink = useCallback((el: SlideElement) => {
    if (!el.link) return;
    if (el.link.kind === 'url') window.open(el.link.url, '_blank', 'noopener');
    else {
      const id = el.link.slideId;
      const idx = pres.slides.findIndex((s) => s.id === id);
      if (idx >= 0) gotoSlide(idx);
    }
  }, [pres.slides, gotoSlide]);

  // Keyboard
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (overview && e.key === 'Escape') { setOverview(false); return; }
      const k = e.key;
      if (/^[0-9]$/.test(k)) { digits.current += k; return; }
      if (k === 'Enter' && digits.current) {
        gotoSlide(Number(digits.current) - 1);
        digits.current = '';
        return;
      }
      digits.current = '';
      if (blank && !['b', 'B', 'w', 'W', '.', ','].includes(k)) { setBlank(null); return; }
      switch (k) {
        case 'ArrowRight': case 'ArrowDown': case 'PageDown': case ' ': case 'Enter': case 'n': case 'N':
          e.preventDefault(); next(); break;
        case 'ArrowLeft': case 'ArrowUp': case 'PageUp': case 'Backspace': case 'p': case 'P':
          e.preventDefault(); prev(); break;
        case 'Home': gotoSlide(visible[0] ?? 0); break;
        case 'End': gotoSlide(visible[visible.length - 1] ?? 0); break;
        case 'b': case 'B': case '.': setBlank((b) => (b === 'black' ? null : 'black')); break;
        case 'w': case 'W': case ',': setBlank((b) => (b === 'white' ? null : 'white')); break;
        case 'l': case 'L': setLaser((v) => !v); setPen(false); break;
        case 'd': case 'D': setPen((v) => !v); setLaser(false); break;
        case 'e': case 'E': {
          const c = canvasRef.current;
          c?.getContext('2d')?.clearRect(0, 0, c.width, c.height);
          break;
        }
        case 'g': case 'G': case '-': setOverview((v) => !v); break;
        case 'Escape': onExit(viewRef.current.cur); break;
        default:
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [next, prev, gotoSlide, onExit, blank, overview, visible]);

  // Presenter view remote control
  useEffect(() => {
    const ch = showChannel();
    if (!ch) return;
    const onMsg = (e: MessageEvent<ShowCommand | ShowState | { type: 'hello'; docId: string }>) => {
      const m = e.data;
      if (!m || ('docId' in m && m.docId !== pres.id)) return;
      if (m.type === 'hello') broadcast();
      if (m.type !== 'cmd') return;
      if (m.cmd === 'next') next();
      else if (m.cmd === 'prev') prev();
      else if (m.cmd === 'goto' && typeof m.index === 'number') gotoSlide(m.index);
      else if (m.cmd === 'black') setBlank((b) => (b === 'black' ? null : 'black'));
      else if (m.cmd === 'white') setBlank((b) => (b === 'white' ? null : 'white'));
      else if (m.cmd === 'end') onExit(viewRef.current.cur);
    };
    ch.addEventListener('message', onMsg);
    return () => ch.removeEventListener('message', onMsg);
  }, [pres.id, next, prev, gotoSlide, onExit, broadcast]);

  // Fullscreen for the life of the show; leaving fullscreen ends the show.
  useEffect(() => {
    const el = document.documentElement;
    let entered = false;
    if (!document.fullscreenElement && el.requestFullscreen) {
      el.requestFullscreen().then(() => { entered = true; }, () => undefined);
    }
    const onChange = () => {
      if (entered && !document.fullscreenElement) onExit(viewRef.current.cur);
    };
    document.addEventListener('fullscreenchange', onChange);
    return () => {
      document.removeEventListener('fullscreenchange', onChange);
      clearTimer();
      if (document.fullscreenElement) document.exitFullscreen().catch(() => undefined);
      showChannel()?.postMessage({ type: 'state', docId: pres.id, index: viewRef.current.cur, step: 0, steps: 0, ended: true, closed: true } satisfies ShowState);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Hide the cursor and controls after a short idle time.
  useEffect(() => {
    let t = window.setTimeout(() => setIdle(true), 2500);
    const onMove = () => {
      setIdle(false);
      window.clearTimeout(t);
      t = window.setTimeout(() => setIdle(true), 2500);
    };
    window.addEventListener('pointermove', onMove);
    return () => { window.removeEventListener('pointermove', onMove); window.clearTimeout(t); };
  }, []);

  // Pen drawing canvas follows the window size.
  useEffect(() => {
    const c = canvasRef.current;
    if (!c) return;
    const resize = () => { c.width = window.innerWidth; c.height = window.innerHeight; };
    resize();
    window.addEventListener('resize', resize);
    return () => window.removeEventListener('resize', resize);
  }, []);

  const drawing = useRef<{ x: number; y: number } | null>(null);
  const onPointerDown = (e: React.PointerEvent) => {
    if (pen) {
      drawing.current = { x: e.clientX, y: e.clientY };
      (e.target as HTMLElement).setPointerCapture(e.pointerId);
      return;
    }
    if (e.button === 0 && slide?.transition.advanceOnClick !== false) next();
  };
  const onPointerMove = (e: React.PointerEvent) => {
    if (laser) setPointer({ x: e.clientX, y: e.clientY });
    if (pen && drawing.current) {
      const ctx = canvasRef.current?.getContext('2d');
      if (ctx) {
        ctx.strokeStyle = penColor;
        ctx.lineWidth = 4;
        ctx.lineCap = 'round';
        ctx.beginPath();
        ctx.moveTo(drawing.current.x, drawing.current.y);
        ctx.lineTo(e.clientX, e.clientY);
        ctx.stroke();
      }
      drawing.current = { x: e.clientX, y: e.clientY };
    }
  };

  const layers: { key: string; index: number }[] = [];
  if (prevKey !== null && view.prev !== null) layers.push({ key: prevKey, index: view.prev });
  layers.push({ key: curKey, index: view.cur });
  const hiddenFor = (index: number) => initiallyHidden(pres.slides[index]);

  return (
    <div
      className={`show ${idle && !pen ? 'idle' : ''} ${laser ? 'laser-on' : ''} ${pen ? 'pen-on' : ''}`}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={() => { drawing.current = null; }}
      onContextMenu={(e) => { e.preventDefault(); prev(); }}
    >
      <div className="show-host" ref={setHost}>
        <ScaledBox W={W} H={H} scale={scale} className="show-box">
          {layers.map(({ key, index }, i) => (
            <div
              key={key}
              className="tr-layer"
              style={{ zIndex: i === layers.length - 1 ? 2 : 1 }}
              ref={(el) => { if (el) layerRefs.current.set(key, el); else layerRefs.current.delete(key); }}
            >
              <SlideContent
                slide={pres.slides[index]}
                W={W}
                H={H}
                mode="present"
                slideNumber={index + 1}
                showSlideNumber={pres.settings.showSlideNumbers}
                hiddenIds={hiddenFor(index)}
                onLink={onLink}
              />
            </div>
          ))}
          <div className="tr-fx" ref={fxRef} />
        </ScaledBox>
      </div>
      <canvas ref={canvasRef} className="pen-canvas" />
      {blank && <div className="show-blank" style={{ background: blank === 'black' ? '#000' : '#fff' }} />}
      {ended && <div className="show-end">End of slide show. Click or press any key to exit.</div>}
      {laser && pointer && <div className="laser-dot" style={{ left: pointer.x, top: pointer.y }} />}
      <div className="show-controls" onPointerDown={(e) => e.stopPropagation()}>
        <button title="Previous (Left arrow)" onClick={prev}>&#9664;</button>
        <span className="show-count">{view.cur + 1} / {pres.slides.length}</span>
        <button title="Next (Right arrow)" onClick={next}>&#9654;</button>
        <button title="Pen (D)" className={pen ? 'on' : ''} onClick={() => { setPen((v) => !v); setLaser(false); }}>Pen</button>
        {pen && (
          <span className="pen-colors">
            {['#ef4444', '#facc15', '#22c55e', '#3b82f6', '#ffffff', '#000000'].map((c) => (
              <button key={c} className={`swatch ${penColor === c ? 'on' : ''}`} style={{ background: c }} onClick={() => setPenColor(c)} aria-label={`Pen colour ${c}`} />
            ))}
            <button title="Erase all (E)" onClick={() => { const c = canvasRef.current; c?.getContext('2d')?.clearRect(0, 0, c.width, c.height); }}>Erase</button>
          </span>
        )}
        <button title="Laser pointer (L)" className={laser ? 'on' : ''} onClick={() => { setLaser((v) => !v); setPen(false); }}>Laser</button>
        <button title="All slides (G)" onClick={() => setOverview(true)}>Slides</button>
        <button title="Black screen (B)" onClick={() => setBlank((b) => (b === 'black' ? null : 'black'))}>Black</button>
        <button title="End show (Esc)" onClick={() => onExit(viewRef.current.cur)}>End</button>
      </div>
      {overview && (
        <div className="show-overview" onPointerDown={(e) => e.stopPropagation()}>
          <div className="show-overview-head">
            <strong>Go to slide</strong>
            <button onClick={() => setOverview(false)}>Close</button>
          </div>
          <div className="show-overview-grid">
            {pres.slides.map((s, i) => (
              <button key={s.id} className={`ov-item ${i === view.cur ? 'on' : ''} ${s.hidden ? 'is-hidden' : ''}`} onClick={() => { setOverview(false); gotoSlide(i); }}>
                <SlideThumb slide={s} W={W} H={H} width={220} slideNumber={i + 1} />
                <span>{i + 1}</span>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
