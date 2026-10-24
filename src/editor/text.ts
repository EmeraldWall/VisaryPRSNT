import type { TextStyle } from '../model/types';
import { getState, selectedElements, updateSelected } from '../store/store';
import { setTextStyle } from './actions';

/** True when the caret is inside a text box with a non empty selection. */
function hasRangeInEditor(): boolean {
  const ui = getState().ui;
  if (!ui.editingId) return false;
  const sel = window.getSelection();
  if (!sel || sel.rangeCount === 0 || sel.isCollapsed) return false;
  const node = sel.anchorNode as Node | null;
  const host = (node instanceof HTMLElement ? node : node?.parentElement)?.closest('[contenteditable="true"]');
  return !!host;
}

function exec(cmd: string, value?: string) {
  document.execCommand('styleWithCSS', false, 'true');
  document.execCommand(cmd, false, value);
}

type Toggle = 'bold' | 'italic' | 'underline' | 'strike';

const CMD: Record<Toggle, string> = { bold: 'bold', italic: 'italic', underline: 'underline', strike: 'strikeThrough' };

/** Bold, italic and friends apply to selected text while editing, otherwise to whole boxes. */
export function toggleStyle(key: Toggle): void {
  if (hasRangeInEditor()) {
    exec(CMD[key]);
    return;
  }
  const els = selectedElements().filter((e) => 'style' in e) as { style: TextStyle }[];
  if (!els.length) return;
  const on = !els.every((e) => e.style[key]);
  setTextStyle({ [key]: on });
}

export function setColor(color: string): void {
  if (hasRangeInEditor()) exec('foreColor', color);
  else setTextStyle({ color });
}

export function setHighlight(color: string): void {
  if (hasRangeInEditor()) exec('hiliteColor', color);
}

export function setFont(fontFamily: string): void {
  if (hasRangeInEditor()) exec('fontName', fontFamily);
  else setTextStyle({ fontFamily });
}

export function setFontSize(px: number): void {
  if (hasRangeInEditor()) {
    // execCommand only knows sizes 1 to 7, so mark the run with 7 and swap it for a pixel size.
    document.execCommand('styleWithCSS', false, 'false');
    document.execCommand('fontSize', false, '7');
    document.querySelectorAll('[contenteditable="true"] font[size="7"]').forEach((f) => {
      const span = document.createElement('span');
      span.style.fontSize = `${px}px`;
      while (f.firstChild) span.appendChild(f.firstChild);
      f.replaceWith(span);
    });
    const host = document.querySelector('[contenteditable="true"]');
    host?.dispatchEvent(new Event('input', { bubbles: true }));
    return;
  }
  setTextStyle({ fontSize: px });
}

export function growFont(delta: number): void {
  const [el] = selectedElements().filter((e) => 'style' in e) as { style: TextStyle }[];
  if (!el) return;
  setFontSize(Math.max(6, Math.round(el.style.fontSize * (delta > 0 ? 1.12 : 0.89))));
}

export function setAlign(align: TextStyle['align']): void {
  if (getState().ui.editingId) exec(align === 'justify' ? 'justifyFull' : `justify${align[0].toUpperCase()}${align.slice(1)}`);
  setTextStyle({ align });
}

/** Turns every paragraph of a box into a list item, or back into plain paragraphs. */
export function toggleListHtml(html: string, kind: 'ul' | 'ol'): string {
  const doc = new DOMParser().parseFromString(`<div>${html}</div>`, 'text/html');
  const root = doc.body.firstElementChild as HTMLElement;
  const lists = root.querySelectorAll('ul, ol');
  const sameKind = lists.length > 0 && [...lists].every((l) => l.tagName.toLowerCase() === kind);
  if (sameKind) {
    lists.forEach((list) => {
      const frag = doc.createDocumentFragment();
      list.querySelectorAll(':scope > li').forEach((li) => {
        const div = doc.createElement('div');
        div.innerHTML = li.innerHTML;
        frag.appendChild(div);
      });
      list.replaceWith(frag);
    });
    return root.innerHTML;
  }
  const items: string[] = [];
  root.childNodes.forEach((n) => {
    if (n instanceof HTMLElement && (n.tagName === 'UL' || n.tagName === 'OL')) {
      n.querySelectorAll(':scope > li').forEach((li) => items.push(li.innerHTML));
    } else if (n instanceof HTMLElement) {
      items.push(n.innerHTML);
    } else if ((n.textContent ?? '').trim()) {
      items.push(n.textContent ?? '');
    }
  });
  return `<${kind}>${items.map((i) => `<li>${i}</li>`).join('')}</${kind}>`;
}

export function toggleList(kind: 'ul' | 'ol'): void {
  if (getState().ui.editingId) {
    exec(kind === 'ul' ? 'insertUnorderedList' : 'insertOrderedList');
    return;
  }
  updateSelected((el) => {
    if (el.type === 'text' || el.type === 'shape') el.html = toggleListHtml(el.html, kind);
  });
}

export function insertLink(url: string): void {
  if (hasRangeInEditor()) exec('createLink', url);
  else updateSelected((el) => { el.link = { kind: 'url', url }; });
}
