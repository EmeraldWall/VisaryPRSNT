import type { Theme } from './types';

export const FONTS = [
  'Inter', 'Roboto', 'Open Sans', 'Lato', 'Montserrat', 'Poppins', 'Raleway', 'Oswald',
  'Playfair Display', 'Merriweather', 'Lora', 'Source Code Pro', 'Bebas Neue', 'Pacifico',
  'Arial', 'Helvetica', 'Georgia', 'Times New Roman', 'Verdana', 'Trebuchet MS',
  'Courier New', 'Impact', 'Comic Sans MS',
];

const solid = (color: string) => ({ fill: { type: 'solid' as const, color } });
const linear = (color: string, color2: string, angle: number) => ({
  fill: { type: 'linear' as const, color, color2, angle },
});

export const THEMES: Theme[] = [
  {
    id: 'classic', name: 'Classic White',
    colors: { background: '#ffffff', background2: '#f3f4f6', text: '#1f2937', textMuted: '#6b7280', accents: ['#2563eb', '#16a34a', '#f59e0b', '#dc2626', '#7c3aed', '#0891b2'] },
    headingFont: 'Inter', bodyFont: 'Inter', background: solid('#ffffff'),
  },
  {
    id: 'midnight', name: 'Midnight',
    colors: { background: '#0f172a', background2: '#1e293b', text: '#f8fafc', textMuted: '#94a3b8', accents: ['#38bdf8', '#a78bfa', '#f472b6', '#34d399', '#fbbf24', '#fb7185'] },
    headingFont: 'Montserrat', bodyFont: 'Open Sans', background: solid('#0f172a'),
    titleBackground: linear('#0f172a', '#312e81', 135),
  },
  {
    id: 'ocean', name: 'Ocean',
    colors: { background: '#f0f9ff', background2: '#e0f2fe', text: '#0c4a6e', textMuted: '#0369a1', accents: ['#0284c7', '#06b6d4', '#14b8a6', '#6366f1', '#f97316', '#e11d48'] },
    headingFont: 'Poppins', bodyFont: 'Lato', background: solid('#f0f9ff'),
    titleBackground: linear('#0369a1', '#06b6d4', 135),
  },
  {
    id: 'sunset', name: 'Sunset',
    colors: { background: '#fff7ed', background2: '#ffedd5', text: '#431407', textMuted: '#9a3412', accents: ['#ea580c', '#db2777', '#f59e0b', '#7c2d12', '#be123c', '#a16207'] },
    headingFont: 'Playfair Display', bodyFont: 'Lato', background: solid('#fff7ed'),
    titleBackground: linear('#f97316', '#db2777', 120),
  },
  {
    id: 'forest', name: 'Forest',
    colors: { background: '#f7fee7', background2: '#ecfccb', text: '#1a2e05', textMuted: '#3f6212', accents: ['#4d7c0f', '#15803d', '#a16207', '#0f766e', '#65a30d', '#854d0e'] },
    headingFont: 'Merriweather', bodyFont: 'Open Sans', background: solid('#f7fee7'),
    titleBackground: linear('#14532d', '#4d7c0f', 160),
  },
  {
    id: 'mono', name: 'Minimal Mono',
    colors: { background: '#fafafa', background2: '#e5e5e5', text: '#0a0a0a', textMuted: '#525252', accents: ['#171717', '#404040', '#737373', '#a3a3a3', '#ef4444', '#262626'] },
    headingFont: 'Bebas Neue', bodyFont: 'Roboto', background: solid('#fafafa'),
    titleBackground: solid('#0a0a0a'),
  },
  {
    id: 'neon', name: 'Neon Night',
    colors: { background: '#09090b', background2: '#18181b', text: '#fafafa', textMuted: '#a1a1aa', accents: ['#22d3ee', '#e879f9', '#a3e635', '#facc15', '#fb7185', '#818cf8'] },
    headingFont: 'Oswald', bodyFont: 'Roboto', background: solid('#09090b'),
    titleBackground: linear('#09090b', '#4a044e', 145),
  },
  {
    id: 'paper', name: 'Paper',
    colors: { background: '#fdf6e3', background2: '#eee8d5', text: '#3b3226', textMuted: '#7c6f5b', accents: ['#b58900', '#cb4b16', '#268bd2', '#859900', '#d33682', '#2aa198'] },
    headingFont: 'Lora', bodyFont: 'Merriweather', background: solid('#fdf6e3'),
  },
  {
    id: 'royal', name: 'Royal',
    colors: { background: '#faf5ff', background2: '#f3e8ff', text: '#2e1065', textMuted: '#6b21a8', accents: ['#7e22ce', '#c026d3', '#eab308', '#4338ca', '#be185d', '#0d9488'] },
    headingFont: 'Raleway', bodyFont: 'Raleway', background: solid('#faf5ff'),
    titleBackground: linear('#3b0764', '#7e22ce', 135),
  },
  {
    id: 'coral', name: 'Coral Pop',
    colors: { background: '#fff1f2', background2: '#ffe4e6', text: '#4c0519', textMuted: '#9f1239', accents: ['#f43f5e', '#fb923c', '#facc15', '#2dd4bf', '#8b5cf6', '#0ea5e9'] },
    headingFont: 'Pacifico', bodyFont: 'Poppins', background: solid('#fff1f2'),
    titleBackground: linear('#fb7185', '#fdba74', 90),
  },
];

export function getTheme(id: string): Theme {
  return THEMES.find((t) => t.id === id) ?? THEMES[0];
}

/** Text color that stays readable on a theme's title background. */
export function titleTextColor(theme: Theme): string {
  if (!theme.titleBackground) return theme.colors.text;
  const c = theme.titleBackground.fill.color;
  return isDark(c) ? '#ffffff' : theme.colors.text;
}

export function isDark(hex: string): boolean {
  const m = hex.replace('#', '');
  if (m.length < 6) return false;
  const r = parseInt(m.slice(0, 2), 16);
  const g = parseInt(m.slice(2, 4), 16);
  const b = parseInt(m.slice(4, 6), 16);
  return 0.299 * r + 0.587 * g + 0.114 * b < 140;
}
