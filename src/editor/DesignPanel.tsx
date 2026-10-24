import { THEMES } from '../model/themes';
import { SLIDE_SIZES } from '../model/factory';
import { update, useStore } from '../store/store';
import { applyTheme, resizeSlides } from './actions';
import { BackgroundEditor } from './Inspector';
import { Section, Toggle } from '../ui/controls';

export function DesignPanel() {
  const pres = useStore((s) => s.pres);
  return (
    <div className="design-panel">
      <h3 className="panel-title">Design</h3>
      <Section title="Themes">
        <div className="theme-grid">
          {THEMES.map((t) => {
            const bg = t.titleBackground ?? t.background;
            const fill = bg.fill.type === 'solid' ? bg.fill.color : `linear-gradient(${bg.fill.angle ?? 135}deg, ${bg.fill.color}, ${bg.fill.color2 ?? bg.fill.color})`;
            return (
              <button key={t.id} className={`theme-card ${pres.themeId === t.id ? 'on' : ''}`} onClick={() => applyTheme(t.id)}>
                <div className="theme-prev" style={{ background: fill }}>
                  <span style={{ fontFamily: t.headingFont, color: t.titleBackground ? '#fff' : t.colors.text }}>Title</span>
                  <div className="theme-dots">{t.colors.accents.slice(0, 4).map((c) => <i key={c} style={{ background: c }} />)}</div>
                </div>
                <span>{t.name}</span>
              </button>
            );
          })}
        </div>
      </Section>
      <BackgroundEditor />
      <Section title="Slide size">
        <div className="menu-list">
          {SLIDE_SIZES.map((s) => (
            <button key={s.id} className={`menu-item ${pres.width === s.width && pres.height === s.height ? 'on' : ''}`} onClick={() => resizeSlides(s.width, s.height)}>
              {s.label} <small>{s.width} x {s.height}</small>
            </button>
          ))}
        </div>
      </Section>
      <Section title="Header and footer">
        <Toggle label="Show slide numbers" checked={pres.settings.showSlideNumbers} onChange={(v) => update((d) => { d.settings.showSlideNumbers = v; })} />
      </Section>
    </div>
  );
}
