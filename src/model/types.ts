export type Id = string;

export type FillType = 'none' | 'solid' | 'linear' | 'radial';

export interface Fill {
  type: FillType;
  color: string;
  color2?: string;
  /** CSS gradient angle in degrees (0 = to top, 90 = to right). */
  angle?: number;
}

export interface Stroke {
  color: string;
  width: number;
  dash: 'solid' | 'dashed' | 'dotted';
}

export interface Shadow {
  enabled: boolean;
  x: number;
  y: number;
  blur: number;
  color: string;
}

export interface TextStyle {
  fontFamily: string;
  fontSize: number;
  color: string;
  bold: boolean;
  italic: boolean;
  underline: boolean;
  strike: boolean;
  align: 'left' | 'center' | 'right' | 'justify';
  vAlign: 'top' | 'middle' | 'bottom';
  lineHeight: number;
  letterSpacing: number;
}

export type Link = { kind: 'url'; url: string } | { kind: 'slide'; slideId: Id };

export interface BaseElement {
  id: Id;
  name?: string;
  x: number;
  y: number;
  w: number;
  h: number;
  rot: number;
  opacity: number;
  locked?: boolean;
  groupId?: Id;
  flipH?: boolean;
  flipV?: boolean;
  shadow?: Shadow;
  link?: Link;
}

export type TextRole = 'title' | 'subtitle' | 'body' | 'caption';

export interface TextElement extends BaseElement {
  type: 'text';
  html: string;
  style: TextStyle;
  fill: Fill;
  stroke: Stroke;
  padding: number;
  role?: TextRole;
  radius?: number;
}

export type ShapeKind =
  | 'rect' | 'roundRect' | 'ellipse' | 'triangle' | 'rightTriangle' | 'diamond'
  | 'pentagon' | 'hexagon' | 'octagon' | 'parallelogram' | 'trapezoid'
  | 'star4' | 'star5' | 'star6' | 'star8' | 'arrowRight' | 'arrowLeft' | 'arrowUp'
  | 'arrowDown' | 'arrowDouble' | 'chevron' | 'heart' | 'callout' | 'cloud'
  | 'plus' | 'lightning' | 'moon' | 'donut' | 'frame' | 'cylinder' | 'line'
  | 'arrowLine' | 'blockArc' | 'wave';

export interface ShapeElement extends BaseElement {
  type: 'shape';
  shape: ShapeKind;
  fill: Fill;
  stroke: Stroke;
  radius: number;
  html: string;
  style: TextStyle;
}

export interface ImageFilters {
  brightness: number;
  contrast: number;
  saturate: number;
  grayscale: number;
  sepia: number;
  blur: number;
  hue: number;
}

export interface ImageElement extends BaseElement {
  type: 'image';
  src: string;
  fit: 'cover' | 'contain' | 'fill';
  radius: number;
  stroke: Stroke;
  filters: ImageFilters;
}

export interface TableElement extends BaseElement {
  type: 'table';
  rows: string[][];
  style: TextStyle;
  headerRow: boolean;
  bandedRows: boolean;
  borderColor: string;
  headerFill: string;
  headerColor: string;
  cellFill: string;
  bandFill: string;
}

export type ChartType = 'column' | 'bar' | 'line' | 'area' | 'pie' | 'doughnut';

export interface ChartSeries {
  name: string;
  values: number[];
  color: string;
}

export interface ChartElement extends BaseElement {
  type: 'chart';
  chartType: ChartType;
  title: string;
  labels: string[];
  series: ChartSeries[];
  showLegend: boolean;
  showValues: boolean;
  showGrid: boolean;
  fontFamily: string;
  color: string;
}

export interface VideoElement extends BaseElement {
  type: 'video';
  src: string;
  autoplay: boolean;
  loop: boolean;
  muted: boolean;
}

export type SlideElement =
  | TextElement
  | ShapeElement
  | ImageElement
  | TableElement
  | ChartElement
  | VideoElement;

export type ElementType = SlideElement['type'];

export interface Background {
  fill: Fill;
  image?: string;
  imageFit?: 'cover' | 'contain';
  /** Darkening overlay 0..1 applied over a background image. */
  overlay?: number;
}

export interface Transition {
  type: string;
  duration: number;
  direction?: string;
  easing: string;
  advanceOnClick: boolean;
  /** Seconds before auto advancing, null for never. */
  advanceAfter: number | null;
}

export type AnimCategory = 'entrance' | 'emphasis' | 'exit' | 'motion';
export type AnimTrigger = 'click' | 'with' | 'after';

export interface Animation {
  id: Id;
  elementId: Id;
  category: AnimCategory;
  effect: string;
  trigger: AnimTrigger;
  delay: number;
  duration: number;
  direction?: string;
  repeat: number;
  easing?: string;
}

export interface Slide {
  id: Id;
  elements: SlideElement[];
  background: Background;
  notes: string;
  transition: Transition;
  animations: Animation[];
  hidden?: boolean;
  layout?: string;
}

export interface ThemeColors {
  background: string;
  background2: string;
  text: string;
  textMuted: string;
  accents: string[];
}

export interface Theme {
  id: string;
  name: string;
  colors: ThemeColors;
  headingFont: string;
  bodyFont: string;
  background: Background;
  titleBackground?: Background;
}

export interface PresentationSettings {
  loop: boolean;
  showSlideNumbers: boolean;
}

export interface Presentation {
  id: Id;
  title: string;
  width: number;
  height: number;
  themeId: string;
  slides: Slide[];
  settings: PresentationSettings;
  createdAt: number;
  updatedAt: number;
}
