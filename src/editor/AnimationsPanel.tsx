import { useState } from 'react';
import type { AnimCategory, AnimTrigger } from '../model/types';
import { htmlToText } from '../model/util';
import { currentSlide, setUI, useStore } from '../store/store';
import { effectsFor, getEffect } from '../present/animations';
import { EASINGS } from '../present/transitions';
import { addAnimation, moveAnimation, removeAnimation, updateAnimation } from './actions';
import { IconButton, NumberField, Section, SelectField } from '../ui/controls';
import { Icon } from '../ui/Icon';

const CATS: { id: AnimCategory; label: string; color: string }[] = [
  { id: 'entrance', label: 'Entrance', color: '#16a34a' },
  { id: 'emphasis', label: 'Emphasis', color: '#d97706' },
  { id: 'exit', label: 'Exit', color: '#dc2626' },
  { id: 'motion', label: 'Motion paths', color: '#2563eb' },
];

const TRIGGERS: { value: AnimTrigger; label: string }[] = [
  { value: 'click', label: 'On click' },
  { value: 'with', label: 'With previous' },
  { value: 'after', label: 'After previous' },
];

export function AnimationsPanel() {
  const slide = useStore((s) => currentSlide(s));
  const selection = useStore((s) => s.ui.selection);
  const [cat, setCat] = useState<AnimCategory>('entrance');
  const [openId, setOpenId] = useState<string | null>(null);
  const label = (id: string) => {
    const el = slide.elements.find((e) => e.id === id);
    if (!el) return 'Missing object';
    if (el.name) return el.name;
    if (el.type === 'text' || el.type === 'shape') return htmlToText(el.html).slice(0, 28) || el.type;
    return el.type[0].toUpperCase() + el.type.slice(1);
  };
  let step = 0;

  return (
    <div className="anim-panel">
      <h3 className="panel-title">Animations</h3>
      <div className="tr-current">
        <span className="hint">{selection.length ? `${selection.length} object${selection.length > 1 ? 's' : ''} selected` : 'Select an object to add an effect'}</span>
        <button className="btn primary" onClick={() => setUI((u) => ({ previewAnimations: u.previewAnimations + 1 }))} disabled={!slide.animations.length}><Icon name="play" size={14} /> Preview</button>
      </div>
      <div className="seg">
        {CATS.map((c) => (
          <button key={c.id} className={cat === c.id ? 'on' : ''} onClick={() => setCat(c.id)} style={{ ['--c' as string]: c.color }}>{c.label}</button>
        ))}
      </div>
      <div className="fx-grid">
        {effectsFor(cat).map((e) => (
          <button key={e.id} className="fx-tile" disabled={!selection.length} onClick={() => addAnimation(cat, e.id)} style={{ ['--c' as string]: CATS.find((c) => c.id === cat)?.color }}>
            <Icon name="star" size={16} />
            <span>{e.name}</span>
          </button>
        ))}
      </div>
      <Section title={`Animation pane (${slide.animations.length})`}>
        {!slide.animations.length && <p className="hint">No animations on this slide yet.</p>}
        <ol className="anim-list">
          {slide.animations.map((a, i) => {
            if (a.trigger === 'click' || i === 0) step += a.trigger === 'click' ? 1 : 0;
            const def = getEffect(a.category, a.effect);
            const color = CATS.find((c) => c.id === a.category)?.color;
            const open = openId === a.id;
            return (
              <li key={a.id} className={`anim-item ${open ? 'open' : ''} ${selection.includes(a.elementId) ? 'sel' : ''}`}>
                <div className="anim-row" onClick={() => { setOpenId(open ? null : a.id); setUI({ selection: [a.elementId] }); }}>
                  <span className="anim-step">{a.trigger === 'click' ? step : ''}</span>
                  <span className="anim-dot" style={{ background: color }} />
                  <span className="anim-name"><strong>{def?.name ?? a.effect}</strong> {label(a.elementId)}</span>
                  <IconButton icon="chevronUp" label="Move earlier" onClick={() => moveAnimation(a.id, -1)} />
                  <IconButton icon="chevronDown" label="Move later" onClick={() => moveAnimation(a.id, 1)} />
                  <IconButton icon="trash" label="Remove" onClick={() => removeAnimation(a.id)} />
                </div>
                {open && (
                  <div className="anim-detail">
                    <SelectField label="Start" value={a.trigger} options={TRIGGERS} onChange={(trigger) => updateAnimation(a.id, { trigger })} />
                    <SelectField label="Effect" value={a.effect} options={effectsFor(a.category).map((e) => ({ value: e.id, label: e.name }))} onChange={(effect) => updateAnimation(a.id, { effect, direction: getEffect(a.category, effect)?.directions?.[0]?.value })} />
                    {def?.directions && <SelectField label="Direction" value={a.direction ?? def.directions[0].value} options={def.directions} onChange={(direction) => updateAnimation(a.id, { direction })} />}
                    <div className="row2">
                      <NumberField label="Duration" value={a.duration / 1000} min={0.05} max={30} step={0.1} suffix="s" onChange={(v) => updateAnimation(a.id, { duration: Math.round(v * 1000) })} />
                      <NumberField label="Delay" value={a.delay / 1000} min={0} max={60} step={0.1} suffix="s" onChange={(v) => updateAnimation(a.id, { delay: Math.round(v * 1000) })} />
                      <NumberField label="Repeat" value={a.repeat} min={1} max={100} onChange={(repeat) => updateAnimation(a.id, { repeat: Math.round(repeat) })} />
                      <SelectField label="Easing" value={a.easing ?? def?.easing ?? 'ease-in-out'} options={[...EASINGS.map((e) => ({ value: e.id, label: e.label })), ...(def?.easing && !EASINGS.some((e) => e.id === def.easing) ? [{ value: def.easing, label: 'Effect default' }] : [])]} onChange={(easing) => updateAnimation(a.id, { easing })} />
                    </div>
                  </div>
                )}
              </li>
            );
          })}
        </ol>
      </Section>
    </div>
  );
}
