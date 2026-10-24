import { describe, expect, it } from 'vitest';
import { buildSteps, EFFECTS, initiallyHidden } from '../present/animations';
import { TRANSITIONS } from '../present/transitions';
import { htmlToRuns, slideTransitionXml, transitionXml } from '../io/pptx';
import { normalizePresentation } from '../io/file';
import { createPresentation, defaultTransition } from '../model/factory';
import { sanitizeHtml } from '../model/util';
import { toggleListHtml } from '../editor/text';
import { getState, initStore, redo, undo, update, beginGesture, endGesture } from '../store/store';
import type { Animation } from '../model/types';

const anim = (id: string, trigger: Animation['trigger'], duration = 500, delay = 0): Animation => ({
  id, elementId: `el-${id}`, category: 'entrance', effect: 'fade', trigger, delay, duration, repeat: 1,
});

describe('transitions', () => {
  it('has unique ids and a large library', () => {
    const ids = TRANSITIONS.map((t) => t.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(ids.length).toBeGreaterThanOrEqual(50);
  });

  it('maps every transition and direction to PowerPoint XML', () => {
    for (const t of TRANSITIONS) {
      for (const d of t.directions ?? [{ value: '', label: '' }]) {
        const tr = { ...defaultTransition(), type: t.id, direction: d.value || undefined };
        const res = transitionXml(tr);
        if (t.id === 'none') expect(res).toBeNull();
        else expect(res?.[1]).toMatch(/^<p(14|15|159)?:[a-zA-Z]+[^>]*\/>$/);
        const xml = slideTransitionXml({ ...tr, advanceAfter: 3 });
        const doc = new DOMParser().parseFromString(`<root xmlns:p="urn:p">${xml}</root>`, 'application/xml');
        expect(doc.getElementsByTagName('parsererror').length).toBe(0);
      }
    }
  });
});

describe('animation steps', () => {
  it('groups click, with and after triggers', () => {
    const steps = buildSteps([anim('a', 'after', 400), anim('b', 'click'), anim('c', 'with', 300, 100), anim('d', 'after', 200)]);
    expect(steps).toHaveLength(2);
    expect(steps[0].auto).toBe(true);
    expect(steps[1].auto).toBe(false);
    expect(steps[1].items.map((i) => i.start)).toEqual([0, 0, 500]);
  });

  it('hides elements whose first effect is an entrance', () => {
    const p = createPresentation({ sample: true });
    const s = p.slides[1];
    expect(initiallyHidden(s).has(s.animations[0].elementId)).toBe(true);
  });

  it('exit effects mirror entrances', () => {
    const fly = EFFECTS.find((e) => e.category === 'exit' && e.id === 'fly')!;
    const frames = fly.build({ dir: 'bottom', box: { x: 0, y: 0, w: 10, h: 10, rot: 0 }, W: 100, H: 100 });
    expect(frames[0].transform).toBe('none');
  });
});

describe('pptx text', () => {
  it('turns html into runs with bullets and breaks', () => {
    const runs = htmlToRuns('<div><b>Bold</b> text</div><ul><li>One</li><li>Two</li></ul>', (px) => px / 2);
    expect(runs.map((r) => r.text)).toEqual(['Bold', ' text', 'One', 'Two']);
    expect(runs[0].options.bold).toBe(true);
    expect(runs[1].options.breakLine).toBe(true);
    expect(runs[2].options.bullet).toBe(true);
    expect(runs[3].options.breakLine).toBeUndefined();
  });
});

describe('file safety', () => {
  it('strips scripts and handlers from imported html', () => {
    const clean = sanitizeHtml('<b onclick="x()">hi</b><script>alert(1)</script><img src=x onerror=alert(1)>');
    expect(clean).toBe('<b>hi</b>');
  });

  it('rejects non presentations and repairs missing fields', () => {
    expect(() => normalizePresentation({ hello: 1 })).toThrow();
    const p = normalizePresentation({ slides: [{ elements: [{ type: 'text', html: '<a href="javascript:x">x</a>', x: 1 }] }] });
    expect(p.slides[0].elements[0].type).toBe('text');
    expect(JSON.stringify(p)).not.toContain('javascript:');
    expect(p.slides[0].transition.type).toBe('none');
  });
});

describe('lists', () => {
  it('toggles paragraphs into a list and back', () => {
    const ul = toggleListHtml('<div>A</div><div>B</div>', 'ul');
    expect(ul).toBe('<ul><li>A</li><li>B</li></ul>');
    expect(toggleListHtml(ul, 'ul')).toBe('<div>A</div><div>B</div>');
  });
});

describe('store history', () => {
  it('undoes, redoes and merges gestures', () => {
    initStore(createPresentation());
    update((d) => { d.title = 'One'; });
    update((d) => { d.title = 'Two'; });
    undo();
    expect(getState().pres.title).toBe('One');
    redo();
    expect(getState().pres.title).toBe('Two');
    beginGesture();
    for (let i = 0; i < 5; i++) update((d) => { d.title = `Drag ${i}`; }, { history: false });
    endGesture();
    undo();
    expect(getState().pres.title).toBe('Two');
  });
});
