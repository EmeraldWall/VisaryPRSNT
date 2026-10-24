import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import type { SlideElement } from '../model/types';
import { fileToDataUrl } from '../model/util';
import { SlideContent } from '../render/SlideView';
import { rememberEditPoint } from '../render/EditableHtml';
import {
  beginGesture, currentIndex, currentSlide, endGesture, getState, setUI, update, updateElements, useStore,
} from '../store/store';
import { applyFormat, insertImage } from './actions';
import { ContextMenu } from './ContextMenu';
import { buildSteps } from '../present/animations';

interface Guide {
  axis: 'x' | 'y';
  pos: number;
}

type Gesture =
  | { kind: 'move'; startX: number; startY: number; orig: Map<string, { x: number; y: number }>; moved: boolean; box: Box }
  | { kind: 'resize'; id: string; hx: number; hy: number; startX: number; startY: number; orig: SlideElement; keepRatio: boolean }
  | { kind: 'resize-multi'; hx: number; hy: number; startX: number; startY: number; box: Box; orig: Map<string, Box> }
  | { kind: 'rotate'; id: string; cx: number; cy: number }
  | { kind: 'marquee'; x0: number; y0: number; additive: boolean };

interface Box {
  x: number;
  y: number;
  w: number;
  h: number;
}

const HANDLES: [number, number][] = [[-1, -1], [0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0]];
const GRID = 40;

function bbox(els: Box[]): Box {
  const x = Math.min(...els.map((e) => e.x));
  const y = Math.min(...els.map((e) => e.y));
  return { x, y, w: Math.max(...els.map((e) => e.x + e.w)) - x, h: Math.max(...els.map((e) => e.y + e.h)) - y };
}

export function Canvas() {
  const pres = useStore((s) => s.pres);
  const slide = useStore((s) => currentSlide(s));
  const selection = useStore((s) => s.ui.selection);
  const editingId = useStore((s) => s.ui.editingId);
  const zoomSetting = useStore((s) => s.ui.zoom);
  const showGrid = useStore((s) => s.ui.showGrid);
  const snap = useStore((s) => s.ui.snap);
  const panel = useStore((s) => s.ui.panel);
  const formatPainter = useStore((s) => s.ui.formatPainter);
  const slideNumber = useStore((s) => currentIndex(s) + 1);
  const W = pres.width, H = pres.height;

  const wrapRef = useRef<HTMLDivElement>(null);
  const stageRef = useRef<HTMLDivElement>(null);
  const [fit, setFit] = useState(0.5);
  const [guides, setGuides] = useState<Guide[]>([]);
  const [marquee, setMarquee] = useState<Box | null>(null);
  const [menu, setMenu] = useState<{ x: number; y: number } | null>(null);
  const gesture = useRef<Gesture | null>(null);

  useLayoutEffect(() => {
    const el = wrapRef.current;
    if (!el) return;
    const measure = () => {
      const pad = 64;
      setFit(Math.max(0.05, Math.min((el.clientWidth - pad) / W, (el.clientHeight - pad) / H)));
    };
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, [W, H]);

  const zoom = zoomSetting === 'fit' ? fit : zoomSetting;

  const toLogical = useCallback((clientX: number, clientY: number) => {
    const r = stageRef.current!.getBoundingClientRect();
    return { x: (clientX - r.left) / zoom, y: (clientY - r.top) / zoom };
  }, [zoom]);

  const onHtmlChange = useCallback((id: string, html: string) => {
    updateElements([id], (el) => { if ('html' in el) el.html = html; }, { coalesce: `type-${id}` });
  }, []);

  const onCellChange = useCallback((id: string, r: number, c: number, html: string) => {
    updateElements([id], (el) => { if (el.type === 'table' && el.rows[r]) el.rows[r][c] = html; }, { coalesce: `cell-${id}-${r}-${c}` });
  }, []);

  /* ---------- snapping ---------- */
  const snapMove = useCallback((box: Box, moving: Set<string>): { dx: number; dy: number; guides: Guide[] } => {
    const out: Guide[] = [];
    if (!snap) return { dx: 0, dy: 0, guides: out };
    if (showGrid) {
      return { dx: Math.round(box.x / GRID) * GRID - box.x, dy: Math.round(box.y / GRID) * GRID - box.y, guides: out };
    }
    const thr = 6 / zoom;
    const xs = [0, W / 2, W];
    const ys = [0, H / 2, H];
    for (const e of currentSlide().elements) {
      if (moving.has(e.id)) continue;
      xs.push(e.x, e.x + e.w / 2, e.x + e.w);
      ys.push(e.y, e.y + e.h / 2, e.y + e.h);
    }
    const pick = (vals: number[], targets: number[]) => {
      let best: { d: number; t: number } | null = null;
      for (const v of vals) for (const t of targets) {
        const d = t - v;
        if (Math.abs(d) < thr && (!best || Math.abs(d) < Math.abs(best.d))) best = { d, t };
      }
      return best;
    };
    const bx = pick([box.x, box.x + box.w / 2, box.x + box.w], xs);
    const by = pick([box.y, box.y + box.h / 2, box.y + box.h], ys);
    if (bx) out.push({ axis: 'x', pos: bx.t });
    if (by) out.push({ axis: 'y', pos: by.t });
    return { dx: bx?.d ?? 0, dy: by?.d ?? 0, guides: out };
  }, [snap, showGrid, zoom, W, H]);

  /* ---------- pointer handling ---------- */
  useEffect(() => {
    const onMove = (e: PointerEvent) => {
      const g = gesture.current;
      if (!g) return;
      const p = toLogical(e.clientX, e.clientY);
      if (g.kind === 'move') {
        const dx0 = (e.clientX - g.startX) / zoom;
        const dy0 = (e.clientY - g.startY) / zoom;
        if (!g.moved && Math.hypot(dx0, dy0) * zoom < 3) return;
        if (!g.moved) { g.moved = true; beginGesture(); }
        let dx = dx0, dy = dy0;
        if (e.shiftKey) { if (Math.abs(dx) > Math.abs(dy)) dy = 0; else dx = 0; }
        const moving = new Set(g.orig.keys());
        const s = snapMove({ ...g.box, x: g.box.x + dx, y: g.box.y + dy }, moving);
        dx += s.dx; dy += s.dy;
        setGuides(s.guides);
        update((d) => {
          const sl = d.slides.find((x) => x.id === getState().ui.currentSlideId);
          if (!sl) return;
          for (const el of sl.elements) {
            const o = g.orig.get(el.id);
            if (o) { el.x = Math.round(o.x + dx); el.y = Math.round(o.y + dy); }
          }
        }, { history: false });
      } else if (g.kind === 'resize') {
        const o = g.orig;
        const rad = (o.rot * Math.PI) / 180;
        const cos = Math.cos(rad), sin = Math.sin(rad);
        const gdx = (e.clientX - g.startX) / zoom, gdy = (e.clientY - g.startY) / zoom;
        const lx = gdx * cos + gdy * sin;
        const ly = -gdx * sin + gdy * cos;
        let w = Math.max(8, o.w + g.hx * lx);
        let h = Math.max(8, o.h + g.hy * ly);
        if ((g.keepRatio || e.shiftKey) && g.hx && g.hy) {
          const k = Math.max(w / o.w, h / o.h);
          w = o.w * k; h = o.h * k;
        }
        if (!g.hx) w = o.w;
        if (!g.hy) h = o.h;
        const ox = (g.hx * (w - o.w)) / 2, oy = (g.hy * (h - o.h)) / 2;
        const cx = o.x + o.w / 2 + ox * cos - oy * sin;
        const cy = o.y + o.h / 2 + ox * sin + oy * cos;
        updateElements([g.id], (el) => {
          el.w = Math.round(w); el.h = Math.round(h);
          el.x = Math.round(cx - w / 2); el.y = Math.round(cy - h / 2);
        }, { history: false });
      } else if (g.kind === 'resize-multi') {
        const b = g.box;
        const dx = (e.clientX - g.startX) / zoom, dy = (e.clientY - g.startY) / zoom;
        const nx = g.hx < 0 ? b.x + dx : b.x;
        const ny = g.hy < 0 ? b.y + dy : b.y;
        const nw = Math.max(10, b.w + g.hx * dx);
        const nh = Math.max(10, b.h + g.hy * dy);
        const kx = g.hx ? nw / b.w : 1, ky = g.hy ? nh / b.h : 1;
        const baseX = g.hx ? nx : b.x, baseY = g.hy ? ny : b.y;
        updateElements([...g.orig.keys()], (el) => {
          const o = g.orig.get(el.id)!;
          el.x = Math.round(baseX + (o.x - b.x) * kx);
          el.y = Math.round(baseY + (o.y - b.y) * ky);
          el.w = Math.max(4, Math.round(o.w * kx));
          el.h = Math.max(4, Math.round(o.h * ky));
        }, { history: false });
      } else if (g.kind === 'rotate') {
        let deg = (Math.atan2(p.y - g.cy, p.x - g.cx) * 180) / Math.PI + 90;
        if (e.shiftKey) deg = Math.round(deg / 15) * 15;
        else for (const a of [0, 90, 180, 270, 360, -90]) if (Math.abs(deg - a) < 3) deg = a;
        deg = ((deg % 360) + 360) % 360;
        updateElements([g.id], (el) => { el.rot = Math.round(deg); }, { history: false });
      } else if (g.kind === 'marquee') {
        setMarquee({ x: Math.min(g.x0, p.x), y: Math.min(g.y0, p.y), w: Math.abs(p.x - g.x0), h: Math.abs(p.y - g.y0) });
      }
    };
    const onUp = (e: PointerEvent) => {
      const g = gesture.current;
      if (!g) return;
      gesture.current = null;
      setGuides([]);
      if (g.kind === 'marquee') {
        const p = toLogical(e.clientX, e.clientY);
        const box = { x: Math.min(g.x0, p.x), y: Math.min(g.y0, p.y), w: Math.abs(p.x - g.x0), h: Math.abs(p.y - g.y0) };
        setMarquee(null);
        if (box.w < 3 && box.h < 3) return;
        const hit = currentSlide().elements
          .filter((el) => el.x < box.x + box.w && el.x + el.w > box.x && el.y < box.y + box.h && el.y + el.h > box.y)
          .map((el) => el.id);
        setUI((ui) => ({ selection: g.additive ? [...new Set([...ui.selection, ...hit])] : hit }));
        return;
      }
      endGesture();
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
    return () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
    };
  }, [toLogical, zoom, snapMove]);

  const selectFor = (el: SlideElement, e: React.PointerEvent): string[] => {
    const all = currentSlide().elements;
    const group = el.groupId && !e.altKey ? all.filter((x) => x.groupId === el.groupId).map((x) => x.id) : [el.id];
    const sel = getState().ui.selection;
    if (e.shiftKey || e.ctrlKey || e.metaKey) {
      const has = group.every((id) => sel.includes(id));
      return has ? sel.filter((id) => !group.includes(id)) : [...new Set([...sel, ...group])];
    }
    if (group.every((id) => sel.includes(id))) return sel;
    return group;
  };

  const onStageDown = (e: React.PointerEvent) => {
    if (e.button === 2) return;
    setMenu(null);
    const target = (e.target as HTMLElement).closest('[data-el-id]') as HTMLElement | null;
    const id = target?.dataset.elId;
    const el = id ? currentSlide().elements.find((x) => x.id === id) : undefined;
    const ui = getState().ui;
    if (ui.editingId && id === ui.editingId) return;
    if (ui.editingId) setUI({ editingId: null });
    if (!el) {
      const p = toLogical(e.clientX, e.clientY);
      if (!e.shiftKey) setUI({ selection: [] });
      gesture.current = { kind: 'marquee', x0: p.x, y0: p.y, additive: e.shiftKey };
      return;
    }
    if (ui.formatPainter) {
      applyFormat(el.id);
      return;
    }
    const sel = selectFor(el, e);
    setUI({ selection: sel });
    const movable = currentSlide().elements.filter((x) => sel.includes(x.id) && !x.locked);
    if (!movable.length) return;
    gesture.current = {
      kind: 'move', startX: e.clientX, startY: e.clientY, moved: false,
      orig: new Map(movable.map((x) => [x.id, { x: x.x, y: x.y }])),
      box: bbox(movable),
    };
  };

  const onDoubleClick = (e: React.MouseEvent) => {
    const target = (e.target as HTMLElement).closest('[data-el-id]') as HTMLElement | null;
    const id = target?.dataset.elId;
    const el = id ? currentSlide().elements.find((x) => x.id === id) : undefined;
    if (!el || el.locked) return;
    if (el.type === 'text' || el.type === 'shape' || el.type === 'table') {
      if (el.type === 'shape' && (el.shape === 'line' || el.shape === 'arrowLine')) return;
      rememberEditPoint(e.clientX, e.clientY);
      setUI({ selection: [el.id], editingId: el.id });
    } else {
      setUI({ selection: [el.id], panel: 'format' });
    }
  };

  const startResize = (e: React.PointerEvent, hx: number, hy: number) => {
    e.stopPropagation();
    const els = currentSlide().elements.filter((x) => getState().ui.selection.includes(x.id));
    beginGesture();
    if (els.length === 1) {
      const el = els[0];
      gesture.current = { kind: 'resize', id: el.id, hx, hy, startX: e.clientX, startY: e.clientY, orig: el, keepRatio: el.type === 'image' };
    } else {
      gesture.current = { kind: 'resize-multi', hx, hy, startX: e.clientX, startY: e.clientY, box: bbox(els), orig: new Map(els.map((x) => [x.id, { x: x.x, y: x.y, w: x.w, h: x.h }])) };
    }
  };

  const startRotate = (e: React.PointerEvent, el: SlideElement) => {
    e.stopPropagation();
    beginGesture();
    gesture.current = { kind: 'rotate', id: el.id, cx: el.x + el.w / 2, cy: el.y + el.h / 2 };
  };

  /* ---------- drop and context menu ---------- */
  const onDrop = async (e: React.DragEvent) => {
    const files = [...e.dataTransfer.files].filter((f) => f.type.startsWith('image/'));
    if (!files.length) return;
    e.preventDefault();
    const p = toLogical(e.clientX, e.clientY);
    for (const f of files) await insertImage(await fileToDataUrl(f), p);
  };

  const onContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    const target = (e.target as HTMLElement).closest('[data-el-id]') as HTMLElement | null;
    const id = target?.dataset.elId;
    if (id && !getState().ui.selection.includes(id)) setUI({ selection: [id] });
    if (!id) setUI({ selection: [] });
    setMenu({ x: e.clientX, y: e.clientY });
  };

  const onWheel = (e: React.WheelEvent) => {
    if (!(e.ctrlKey || e.metaKey)) return;
    e.preventDefault();
    const z = Math.max(0.1, Math.min(4, zoom * (e.deltaY < 0 ? 1.1 : 0.9)));
    setUI({ zoom: z });
  };

  const selected = slide.elements.filter((e) => selection.includes(e.id));
  const animBadges = useMemo(() => {
    if (panel !== 'animations') return new Map<string, number[]>();
    const map = new Map<string, number[]>();
    const steps = buildSteps(slide.animations);
    // Like PowerPoint, animations that start on their own are numbered 0, click steps from 1.
    const offset = steps[0]?.auto ? 0 : 1;
    steps.forEach((st, i) => {
      for (const it of st.items) {
        const list = map.get(it.anim.elementId) ?? [];
        if (!list.includes(i + offset)) list.push(i + offset);
        map.set(it.anim.elementId, list);
      }
    });
    return map;
  }, [panel, slide.animations]);

  const multiBox = selected.length > 1 ? bbox(selected) : null;
  const single = selected.length === 1 ? selected[0] : null;

  return (
    <div
      className={`canvas-wrap ${formatPainter ? 'painter' : ''}`}
      ref={wrapRef}
      onWheel={onWheel}
      onDragOver={(e) => e.preventDefault()}
      onDrop={onDrop}
      onPointerDown={(e) => {
        if (e.target === e.currentTarget) {
          setUI({ selection: [], editingId: null });
          setMenu(null);
        }
      }}
    >
      <div className="canvas-center" onPointerDown={(e) => { if (e.target === e.currentTarget) setUI({ selection: [], editingId: null }); }}>
        <div className="stage" ref={stageRef} style={{ width: W * zoom, height: H * zoom }} onPointerDown={onStageDown} onDoubleClick={onDoubleClick} onContextMenu={onContextMenu}>
          <div className="stage-scale" style={{ width: W, height: H, transform: `scale(${zoom})` }}>
            <SlideContent
              slide={slide}
              W={W}
              H={H}
              mode="edit"
              slideNumber={slideNumber}
              showSlideNumber={pres.settings.showSlideNumbers}
              editingId={editingId}
              onHtmlChange={onHtmlChange}
              onCellChange={onCellChange}
            />
          </div>
          {showGrid && <div className="grid-overlay" style={{ backgroundSize: `${GRID * zoom}px ${GRID * zoom}px` }} />}
          <div className="overlay">
            {selected.map((el) => (
              <div
                key={el.id}
                className={`sel-box ${el.locked ? 'locked' : ''} ${editingId === el.id ? 'editing' : ''}`}
                style={{ left: el.x * zoom, top: el.y * zoom, width: el.w * zoom, height: el.h * zoom, transform: el.rot ? `rotate(${el.rot}deg)` : undefined }}
              >
                {single && !el.locked && editingId !== el.id && (
                  <>
                    {HANDLES.map(([hx, hy]) => (
                      <span
                        key={`${hx}${hy}`}
                        className={`handle h${hx}${hy}`}
                        style={{ left: `${(hx + 1) * 50}%`, top: `${(hy + 1) * 50}%`, cursor: cursorFor(hx, hy, el.rot) }}
                        onPointerDown={(e) => startResize(e, hx, hy)}
                      />
                    ))}
                    <span className="rot-line" />
                    <span className="rot-handle" title="Rotate (Shift snaps to 15 degrees)" onPointerDown={(e) => startRotate(e, el)} />
                  </>
                )}
                {el.locked && <span className="lock-badge" title="Locked">L</span>}
              </div>
            ))}
            {multiBox && (
              <div className="sel-box multi" style={{ left: multiBox.x * zoom, top: multiBox.y * zoom, width: multiBox.w * zoom, height: multiBox.h * zoom }}>
                {HANDLES.map(([hx, hy]) => (
                  <span key={`${hx}${hy}`} className="handle" style={{ left: `${(hx + 1) * 50}%`, top: `${(hy + 1) * 50}%`, cursor: cursorFor(hx, hy, 0) }} onPointerDown={(e) => startResize(e, hx, hy)} />
                ))}
              </div>
            )}
            {[...animBadges.entries()].map(([id, nums]) => {
              const el = slide.elements.find((x) => x.id === id);
              if (!el) return null;
              return <span key={id} className="anim-badge" style={{ left: el.x * zoom - 10, top: el.y * zoom - 10 }}>{nums.join(',')}</span>;
            })}
            {guides.map((g, i) => (
              <div key={i} className={`guide guide-${g.axis}`} style={g.axis === 'x' ? { left: g.pos * zoom } : { top: g.pos * zoom }} />
            ))}
            {marquee && <div className="marquee" style={{ left: marquee.x * zoom, top: marquee.y * zoom, width: marquee.w * zoom, height: marquee.h * zoom }} />}
          </div>
        </div>
      </div>
      {menu && <ContextMenu x={menu.x} y={menu.y} onClose={() => setMenu(null)} />}
    </div>
  );
}

function cursorFor(hx: number, hy: number, rot: number): string {
  const base = (Math.atan2(hy, hx) * 180) / Math.PI + rot;
  const a = ((Math.round(base / 45) * 45) % 180 + 180) % 180;
  return a === 0 ? 'ew-resize' : a === 45 ? 'nwse-resize' : a === 90 ? 'ns-resize' : 'nesw-resize';
}
