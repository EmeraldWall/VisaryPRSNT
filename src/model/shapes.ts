import type { ShapeKind } from './types';

export interface ShapeInfo {
  kind: ShapeKind;
  name: string;
  group: 'Basic' | 'Arrows' | 'Stars and callouts' | 'Lines';
}

export const SHAPES: ShapeInfo[] = [
  { kind: 'rect', name: 'Rectangle', group: 'Basic' },
  { kind: 'roundRect', name: 'Rounded rectangle', group: 'Basic' },
  { kind: 'ellipse', name: 'Oval', group: 'Basic' },
  { kind: 'triangle', name: 'Triangle', group: 'Basic' },
  { kind: 'rightTriangle', name: 'Right triangle', group: 'Basic' },
  { kind: 'diamond', name: 'Diamond', group: 'Basic' },
  { kind: 'pentagon', name: 'Pentagon', group: 'Basic' },
  { kind: 'hexagon', name: 'Hexagon', group: 'Basic' },
  { kind: 'octagon', name: 'Octagon', group: 'Basic' },
  { kind: 'parallelogram', name: 'Parallelogram', group: 'Basic' },
  { kind: 'trapezoid', name: 'Trapezoid', group: 'Basic' },
  { kind: 'plus', name: 'Plus', group: 'Basic' },
  { kind: 'heart', name: 'Heart', group: 'Basic' },
  { kind: 'cloud', name: 'Cloud', group: 'Basic' },
  { kind: 'lightning', name: 'Lightning', group: 'Basic' },
  { kind: 'moon', name: 'Moon', group: 'Basic' },
  { kind: 'donut', name: 'Donut', group: 'Basic' },
  { kind: 'frame', name: 'Frame', group: 'Basic' },
  { kind: 'cylinder', name: 'Cylinder', group: 'Basic' },
  { kind: 'blockArc', name: 'Block arc', group: 'Basic' },
  { kind: 'wave', name: 'Wave', group: 'Basic' },
  { kind: 'arrowRight', name: 'Right arrow', group: 'Arrows' },
  { kind: 'arrowLeft', name: 'Left arrow', group: 'Arrows' },
  { kind: 'arrowUp', name: 'Up arrow', group: 'Arrows' },
  { kind: 'arrowDown', name: 'Down arrow', group: 'Arrows' },
  { kind: 'arrowDouble', name: 'Double arrow', group: 'Arrows' },
  { kind: 'chevron', name: 'Chevron', group: 'Arrows' },
  { kind: 'star4', name: '4 point star', group: 'Stars and callouts' },
  { kind: 'star5', name: '5 point star', group: 'Stars and callouts' },
  { kind: 'star6', name: '6 point star', group: 'Stars and callouts' },
  { kind: 'star8', name: '8 point star', group: 'Stars and callouts' },
  { kind: 'callout', name: 'Speech bubble', group: 'Stars and callouts' },
  { kind: 'line', name: 'Line', group: 'Lines' },
  { kind: 'arrowLine', name: 'Arrow line', group: 'Lines' },
];

const pts = (points: [number, number][]) =>
  'M' + points.map(([x, y]) => `${r(x)} ${r(y)}`).join(' L') + ' Z';
const r = (v: number) => Math.round(v * 100) / 100;

function polygon(n: number, w: number, h: number, offset = -Math.PI / 2): string {
  const p: [number, number][] = [];
  for (let i = 0; i < n; i++) {
    const a = offset + (i * 2 * Math.PI) / n;
    p.push([w / 2 + (w / 2) * Math.cos(a), h / 2 + (h / 2) * Math.sin(a)]);
  }
  return pts(p);
}

function star(n: number, w: number, h: number, inner: number): string {
  const p: [number, number][] = [];
  for (let i = 0; i < n * 2; i++) {
    const a = -Math.PI / 2 + (i * Math.PI) / n;
    const k = i % 2 === 0 ? 1 : inner;
    p.push([w / 2 + (w / 2) * k * Math.cos(a), h / 2 + (h / 2) * k * Math.sin(a)]);
  }
  return pts(p);
}

function ellipsePath(cx: number, cy: number, rx: number, ry: number, ccw = false): string {
  const s = ccw ? 0 : 1;
  return `M${r(cx - rx)} ${r(cy)} A${r(rx)} ${r(ry)} 0 1 ${s} ${r(cx + rx)} ${r(cy)} A${r(rx)} ${r(ry)} 0 1 ${s} ${r(cx - rx)} ${r(cy)} Z`;
}

/** SVG path data for a shape drawn in a w x h box, in the box's own pixels. */
export function shapePath(kind: ShapeKind, w: number, h: number, radius = 24): string {
  switch (kind) {
    case 'rect':
      return pts([[0, 0], [w, 0], [w, h], [0, h]]);
    case 'roundRect': {
      const rr = Math.min(radius, w / 2, h / 2);
      return `M${rr} 0 H${w - rr} A${rr} ${rr} 0 0 1 ${w} ${rr} V${h - rr} A${rr} ${rr} 0 0 1 ${w - rr} ${h} H${rr} A${rr} ${rr} 0 0 1 0 ${h - rr} V${rr} A${rr} ${rr} 0 0 1 ${rr} 0 Z`;
    }
    case 'ellipse':
      return ellipsePath(w / 2, h / 2, w / 2, h / 2);
    case 'triangle':
      return pts([[w / 2, 0], [w, h], [0, h]]);
    case 'rightTriangle':
      return pts([[0, 0], [w, h], [0, h]]);
    case 'diamond':
      return pts([[w / 2, 0], [w, h / 2], [w / 2, h], [0, h / 2]]);
    case 'pentagon':
      return polygon(5, w, h);
    case 'hexagon':
      return pts([[w * 0.25, 0], [w * 0.75, 0], [w, h / 2], [w * 0.75, h], [w * 0.25, h], [0, h / 2]]);
    case 'octagon': {
      const a = Math.min(w, h) * 0.29;
      return pts([[a, 0], [w - a, 0], [w, a], [w, h - a], [w - a, h], [a, h], [0, h - a], [0, a]]);
    }
    case 'parallelogram':
      return pts([[w * 0.25, 0], [w, 0], [w * 0.75, h], [0, h]]);
    case 'trapezoid':
      return pts([[w * 0.2, 0], [w * 0.8, 0], [w, h], [0, h]]);
    case 'plus': {
      const a = w * 0.33, b = h * 0.33;
      return pts([[a, 0], [w - a, 0], [w - a, b], [w, b], [w, h - b], [w - a, h - b], [w - a, h], [a, h], [a, h - b], [0, h - b], [0, b], [a, b]]);
    }
    case 'star4': return star(4, w, h, 0.38);
    case 'star5': return star(5, w, h, 0.45);
    case 'star6': return star(6, w, h, 0.55);
    case 'star8': return star(8, w, h, 0.65);
    case 'arrowRight':
      return pts([[0, h * 0.25], [w * 0.6, h * 0.25], [w * 0.6, 0], [w, h / 2], [w * 0.6, h], [w * 0.6, h * 0.75], [0, h * 0.75]]);
    case 'arrowLeft':
      return pts([[w, h * 0.25], [w * 0.4, h * 0.25], [w * 0.4, 0], [0, h / 2], [w * 0.4, h], [w * 0.4, h * 0.75], [w, h * 0.75]]);
    case 'arrowUp':
      return pts([[w * 0.25, h], [w * 0.25, h * 0.4], [0, h * 0.4], [w / 2, 0], [w, h * 0.4], [w * 0.75, h * 0.4], [w * 0.75, h]]);
    case 'arrowDown':
      return pts([[w * 0.25, 0], [w * 0.25, h * 0.6], [0, h * 0.6], [w / 2, h], [w, h * 0.6], [w * 0.75, h * 0.6], [w * 0.75, 0]]);
    case 'arrowDouble':
      return pts([[0, h / 2], [w * 0.25, 0], [w * 0.25, h * 0.25], [w * 0.75, h * 0.25], [w * 0.75, 0], [w, h / 2], [w * 0.75, h], [w * 0.75, h * 0.75], [w * 0.25, h * 0.75], [w * 0.25, h]]);
    case 'chevron':
      return pts([[0, 0], [w * 0.7, 0], [w, h / 2], [w * 0.7, h], [0, h], [w * 0.3, h / 2]]);
    case 'heart':
      return `M${w / 2} ${h * 0.25} C${w / 2} ${h * 0.1} ${w * 0.35} 0 ${w * 0.22} 0 C${w * 0.08} 0 0 ${h * 0.12} 0 ${h * 0.3} C0 ${h * 0.55} ${w * 0.3} ${h * 0.75} ${w / 2} ${h} C${w * 0.7} ${h * 0.75} ${w} ${h * 0.55} ${w} ${h * 0.3} C${w} ${h * 0.12} ${w * 0.92} 0 ${w * 0.78} 0 C${w * 0.65} 0 ${w / 2} ${h * 0.1} ${w / 2} ${h * 0.25} Z`;
    case 'callout': {
      const bh = h * 0.78;
      const rr = Math.min(w, bh) * 0.12;
      return `M${rr} 0 H${w - rr} A${rr} ${rr} 0 0 1 ${w} ${rr} V${bh - rr} A${rr} ${rr} 0 0 1 ${w - rr} ${bh} H${w * 0.42} L${w * 0.22} ${h} L${w * 0.26} ${bh} H${rr} A${rr} ${rr} 0 0 1 0 ${bh - rr} V${rr} A${rr} ${rr} 0 0 1 ${rr} 0 Z`;
    }
    case 'cloud':
      return `M${w * 0.25} ${h * 0.85} C${w * 0.05} ${h * 0.85} 0 ${h * 0.6} ${w * 0.14} ${h * 0.52} C${w * 0.06} ${h * 0.3} ${w * 0.28} ${h * 0.18} ${w * 0.38} ${h * 0.28} C${w * 0.44} ${h * 0.06} ${w * 0.7} ${h * 0.06} ${w * 0.72} ${h * 0.28} C${w * 0.9} ${h * 0.2} ${w} ${h * 0.42} ${w * 0.9} ${h * 0.55} C${w} ${h * 0.7} ${w * 0.9} ${h * 0.88} ${w * 0.75} ${h * 0.85} Z`;
    case 'lightning':
      return pts([[w * 0.38, 0], [w * 0.72, 0], [w * 0.55, h * 0.36], [w * 0.85, h * 0.36], [w * 0.25, h], [w * 0.4, h * 0.52], [w * 0.15, h * 0.52]]);
    case 'moon':
      return `M${w * 0.75} ${h * 0.05} A${w * 0.5} ${h * 0.5} 0 1 0 ${w * 0.75} ${h * 0.95} A${w * 0.38} ${h * 0.45} 0 1 1 ${w * 0.75} ${h * 0.05} Z`;
    case 'donut':
      return ellipsePath(w / 2, h / 2, w / 2, h / 2) + ' ' + ellipsePath(w / 2, h / 2, w * 0.3, h * 0.3, true);
    case 'frame': {
      const t = Math.min(w, h) * 0.12;
      return `M0 0 H${w} V${h} H0 Z M${t} ${t} V${h - t} H${w - t} V${t} Z`;
    }
    case 'cylinder': {
      const ry = h * 0.1;
      return `M0 ${ry} A${w / 2} ${ry} 0 0 1 ${w} ${ry} V${h - ry} A${w / 2} ${ry} 0 0 1 0 ${h - ry} Z M0 ${ry} A${w / 2} ${ry} 0 0 0 ${w} ${ry}`;
    }
    case 'blockArc': {
      const t = Math.min(w, h) * 0.22;
      return `M0 ${h} A${w / 2} ${h} 0 0 1 ${w} ${h} H${w - t} A${w / 2 - t} ${h - t} 0 0 0 ${t} ${h} Z`;
    }
    case 'wave':
      return `M0 ${h * 0.2} C${w * 0.25} ${-h * 0.1} ${w * 0.25} ${h * 0.5} ${w / 2} ${h * 0.2} C${w * 0.75} ${-h * 0.1} ${w * 0.75} ${h * 0.5} ${w} ${h * 0.2} V${h * 0.8} C${w * 0.75} ${h * 1.1} ${w * 0.75} ${h * 0.5} ${w / 2} ${h * 0.8} C${w * 0.25} ${h * 1.1} ${w * 0.25} ${h * 0.5} 0 ${h * 0.8} Z`;
    case 'line':
    case 'arrowLine':
      return `M0 ${h / 2} H${w}`;
  }
}
