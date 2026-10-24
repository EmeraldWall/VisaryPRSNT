import { useRef } from 'react';
import { getTheme } from '../model/themes';
import { currentSlide, setUI, useStore } from '../store/store';
import {
  EASINGS, TRANSITIONS, TRANSITION_CATEGORIES, createController, getTransition, runTransition, type Controller,
} from '../present/transitions';
import { applyTransitionToAll, setTransition } from './actions';
import { NumberField, Section, SelectField, Toggle } from '../ui/controls';
import { Icon } from '../ui/Icon';

const MW = 128, MH = 72;

function TransitionTile({ id, name, active, accent, onPick }: { id: string; name: string; active: boolean; accent: string; onPick: () => void }) {
  const from = useRef<HTMLDivElement>(null);
  const to = useRef<HTMLDivElement>(null);
  const fx = useRef<HTMLDivElement>(null);
  const ctl = useRef<Controller | null>(null);
  const play = () => {
    if (!from.current || !to.current || !fx.current || id === 'none') return;
    ctl.current?.skip();
    const c = createController();
    ctl.current = c;
    void runTransition({
      from: from.current, to: to.current, fx: fx.current, W: MW, H: MH, duration: 900, dir: '', easing: 'ease-in-out',
      accent, ctl: c, cleanups: [],
    }, id === 'morph' ? 'fade' : id);
  };
  return (
    <button className={`tr-tile ${active ? 'on' : ''}`} onClick={onPick} onMouseEnter={play} title={name}>
      <div className="tr-mini" style={{ width: MW, height: MH }}>
        <div className="tr-layer mini-a" ref={from} style={{ zIndex: 1 }}><div className="slide-content"><span>A</span></div></div>
        <div className="tr-layer mini-b" ref={to} style={{ zIndex: 2, background: accent }}><div className="slide-content"><span>B</span></div></div>
        <div className="tr-fx" ref={fx} />
      </div>
      <span className="tr-name">{name}</span>
    </button>
  );
}

export function TransitionsPanel() {
  const slide = useStore((s) => currentSlide(s));
  const themeId = useStore((s) => s.pres.themeId);
  const accent = getTheme(themeId).colors.accents[0];
  const t = slide.transition;
  const def = getTransition(t.type);
  const preview = () => setUI((u) => ({ previewTransition: u.previewTransition + 1 }));

  return (
    <div className="tr-panel">
      <h3 className="panel-title">Transitions</h3>
      <div className="tr-current">
        <div>
          <div className="field-label">Applied to this slide</div>
          <strong>{def.name}</strong>
        </div>
        <button className="btn primary" onClick={preview} disabled={t.type === 'none'}><Icon name="play" size={14} /> Preview</button>
      </div>
      <Section title="Effect options">
        {def.directions && (
          <SelectField label="Direction or variant" value={t.direction ?? def.directions[0].value} options={def.directions} onChange={(direction) => { setTransition({ direction }); preview(); }} />
        )}
        <div className="row2">
          <NumberField label="Duration" value={t.duration / 1000} min={0.1} max={10} step={0.1} suffix="s" onChange={(v) => setTransition({ duration: Math.round(v * 1000) })} />
          <SelectField label="Easing" value={t.easing} options={EASINGS.map((e) => ({ value: e.id, label: e.label }))} onChange={(easing) => setTransition({ easing })} />
        </div>
      </Section>
      <Section title="Timing">
        <Toggle label="Advance on mouse click" checked={t.advanceOnClick} onChange={(advanceOnClick) => setTransition({ advanceOnClick })} />
        <Toggle label="Advance automatically" checked={t.advanceAfter !== null} onChange={(on) => setTransition({ advanceAfter: on ? 5 : null })} />
        {t.advanceAfter !== null && (
          <NumberField label="After" value={t.advanceAfter} min={0} max={3600} step={0.5} suffix="s" onChange={(advanceAfter) => setTransition({ advanceAfter })} />
        )}
        <button className="btn wide" onClick={applyTransitionToAll}>Apply to all slides</button>
      </Section>
      {TRANSITION_CATEGORIES.map((cat) => (
        <Section key={cat} title={cat}>
          <div className="tr-grid">
            {TRANSITIONS.filter((x) => x.category === cat).map((x) => (
              <TransitionTile
                key={x.id}
                id={x.id}
                name={x.name}
                accent={accent}
                active={t.type === x.id}
                onPick={() => {
                  setTransition({ type: x.id, direction: x.directions?.[0]?.value });
                  if (x.id !== 'none') preview();
                }}
              />
            ))}
          </div>
        </Section>
      ))}
      <p className="hint">Tip: Morph animates objects that appear on both slides. Duplicate a slide, move or resize things, then apply Morph to the second slide.</p>
    </div>
  );
}
