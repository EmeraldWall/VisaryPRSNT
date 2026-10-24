import { memo, useId, type CSSProperties } from 'react';
import type {
  Fill, ImageElement, ShapeElement, SlideElement, TableElement, TextElement, TextStyle, VideoElement,
} from '../model/types';
import { shapePath } from '../model/shapes';
import { embedUrl } from '../model/util';
import { dashArray, fillCss, shadowFilter, strokeCss, textCss } from './fill';
import { ChartView } from './Chart';
import { EditableHtml } from './EditableHtml';

export type RenderMode = 'edit' | 'thumb' | 'present' | 'print';

export interface ElementViewProps {
  el: SlideElement;
  mode: RenderMode;
  editing?: boolean;
  slideNumber?: number;
  hidden?: boolean;
  onHtmlChange?: (id: string, html: string) => void;
  onCellChange?: (id: string, r: number, c: number, html: string) => void;
  onLink?: (el: SlideElement) => void;
}

export function withFields(html: string, slideNumber?: number): string {
  if (slideNumber === undefined || !html.includes('data-field')) return html;
  return html.replace(/(<span data-field="slide-number"[^>]*>)[^<]*(<\/span>)/g, `$1${slideNumber}$2`);
}

function TextBody({ html, style, editing, onChange, slideNumber }: {
  html: string; style: TextStyle; editing?: boolean; onChange?: (html: string) => void; slideNumber?: number;
}) {
  if (editing && onChange) {
    return <EditableHtml className="text-html editing" html={html} onChange={onChange} autoFocus />;
  }
  return <div className="text-html" dangerouslySetInnerHTML={{ __html: withFields(html, slideNumber) }} data-empty={!html || undefined} style={{ textAlign: style.align }} />;
}

function SvgFill({ id, fill }: { id: string; fill: Fill }) {
  if (fill.type === 'linear') {
    return (
      <linearGradient id={id} gradientTransform={`rotate(${(fill.angle ?? 135) - 90} 0.5 0.5)`}>
        <stop offset="0" stopColor={fill.color} />
        <stop offset="1" stopColor={fill.color2 ?? fill.color} />
      </linearGradient>
    );
  }
  if (fill.type === 'radial') {
    return (
      <radialGradient id={id}>
        <stop offset="0" stopColor={fill.color} />
        <stop offset="1" stopColor={fill.color2 ?? fill.color} />
      </radialGradient>
    );
  }
  return null;
}

function ShapeView({ el, editing, onChange, slideNumber }: { el: ShapeElement; editing?: boolean; onChange?: (h: string) => void; slideNumber?: number }) {
  const gid = useId().replace(/:/g, '');
  const isLine = el.shape === 'line' || el.shape === 'arrowLine';
  const fill = el.fill.type === 'solid' ? el.fill.color : el.fill.type === 'none' ? 'none' : `url(#g${gid})`;
  const sw = el.stroke.width;
  const flip = `${el.flipH ? 'scaleX(-1)' : ''} ${el.flipV ? 'scaleY(-1)' : ''}`.trim();
  const arrowSize = Math.max(3, 12 / Math.max(1, sw / 4));
  return (
    <>
      <svg className="shape-svg" width={el.w} height={el.h} viewBox={`0 0 ${el.w} ${el.h}`} style={{ transform: flip || undefined }}>
        <defs>
          <SvgFill id={`g${gid}`} fill={el.fill} />
          {el.shape === 'arrowLine' && (
            <marker id={`m${gid}`} viewBox="0 0 10 10" refX="8" refY="5" markerWidth={arrowSize} markerHeight={arrowSize} orient="auto-start-reverse">
              <path d="M0 0 L10 5 L0 10 Z" fill={el.stroke.color} />
            </marker>
          )}
        </defs>
        <path
          d={shapePath(el.shape, el.w, el.h, el.radius)}
          fill={isLine ? 'none' : fill}
          fillRule="evenodd"
          stroke={sw > 0 ? el.stroke.color : 'none'}
          strokeWidth={sw}
          strokeDasharray={dashArray(el.stroke)}
          strokeLinecap={isLine ? 'round' : undefined}
          strokeLinejoin="round"
          markerEnd={el.shape === 'arrowLine' ? `url(#m${gid})` : undefined}
        />
      </svg>
      {!isLine && (el.html || editing) && (
        <div className="text-box shape-text" style={{ ...textCss(el.style), padding: Math.min(el.w, el.h) * 0.08 }}>
          <TextBody html={el.html} style={el.style} editing={editing} onChange={onChange} slideNumber={slideNumber} />
        </div>
      )}
    </>
  );
}

function TextView({ el, editing, onChange, slideNumber }: { el: TextElement; editing?: boolean; onChange?: (h: string) => void; slideNumber?: number }) {
  const style: CSSProperties = {
    ...textCss(el.style),
    background: fillCss(el.fill),
    border: strokeCss(el.stroke),
    borderRadius: el.radius,
    padding: el.padding,
  };
  return (
    <div className="text-box" style={style}>
      <TextBody html={el.html} style={el.style} editing={editing} onChange={onChange} slideNumber={slideNumber} />
    </div>
  );
}

function ImageView({ el }: { el: ImageElement }) {
  const f = el.filters;
  const filter = [
    f.brightness !== 100 && `brightness(${f.brightness}%)`,
    f.contrast !== 100 && `contrast(${f.contrast}%)`,
    f.saturate !== 100 && `saturate(${f.saturate}%)`,
    f.grayscale && `grayscale(${f.grayscale}%)`,
    f.sepia && `sepia(${f.sepia}%)`,
    f.blur && `blur(${f.blur}px)`,
    f.hue && `hue-rotate(${f.hue}deg)`,
  ].filter(Boolean).join(' ');
  const flip = `${el.flipH ? 'scaleX(-1)' : ''} ${el.flipV ? 'scaleY(-1)' : ''}`.trim();
  return (
    <img
      className="img-el"
      src={el.src}
      alt={el.name ?? ''}
      draggable={false}
      style={{
        objectFit: el.fit, borderRadius: el.radius, filter: filter || undefined,
        border: strokeCss(el.stroke), transform: flip || undefined,
      }}
    />
  );
}

function TableView({ el, editing, onCellChange }: { el: TableElement; editing?: boolean; onCellChange?: (r: number, c: number, html: string) => void }) {
  const css = textCss(el.style);
  return (
    <table className="table-el" style={{ ...css, borderColor: el.borderColor }}>
      <tbody>
        {el.rows.map((row, r) => {
          const header = el.headerRow && r === 0;
          const bg = header ? el.headerFill : el.bandedRows && r % 2 === 0 ? el.bandFill : el.cellFill;
          return (
            <tr key={r}>
              {row.map((cell, c) => (
                <td
                  key={c}
                  style={{
                    background: bg, borderColor: el.borderColor,
                    color: header ? el.headerColor : el.style.color, fontWeight: header || el.style.bold ? 700 : 400,
                  }}
                >
                  {editing && onCellChange ? (
                    <EditableHtml className="cell-html" html={cell} onChange={(h) => onCellChange(r, c, h)} autoFocus={r === 0 && c === 0} />
                  ) : (
                    <div className="cell-html" dangerouslySetInnerHTML={{ __html: cell }} />
                  )}
                </td>
              ))}
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

function VideoView({ el, mode }: { el: VideoElement; mode: RenderMode }) {
  const embed = embedUrl(el.src);
  if (mode === 'present') {
    if (embed) {
      const q = new URLSearchParams();
      if (el.autoplay) q.set('autoplay', '1');
      if (el.muted) q.set('mute', '1');
      if (el.loop) q.set('loop', '1');
      return (
        <iframe
          className="video-el"
          src={`${embed}?${q.toString()}`}
          allow="autoplay; encrypted-media; picture-in-picture; fullscreen"
          allowFullScreen
          title="Embedded video"
        />
      );
    }
    return <video className="video-el" src={el.src} controls autoPlay={el.autoplay} loop={el.loop} muted={el.muted} playsInline />;
  }
  return (
    <div className="video-ph">
      {!embed && el.src && <video className="video-el" src={el.src} muted preload="metadata" />}
      <div className="video-play">
        <svg viewBox="0 0 24 24" width="30%" height="30%"><circle cx="12" cy="12" r="11" fill="rgba(0,0,0,.55)" /><path d="M9.5 7.5v9l7-4.5z" fill="#fff" /></svg>
        {embed && <span>{embed.includes('youtube') ? 'YouTube video' : 'Vimeo video'}</span>}
      </div>
    </div>
  );
}

function ElementViewInner({ el, mode, editing, slideNumber, hidden, onHtmlChange, onCellChange, onLink }: ElementViewProps) {
  const wrapper: CSSProperties = {
    left: el.x,
    top: el.y,
    width: el.w,
    height: el.h,
    transform: el.rot ? `rotate(${el.rot}deg)` : undefined,
    visibility: hidden ? 'hidden' : undefined,
    cursor: mode === 'present' && el.link ? 'pointer' : undefined,
  };
  const change = onHtmlChange ? (h: string) => onHtmlChange(el.id, h) : undefined;
  let content: JSX.Element;
  switch (el.type) {
    case 'text': content = <TextView el={el} editing={editing} onChange={change} slideNumber={slideNumber} />; break;
    case 'shape': content = <ShapeView el={el} editing={editing} onChange={change} slideNumber={slideNumber} />; break;
    case 'image': content = <ImageView el={el} />; break;
    case 'table': content = <TableView el={el} editing={editing} onCellChange={onCellChange ? (r, c, h) => onCellChange(el.id, r, c, h) : undefined} />; break;
    case 'chart': content = <ChartView el={el} />; break;
    case 'video': content = <VideoView el={el} mode={mode} />; break;
  }
  return (
    <div
      className={`el el-${el.type}${editing ? ' is-editing' : ''}`}
      data-el-id={el.id}
      style={wrapper}
      onClick={mode === 'present' && el.link && onLink ? (e) => { e.stopPropagation(); onLink(el); } : undefined}
    >
      <div className="el-anim" style={{ opacity: el.opacity }}>
        <div className="el-content" style={{ filter: shadowFilter(el.shadow) }}>{content}</div>
      </div>
    </div>
  );
}

export const ElementView = memo(ElementViewInner);
