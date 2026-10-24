import { useEffect, useState } from 'react';
import type { Presentation } from './model/types';
import { loadDoc } from './io/storage';
import { initStore, useStore } from './store/store';
import { Editor } from './editor/Editor';
import { Home } from './Home';
import { PresenterView } from './present/PresenterView';

type Route = { kind: 'home' } | { kind: 'doc'; id: string } | { kind: 'presenter'; id: string };

function readRoute(): Route {
  const q = new URLSearchParams(location.search);
  const presenter = q.get('presenter');
  if (presenter) return { kind: 'presenter', id: presenter };
  const doc = q.get('doc');
  return doc ? { kind: 'doc', id: doc } : { kind: 'home' };
}

function navigate(route: Route) {
  const url = route.kind === 'home' ? location.pathname : `${location.pathname}?doc=${encodeURIComponent(route.id)}`;
  history.pushState(null, '', url);
}

export function App() {
  const [route, setRoute] = useState<Route>(readRoute);
  const [loaded, setLoaded] = useState<Presentation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const storeId = useStore((s) => (s ? s.pres.id : null));

  useEffect(() => {
    const onPop = () => setRoute(readRoute());
    window.addEventListener('popstate', onPop);
    return () => window.removeEventListener('popstate', onPop);
  }, []);

  useEffect(() => {
    if (route.kind === 'home') { setLoaded(null); return; }
    setError(null);
    loadDoc(route.id).then((p) => {
      if (!p) { setError('This presentation was not found in this browser.'); return; }
      if (route.kind === 'doc') initStore(p);
      setLoaded(p);
    }, () => setError('Could not open browser storage.'));
  }, [route]);

  const open = (id: string) => { navigate({ kind: 'doc', id }); setRoute({ kind: 'doc', id }); };
  const home = () => { navigate({ kind: 'home' }); setRoute({ kind: 'home' }); };

  if (error) {
    return (
      <div className="center-msg">
        <p>{error}</p>
        <button className="btn primary" onClick={home}>Go to my presentations</button>
      </div>
    );
  }
  if (route.kind === 'home') return <Home onOpen={open} />;
  if (!loaded || loaded.id !== route.id) return <div className="center-msg">Loading...</div>;
  if (route.kind === 'presenter') return <PresenterView pres={loaded} />;
  if (storeId !== route.id) return <div className="center-msg">Loading...</div>;
  return <Editor key={route.id} onHome={home} onOpen={open} />;
}
