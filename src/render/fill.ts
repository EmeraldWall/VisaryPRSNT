import type { CSSProperties } from 'react';
import type { Background, Fill, Shadow, Stroke, TextStyle } from '../model/types';

export function fillCss(fill: Fill | undefined): string {
  if (!fill || fill.type === 'none') return 'transparent';
  if (fill.type === 'solid') return fill.color;
  const c2 = fill.color2 ?? fill.color;
  if (fill.type === 'linear') return `linear-gradient(${fill.angle ?? 135}deg, ${fill.color}, ${c2})`;
  return `radial-gradient(circle at center, ${fill.color}, ${c2})`;
}

export function backgroundCss(bg: Background): CSSProperties {
  const layers: string[] = [];
  if (bg.image) {
    if (bg.overlay) layers.push(`linear-gradient(rgba(0,0,0,${bg.overlay}), rgba(0,0,0,${bg.overlay}))`);
    layers.push(`url("${bg.image}")`);
  }
  const base = fillCss(bg.fill);
  if (bg.fill.type === 'linear' || bg.fill.type === 'radial') layers.push(base);
  return {
    backgroundImage: layers.length ? layers.join(', ') : undefined,
    backgroundColor: bg.fill.type === 'solid' ? bg.fill.color : bg.fill.type === 'none' ? '#ffffff' : bg.fill.color,
    backgroundSize: bg.image ? (bg.imageFit ?? 'cover') : undefined,
    backgroundPosition: 'center',
    backgroundRepeat: 'no-repeat',
  };
}

export function strokeCss(stroke: Stroke | undefined): string | undefined {
  if (!stroke || stroke.width <= 0) return undefined;
  return `${stroke.width}px ${stroke.dash} ${stroke.color}`;
}

export function dashArray(stroke: Stroke): string | undefined {
  if (stroke.dash === 'dashed') return `${stroke.width * 3} ${stroke.width * 2}`;
  if (stroke.dash === 'dotted') return `${stroke.width} ${stroke.width * 1.5}`;
  return undefined;
}

export function shadowFilter(shadow: Shadow | undefined): string | undefined {
  if (!shadow?.enabled) return undefined;
  return `drop-shadow(${shadow.x}px ${shadow.y}px ${shadow.blur}px ${shadow.color})`;
}

export function textCss(style: TextStyle): CSSProperties {
  const deco = [style.underline && 'underline', style.strike && 'line-through'].filter(Boolean).join(' ');
  return {
    fontFamily: `"${style.fontFamily}", system-ui, sans-serif`,
    fontSize: style.fontSize,
    color: style.color,
    fontWeight: style.bold ? 700 : 400,
    fontStyle: style.italic ? 'italic' : 'normal',
    textDecoration: deco || 'none',
    textAlign: style.align,
    lineHeight: style.lineHeight,
    letterSpacing: style.letterSpacing ? `${style.letterSpacing}px` : undefined,
    justifyContent: style.vAlign === 'top' ? 'flex-start' : style.vAlign === 'middle' ? 'center' : 'flex-end',
  };
}
