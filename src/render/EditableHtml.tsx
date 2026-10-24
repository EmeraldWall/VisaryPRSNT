import { useEffect, useLayoutEffect, useRef, type CSSProperties, type KeyboardEvent } from 'react';
import { escapeHtml } from '../model/util';

let lastPoint: { x: number; y: number } | null = null;

/** Remembers where the user double clicked so editing can start with the caret there. */
export function rememberEditPoint(x: number, y: number): void {
  lastPoint = { x, y };
}

function placeCaret(el: HTMLElement) {
  const sel = window.getSelection();
  if (!sel) return;
  let range: Range | null = null;
  const p = lastPoint;
  lastPoint = null;
  if (p) {
    const doc = document as Document & {
      caretPositionFromPoint?: (x: number, y: number) => { offsetNode: Node; offset: number } | null;
    };
    if (doc.caretPositionFromPoint) {
      const pos = doc.caretPositionFromPoint(p.x, p.y);
      if (pos && el.contains(pos.offsetNode)) {
        range = document.createRange();
        range.setStart(pos.offsetNode, pos.offset);
      }
    } else if (document.caretRangeFromPoint) {
      const r = document.caretRangeFromPoint(p.x, p.y);
      if (r && el.contains(r.startContainer)) range = r;
    }
  }
  if (!range) {
    range = document.createRange();
    range.selectNodeContents(el);
  }
  sel.removeAllRanges();
  sel.addRange(range);
}

interface Props {
  html: string;
  onChange: (html: string) => void;
  className?: string;
  style?: CSSProperties;
  autoFocus?: boolean;
  onKeyDown?: (e: KeyboardEvent<HTMLDivElement>) => void;
}

/**
 * Uncontrolled contentEditable: React never rewrites the DOM while it has focus,
 * so the caret does not jump while the store receives every keystroke.
 */
export function EditableHtml({ html, onChange, className, style, autoFocus, onKeyDown }: Props) {
  const ref = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    const el = ref.current;
    if (el && document.activeElement !== el && el.innerHTML !== html) el.innerHTML = html;
  }, [html]);

  useEffect(() => {
    const el = ref.current;
    if (!el || !autoFocus) return;
    el.focus({ preventScroll: true });
    placeCaret(el);
  }, [autoFocus]);

  return (
    <div
      ref={ref}
      className={className}
      style={style}
      contentEditable
      suppressContentEditableWarning
      spellCheck
      onInput={(e) => onChange(e.currentTarget.innerHTML)}
      onKeyDown={onKeyDown}
      onPointerDown={(e) => e.stopPropagation()}
      onPaste={(e) => {
        const text = e.clipboardData.getData('text/plain');
        if (!text) return;
        e.preventDefault();
        const html = text.split(/\r?\n/).map(escapeHtml).join('<br>');
        document.execCommand('insertHTML', false, html);
      }}
    />
  );
}
