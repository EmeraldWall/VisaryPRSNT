import { useState } from 'react';
import { fileToDataUrl } from '../model/util';
import { pickFile } from '../io/file';
import { setUI, useStore } from '../store/store';
import { findMatches, insertImage, insertVideo, replaceAll } from './actions';
import { Modal, Toggle } from '../ui/controls';

const close = () => setUI({ modal: null });

function FindDialog() {
  const [q, setQ] = useState('');
  const [r, setR] = useState('');
  const [matchCase, setMatchCase] = useState(false);
  const [msg, setMsg] = useState('');
  const [cursor, setCursor] = useState(0);
  const matches = findMatches(q, matchCase);
  const next = () => {
    if (!matches.length) return;
    const m = matches[cursor % matches.length];
    setUI({ currentSlideId: m.slideId, selection: [m.elementId], view: 'normal' });
    setCursor(cursor + 1);
  };
  return (
    <Modal title="Find and replace" onClose={close}>
      <div className="stack">
        <label className="field"><span className="field-label">Find</span><input autoFocus value={q} onChange={(e) => { setQ(e.target.value); setCursor(0); }} onKeyDown={(e) => { if (e.key === 'Enter') next(); }} /></label>
        <label className="field"><span className="field-label">Replace with</span><input value={r} onChange={(e) => setR(e.target.value)} /></label>
        <Toggle label="Match case" checked={matchCase} onChange={setMatchCase} />
        <p className="hint">{q ? `${matches.length} object${matches.length === 1 ? '' : 's'} contain this text.` : 'Type to search all slides.'} {msg}</p>
        <div className="btn-row">
          <button className="btn" onClick={next} disabled={!matches.length}>Find next</button>
          <button className="btn primary" onClick={() => setMsg(`Replaced text in ${replaceAll(q, r, matchCase)} objects.`)} disabled={!matches.length}>Replace all</button>
        </div>
      </div>
    </Modal>
  );
}

function VideoDialog() {
  const [url, setUrl] = useState('');
  const ok = /^https?:\/\//i.test(url.trim());
  return (
    <Modal title="Insert video" onClose={close}>
      <div className="stack">
        <label className="field"><span className="field-label">YouTube, Vimeo or video file address</span><input autoFocus placeholder="https://www.youtube.com/watch?v=..." value={url} onChange={(e) => setUrl(e.target.value)} /></label>
        <div className="btn-row">
          <button className="btn primary" disabled={!ok} onClick={() => { insertVideo(url.trim()); close(); }}>Insert</button>
          <button className="btn" onClick={async () => {
            const f = await pickFile('video/*');
            if (!f) return;
            if (f.size > 40 * 1024 * 1024 && !window.confirm('This video is large and will make the presentation file big. Continue?')) return;
            insertVideo(await fileToDataUrl(f));
            close();
          }}>Upload a video file</button>
        </div>
      </div>
    </Modal>
  );
}

function ImageUrlDialog() {
  const [url, setUrl] = useState('');
  const ok = /^(https?:\/\/|data:image\/)/i.test(url.trim());
  return (
    <Modal title="Picture from address" onClose={close}>
      <div className="stack">
        <label className="field"><span className="field-label">Image address</span><input autoFocus placeholder="https://..." value={url} onChange={(e) => setUrl(e.target.value)} /></label>
        <p className="hint">Pictures from other websites stay linked. For offline use and PowerPoint export, uploading the file is more reliable.</p>
        <button className="btn primary" disabled={!ok} onClick={() => { void insertImage(url.trim()); close(); }}>Insert</button>
      </div>
    </Modal>
  );
}

const SHORTCUTS: [string, string][] = [
  ['Ctrl+Z / Ctrl+Y', 'Undo / redo'], ['Ctrl+C, X, V', 'Copy, cut, paste'], ['Ctrl+D', 'Duplicate'],
  ['Delete', 'Delete selection'], ['Arrows (Shift)', 'Nudge 1 px (10 px)'], ['Ctrl+A', 'Select all'],
  ['Ctrl+G / Ctrl+Shift+G', 'Group / ungroup'], ['Ctrl+] / Ctrl+[', 'Bring forward / send backward'],
  ['Ctrl+B, I, U', 'Bold, italic, underline'], ['Ctrl+M', 'New slide'], ['Ctrl+F', 'Find and replace'],
  ['Ctrl+S', 'Save a copy to your computer'], ['Enter', 'Edit selected text'], ['Esc', 'Stop editing or deselect'],
  ['F5 / Shift+F5', 'Present from start / current slide'], ['Page Up / Page Down', 'Previous / next slide'],
  ['Shift while dragging', 'Keep straight or keep proportions'], ['Alt click', 'Select one object inside a group'],
  ['In show: N, Space, arrows', 'Next / previous'], ['In show: B / W', 'Black / white screen'],
  ['In show: D / E / L', 'Pen / erase / laser pointer'], ['In show: G', 'All slides'], ['In show: number + Enter', 'Go to slide'],
];

function ShortcutsDialog() {
  return (
    <Modal title="Keyboard shortcuts" onClose={close} wide>
      <table className="shortcuts">
        <tbody>
          {SHORTCUTS.map(([k, v]) => <tr key={k}><td><kbd>{k}</kbd></td><td>{v}</td></tr>)}
        </tbody>
      </table>
    </Modal>
  );
}

export function Dialogs() {
  const modal = useStore((s) => s.ui.modal);
  if (modal === 'find') return <FindDialog />;
  if (modal === 'video') return <VideoDialog />;
  if (modal === 'image-url') return <ImageUrlDialog />;
  if (modal === 'shortcuts') return <ShortcutsDialog />;
  return null;
}
