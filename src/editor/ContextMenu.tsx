import { useLayoutEffect, useRef, useState } from 'react';
import { getState, setUI } from '../store/store';
import {
  addSlide, arrange, copySelected, cutSelected, deleteSelected, duplicateSelected, groupSelected,
  hasClipboard, paste, pickFormat, selectAll, toggleLock, ungroupSelected,
} from './actions';
import { useOutside } from '../ui/controls';

interface Item {
  label: string;
  hint?: string;
  run: () => void;
  disabled?: boolean;
}

export function ContextMenu({ x, y, onClose }: { x: number; y: number; onClose: () => void }) {
  const ref = useRef<HTMLDivElement>(null);
  const [pos, setPos] = useState({ x, y });
  useOutside(ref, onClose);
  useLayoutEffect(() => {
    const r = ref.current?.getBoundingClientRect();
    if (!r) return;
    setPos({ x: Math.min(x, window.innerWidth - r.width - 8), y: Math.min(y, window.innerHeight - r.height - 8) });
  }, [x, y]);

  const sel = getState().ui.selection;
  const has = sel.length > 0;
  const groups: Item[][] = has
    ? [
      [
        { label: 'Cut', hint: 'Ctrl+X', run: cutSelected },
        { label: 'Copy', hint: 'Ctrl+C', run: copySelected },
        { label: 'Paste', hint: 'Ctrl+V', run: () => paste(), disabled: !hasClipboard() },
        { label: 'Duplicate', hint: 'Ctrl+D', run: duplicateSelected },
        { label: 'Delete', hint: 'Del', run: deleteSelected },
      ],
      [
        { label: 'Bring to front', hint: 'Ctrl+Shift+]', run: () => arrange('front') },
        { label: 'Bring forward', hint: 'Ctrl+]', run: () => arrange('forward') },
        { label: 'Send backward', hint: 'Ctrl+[', run: () => arrange('backward') },
        { label: 'Send to back', hint: 'Ctrl+Shift+[', run: () => arrange('back') },
      ],
      [
        { label: 'Group', hint: 'Ctrl+G', run: groupSelected, disabled: sel.length < 2 },
        { label: 'Ungroup', hint: 'Ctrl+Shift+G', run: ungroupSelected },
        { label: 'Lock or unlock', run: toggleLock },
        { label: 'Copy format', run: pickFormat },
      ],
      [
        { label: 'Add animation', run: () => setUI({ panel: 'animations', tab: 'animations' }) },
        { label: 'Format options', run: () => setUI({ panel: 'format' }) },
      ],
    ]
    : [
      [
        { label: 'Paste', hint: 'Ctrl+V', run: () => paste(), disabled: !hasClipboard() },
        { label: 'Select all', hint: 'Ctrl+A', run: selectAll },
      ],
      [
        { label: 'New slide', hint: 'Ctrl+M', run: () => addSlide() },
        { label: 'Format background', run: () => setUI({ panel: 'design', tab: 'design' }) },
        { label: 'Slide transition', run: () => setUI({ panel: 'transitions', tab: 'transitions' }) },
      ],
    ];

  return (
    <div className="context-menu popover" ref={ref} style={{ left: pos.x, top: pos.y }} role="menu">
      {groups.map((g, i) => (
        <div key={i} className="menu-group">
          {g.map((item) => (
            <button
              key={item.label}
              role="menuitem"
              className="menu-item"
              disabled={item.disabled}
              onClick={() => { item.run(); onClose(); }}
            >
              <span>{item.label}</span>
              {item.hint && <kbd>{item.hint}</kbd>}
            </button>
          ))}
        </div>
      ))}
    </div>
  );
}
