import { useEffect, useState } from 'react';
import { createPresentation, SLIDE_SIZES } from './model/factory';
import { THEMES } from './model/themes';
import type { Presentation } from './model/types';
import { SlideThumb } from './render/SlideView';
import { deleteDoc, listDocs, saveDoc, type DocMeta } from './io/storage';
import { FILE_EXT, importJsonFile, pickFile } from './io/file';
import { Icon } from './ui/Icon';

export function Home({ onOpen }: { onOpen: (id: string) => void }) {
  const [docs, setDocs] = useState<DocMeta[] | null>(null);
  const [query, setQuery] = useState('');
  const refresh = () => { listDocs().then(setDocs, () => setDocs([])); };
  useEffect(refresh, []);
  useEffect(() => { document.title = 'Visary PRSNT'; }, []);

  const create = async (p: Presentation) => {
    await saveDoc(p);
    onOpen(p.id);
  };

  return (
    <div className="home">
      <header className="home-top">
        <div className="logo"><span className="logo-mark">V</span><span className="logo-text">Visary PRSNT</span></div>
        <span className="grow" />
        <button className="btn" onClick={async () => {
          const f = await pickFile(`${FILE_EXT},.json,application/json`);
          if (!f) return;
          try {
            await create(await importJsonFile(f));
          } catch (err) {
            window.alert((err as Error).message || 'Could not open this file.');
          }
        }}><Icon name="folder" size={16} /> Open file</button>
      </header>

      <section className="home-section">
        <h2>Start a new presentation</h2>
        <div className="new-grid">
          {SLIDE_SIZES.map((s) => (
            <button key={s.id} className="new-card" onClick={() => create(createPresentation({ width: s.width, height: s.height }))}>
              <div className="new-shape" style={{ aspectRatio: `${s.width} / ${s.height}` }}><Icon name="plus" size={22} /></div>
              <span>{s.label}</span>
            </button>
          ))}
        </div>
      </section>

      <section className="home-section">
        <h2>Templates</h2>
        <div className="tpl-grid">
          {THEMES.map((t) => {
            const p = createPresentation({ themeId: t.id, sample: true, title: `${t.name} deck` });
            return (
              <button key={t.id} className="tpl-card" onClick={() => create(createPresentation({ themeId: t.id, sample: true, title: `${t.name} deck` }))}>
                <SlideThumb slide={p.slides[0]} W={p.width} H={p.height} width={240} />
                <span>{t.name}</span>
              </button>
            );
          })}
        </div>
        <p className="hint">Each template is a five slide starter deck with layouts, a chart, transitions and animations you can change.</p>
      </section>

      <section className="home-section">
        <div className="home-row">
          <h2>Recent presentations</h2>
          <input className="search" placeholder="Search" value={query} onChange={(e) => setQuery(e.target.value)} aria-label="Search presentations" />
        </div>
        {docs === null && <p className="hint">Loading...</p>}
        {docs && docs.length === 0 && <p className="hint">Nothing yet. Your presentations are saved automatically in this browser.</p>}
        <div className="tpl-grid">
          {docs?.filter((d) => d.title.toLowerCase().includes(query.toLowerCase())).map((d) => (
            <div key={d.id} className="tpl-card recent">
              <button className="recent-open" onClick={() => onOpen(d.id)}>
                {d.pres.slides[0] ? <SlideThumb slide={d.pres.slides[0]} W={d.pres.width} H={d.pres.height} width={240} /> : <div className="empty-thumb" />}
                <span className="recent-title">{d.title}</span>
                <small>{new Date(d.updatedAt).toLocaleString()} . {d.pres.slides.length} slides</small>
              </button>
              <button className="recent-del" title="Delete" aria-label={`Delete ${d.title}`} onClick={async () => {
                if (!window.confirm(`Delete "${d.title}"? This cannot be undone.`)) return;
                await deleteDoc(d.id);
                refresh();
              }}><Icon name="trash" size={16} /></button>
            </div>
          ))}
        </div>
      </section>
      <footer className="home-foot">
        <span>Presentations are saved in this browser. Use File, Save a copy to keep a backup, or export to PowerPoint and PDF.</span>
      </footer>
    </div>
  );
}
