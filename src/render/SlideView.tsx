import { memo, type CSSProperties, type ReactNode, type Ref } from 'react';
import type { Slide, SlideElement } from '../model/types';
import { backgroundCss } from './fill';
import { ElementView, type RenderMode } from './ElementView';

export interface SlideContentProps {
  slide: Slide;
  W: number;
  H: number;
  mode: RenderMode;
  slideNumber?: number;
  showSlideNumber?: boolean;
  editingId?: string | null;
  hiddenIds?: Set<string>;
  onHtmlChange?: (id: string, html: string) => void;
  onCellChange?: (id: string, r: number, c: number, html: string) => void;
  onLink?: (el: SlideElement) => void;
  innerRef?: Ref<HTMLDivElement>;
  style?: CSSProperties;
  children?: ReactNode;
}

/** A slide at its logical size (for example 1920 x 1080). Callers scale it with CSS. */
export function SlideContent({
  slide, W, H, mode, slideNumber, showSlideNumber, editingId, hiddenIds,
  onHtmlChange, onCellChange, onLink, innerRef, style, children,
}: SlideContentProps) {
  return (
    <div
      ref={innerRef}
      className={`slide-content mode-${mode}`}
      data-slide-id={slide.id}
      style={{ width: W, height: H, ...backgroundCss(slide.background), ...style }}
    >
      {slide.elements.map((el) => (
        <ElementView
          key={el.id}
          el={el}
          mode={mode}
          editing={editingId === el.id}
          slideNumber={slideNumber}
          hidden={hiddenIds?.has(el.id)}
          onHtmlChange={onHtmlChange}
          onCellChange={onCellChange}
          onLink={onLink}
        />
      ))}
      {showSlideNumber && slideNumber !== undefined && (
        <div className="slide-num-badge" style={{ fontSize: Math.round(H * 0.022) }}>{slideNumber}</div>
      )}
      {children}
    </div>
  );
}

interface ThumbProps {
  slide: Slide;
  W: number;
  H: number;
  width: number;
  slideNumber?: number;
  showSlideNumber?: boolean;
}

function SlideThumbInner({ slide, W, H, width, slideNumber, showSlideNumber }: ThumbProps) {
  const scale = width / W;
  return (
    <div className="slide-thumb" style={{ width, height: H * scale }}>
      <div style={{ transform: `scale(${scale})`, transformOrigin: '0 0', width: W, height: H }}>
        <SlideContent slide={slide} W={W} H={H} mode="thumb" slideNumber={slideNumber} showSlideNumber={showSlideNumber} />
      </div>
    </div>
  );
}

export const SlideThumb = memo(SlideThumbInner);
