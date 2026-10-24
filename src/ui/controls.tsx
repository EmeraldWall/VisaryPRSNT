import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Icon } from './Icon';

export function IconButton({ icon, label, onClick, active, disabled, showLabel, className }: {
  icon: string; label: string; onClick?: () => void; active?: boolean; disabled?: boolean; showLabel?: boolean; className?: string;
}) {
  return (
    <button
      type="button"
      className={`icon-btn ${active ? 'active' : ''} ${showLabel ? 'with-label' : ''} ${className ?? ''}`}
      title={label}
      aria-label={label}
      aria-pressed={active}
      disabled={disabled}
      onMouseDown={(e) => e.preventDefault()}
      onClick={onClick}
    >
      <Icon name={icon} />
      {showLabel && <span>{label}</span>}
    </button>
  );
}

export function NumberField({ label, value, onChange, min, max, step = 1, suffix, width }: {
  label?: string; value: number; onChange: (v: number) => void; min?: number; max?: number; step?: number; suffix?: string; width?: number;
}) {
  const [text, setText] = useState(String(round(value)));
  const focused = useRef(false);
  useEffect(() => {
    if (!focused.current) setText(String(round(value)));
  }, [value]);
  const commit = (raw: string) => {
    const v = Number(raw);
    if (raw.trim() === '' || !Number.isFinite(v)) return;
    let c = v;
    if (min !== undefined) c = Math.max(min, c);
    if (max !== undefined) c = Math.min(max, c);
    onChange(c);
  };
  return (
    <label className="field num-field" style={{ width }}>
      {label && <span className="field-label">{label}</span>}
      <span className="num-wrap">
        <input
          type="number"
          value={text}
          step={step}
          min={min}
          max={max}
          onFocus={() => { focused.current = true; }}
          onBlur={() => { focused.current = false; setText(String(round(value))); }}
          onChange={(e) => { setText(e.target.value); commit(e.target.value); }}
        />
        {suffix && <span className="suffix">{suffix}</span>}
      </span>
    </label>
  );
}

const round = (v: number) => Math.round(v * 100) / 100;

export function RangeField({ label, value, onChange, min, max, step = 1, suffix }: {
  label: string; value: number; onChange: (v: number) => void; min: number; max: number; step?: number; suffix?: string;
}) {
  return (
    <label className="field range-field">
      <span className="field-label">{label}<em>{round(value)}{suffix}</em></span>
      <input type="range" min={min} max={max} step={step} value={value} onChange={(e) => onChange(Number(e.target.value))} />
    </label>
  );
}

const SWATCHES = [
  '#000000', '#404040', '#7f7f7f', '#bfbfbf', '#ffffff', '#dc2626', '#ea580c', '#f59e0b', '#eab308', '#84cc16',
  '#16a34a', '#14b8a6', '#06b6d4', '#2563eb', '#4f46e5', '#7c3aed', '#c026d3', '#db2777', '#78350f', '#0f172a',
];

export function ColorField({ label, value, onChange, extra }: { label?: string; value: string; onChange: (v: string) => void; extra?: string[] }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useOutside(ref, () => setOpen(false), open);
  const hex = toHex(value);
  return (
    <div className="field color-field" ref={ref}>
      {label && <span className="field-label">{label}</span>}
      <button type="button" className="color-btn" onClick={() => setOpen((o) => !o)} aria-label={`${label ?? 'Colour'} ${hex}`}>
        <span className="color-chip" style={{ background: value }} />
        <span className="color-hex">{hex}</span>
      </button>
      {open && (
        <div className="popover color-pop">
          {extra && extra.length > 0 && (
            <>
              <div className="pop-title">Theme colours</div>
              <div className="swatches">
                {extra.map((c, i) => <button key={`${c}${i}`} type="button" className="swatch" style={{ background: c }} onClick={() => { onChange(c); setOpen(false); }} aria-label={c} />)}
              </div>
            </>
          )}
          <div className="pop-title">Standard colours</div>
          <div className="swatches">
            {SWATCHES.map((c) => <button key={c} type="button" className="swatch" style={{ background: c }} onClick={() => { onChange(c); setOpen(false); }} aria-label={c} />)}
          </div>
          <div className="color-custom">
            <input type="color" value={hex} onChange={(e) => onChange(e.target.value)} aria-label="Custom colour" />
            <input type="text" value={hex} onChange={(e) => { if (/^#[0-9a-f]{6}$/i.test(e.target.value)) onChange(e.target.value); }} aria-label="Hex colour" />
          </div>
        </div>
      )}
    </div>
  );
}

export function toHex(c: string): string {
  if (/^#[0-9a-f]{6}$/i.test(c)) return c.toLowerCase();
  if (/^#[0-9a-f]{8}$/i.test(c)) return c.slice(0, 7).toLowerCase();
  if (/^#[0-9a-f]{3}$/i.test(c)) return '#' + c.slice(1).split('').map((x) => x + x).join('').toLowerCase();
  const m = c.match(/rgba?\((\d+)[,\s]+(\d+)[,\s]+(\d+)/);
  if (m) return '#' + [m[1], m[2], m[3]].map((v) => Number(v).toString(16).padStart(2, '0')).join('');
  return '#000000';
}

export function SelectField<T extends string>({ label, value, options, onChange, width }: {
  label?: string; value: T; options: { value: T; label: string }[]; onChange: (v: T) => void; width?: number;
}) {
  return (
    <label className="field select-field" style={{ width }}>
      {label && <span className="field-label">{label}</span>}
      <select value={value} onChange={(e) => onChange(e.target.value as T)}>
        {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    </label>
  );
}

export function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="toggle">
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} />
      <span>{label}</span>
    </label>
  );
}

export function Section({ title, children, defaultOpen = true }: { title: string; children: ReactNode; defaultOpen?: boolean }) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <section className={`section ${open ? 'open' : ''}`}>
      <button type="button" className="section-head" onClick={() => setOpen((o) => !o)} aria-expanded={open}>
        <span>{title}</span>
        <Icon name={open ? 'chevronUp' : 'chevronDown'} size={16} />
      </button>
      {open && <div className="section-body">{children}</div>}
    </section>
  );
}

export function useOutside(ref: React.RefObject<HTMLElement>, fn: () => void, active = true): void {
  useEffect(() => {
    if (!active) return;
    const onDown = (e: PointerEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) fn();
    };
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') fn(); };
    document.addEventListener('pointerdown', onDown, true);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onDown, true);
      document.removeEventListener('keydown', onKey);
    };
  }, [ref, fn, active]);
}

/** A button that opens a floating panel. The panel closes on outside click or when `close` is called. */
export function Dropdown({ label, icon, children, className, title, showLabel = true }: {
  label: string; icon?: string; children: (close: () => void) => ReactNode; className?: string; title?: string; showLabel?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useOutside(ref, () => setOpen(false), open);
  return (
    <div className={`dropdown ${className ?? ''}`} ref={ref}>
      <button
        type="button"
        className={`icon-btn ${showLabel ? 'with-label' : ''} ${open ? 'active' : ''}`}
        onMouseDown={(e) => e.preventDefault()}
        onClick={() => setOpen((o) => !o)}
        title={title ?? label}
        aria-label={title ?? label}
        aria-expanded={open}
      >
        {icon && <Icon name={icon} />}
        {showLabel && <span>{label}</span>}
        <Icon name="chevronDown" size={14} className="caret" />
      </button>
      {open && <div className="popover dropdown-pop" onMouseDown={(e) => { if ((e.target as HTMLElement).tagName !== 'INPUT') e.preventDefault(); }}>{children(() => setOpen(false))}</div>}
    </div>
  );
}

export function Modal({ title, onClose, children, wide }: { title: string; onClose: () => void; children: ReactNode; wide?: boolean }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);
  return (
    <div className="modal-backdrop" onPointerDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div className={`modal ${wide ? 'wide' : ''}`} role="dialog" aria-modal="true" aria-label={title}>
        <header className="modal-head">
          <h2>{title}</h2>
          <IconButton icon="close" label="Close" onClick={onClose} />
        </header>
        <div className="modal-body">{children}</div>
      </div>
    </div>
  );
}
