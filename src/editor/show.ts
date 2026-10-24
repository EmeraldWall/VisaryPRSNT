import { saveDoc } from '../io/storage';
import { currentIndex, getState, setUI } from '../store/store';

export async function startShow(from: 'start' | 'current', presenterView = false): Promise<void> {
  const { pres } = getState();
  const index = from === 'start' ? 0 : currentIndex();
  // The presenter window reads the presentation from storage, so save first.
  if (presenterView) {
    const win = window.open(`${location.pathname}?presenter=${encodeURIComponent(pres.id)}`, 'visary-presenter', 'width=1280,height=800');
    await saveDoc(pres);
    win?.focus();
  }
  setUI({ presenting: { startIndex: index }, editingId: null });
}
