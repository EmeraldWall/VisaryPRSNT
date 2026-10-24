import type { ChartElement } from '../model/types';

const fmt = (v: number) => (Math.abs(v) >= 1000 ? `${Math.round(v / 100) / 10}k` : String(Math.round(v * 100) / 100));

function niceMax(v: number): number {
  if (v <= 0) return 1;
  const p = 10 ** Math.floor(Math.log10(v));
  const n = v / p;
  return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * p;
}

export function ChartView({ el }: { el: ChartElement }) {
  const { w, h } = el;
  const fs = Math.max(12, Math.min(w, h) * 0.045);
  const titleH = el.title ? fs * 2 : 0;
  const legendH = el.showLegend ? fs * 2 : 0;
  const font = `"${el.fontFamily}", system-ui, sans-serif`;
  const plotTop = titleH + fs * 0.5;
  const plotBottom = h - legendH - fs * 1.8;

  const legend = el.showLegend && (
    <g transform={`translate(0 ${h - legendH * 0.35})`}>
      {(el.chartType === 'pie' || el.chartType === 'doughnut'
        ? el.labels.map((l, i) => ({ name: l, color: pieColor(el, i) }))
        : el.series.map((s) => ({ name: s.name, color: s.color }))
      ).map((item, i, arr) => {
        const slot = w / arr.length;
        return (
          <g key={i} transform={`translate(${slot * i + slot / 2 - fs * 2} 0)`}>
            <rect x={0} y={-fs * 0.75} width={fs * 0.8} height={fs * 0.8} rx={fs * 0.15} fill={item.color} />
            <text x={fs * 1.1} y={0} fontSize={fs * 0.9} fill={el.color}>{item.name}</text>
          </g>
        );
      })}
    </g>
  );

  let body: JSX.Element;
  if (el.chartType === 'pie' || el.chartType === 'doughnut') {
    const values = el.series[0]?.values ?? [];
    const total = values.reduce((a, b) => a + Math.max(0, b), 0) || 1;
    const cx = w / 2;
    const cy = (plotTop + h - legendH) / 2;
    const rad = Math.max(10, Math.min(w, h - legendH - plotTop) / 2 - fs);
    let a0 = -Math.PI / 2;
    body = (
      <g>
        {values.map((v, i) => {
          const frac = Math.max(0, v) / total;
          const a1 = a0 + frac * Math.PI * 2;
          const large = a1 - a0 > Math.PI ? 1 : 0;
          const d = frac >= 0.9999
            ? `M${cx - rad} ${cy} A${rad} ${rad} 0 1 1 ${cx + rad} ${cy} A${rad} ${rad} 0 1 1 ${cx - rad} ${cy} Z`
            : `M${cx} ${cy} L${cx + rad * Math.cos(a0)} ${cy + rad * Math.sin(a0)} A${rad} ${rad} 0 ${large} 1 ${cx + rad * Math.cos(a1)} ${cy + rad * Math.sin(a1)} Z`;
          const mid = (a0 + a1) / 2;
          const label = el.showValues && frac > 0.03 && (
            <text x={cx + rad * 0.7 * Math.cos(mid)} y={cy + rad * 0.7 * Math.sin(mid)} fontSize={fs} fill="#fff" textAnchor="middle" dominantBaseline="middle">
              {Math.round(frac * 100)}%
            </text>
          );
          a0 = a1;
          return <g key={i}><path d={d} fill={pieColor(el, i)} stroke="#fff" strokeWidth={2} />{label}</g>;
        })}
        {el.chartType === 'doughnut' && <circle cx={cx} cy={cy} r={rad * 0.55} fill="var(--chart-hole, #fff)" />}
      </g>
    );
  } else {
    const all = el.series.flatMap((s) => s.values);
    const max = niceMax(Math.max(0, ...all));
    const horizontal = el.chartType === 'bar';
    const left = fs * 3.2;
    const right = w - fs;
    const n = Math.max(1, el.labels.length);
    const ticks = 5;
    const grid: JSX.Element[] = [];
    for (let i = 0; i <= ticks; i++) {
      const v = (max / ticks) * i;
      if (horizontal) {
        const x = left + ((right - left) * i) / ticks;
        grid.push(
          <g key={i}>
            {el.showGrid && <line x1={x} x2={x} y1={plotTop} y2={plotBottom} stroke={el.color} strokeOpacity={0.15} />}
            <text x={x} y={plotBottom + fs * 1.2} fontSize={fs * 0.8} fill={el.color} textAnchor="middle">{fmt(v)}</text>
          </g>,
        );
      } else {
        const y = plotBottom - ((plotBottom - plotTop) * i) / ticks;
        grid.push(
          <g key={i}>
            {el.showGrid && <line x1={left} x2={right} y1={y} y2={y} stroke={el.color} strokeOpacity={0.15} />}
            <text x={left - fs * 0.4} y={y} fontSize={fs * 0.8} fill={el.color} textAnchor="end" dominantBaseline="middle">{fmt(v)}</text>
          </g>,
        );
      }
    }
    const series = el.series;
    let marks: JSX.Element[] = [];
    if (el.chartType === 'column' || el.chartType === 'bar') {
      const band = horizontal ? (plotBottom - plotTop) / n : (right - left) / n;
      const bw = (band * 0.7) / Math.max(1, series.length);
      marks = series.flatMap((s, si) =>
        s.values.slice(0, n).map((v, i) => {
          const frac = Math.max(0, v) / max;
          if (horizontal) {
            const y = plotTop + band * i + band * 0.15 + bw * si;
            const len = (right - left) * frac;
            return (
              <g key={`${si}-${i}`}>
                <rect x={left} y={y} width={len} height={bw * 0.92} fill={s.color} rx={2} />
                {el.showValues && <text x={left + len + 4} y={y + bw / 2} fontSize={fs * 0.8} fill={el.color} dominantBaseline="middle">{fmt(v)}</text>}
              </g>
            );
          }
          const x = left + band * i + band * 0.15 + bw * si;
          const len = (plotBottom - plotTop) * frac;
          return (
            <g key={`${si}-${i}`}>
              <rect x={x} y={plotBottom - len} width={bw * 0.92} height={len} fill={s.color} rx={2} />
              {el.showValues && <text x={x + bw / 2} y={plotBottom - len - 4} fontSize={fs * 0.8} fill={el.color} textAnchor="middle">{fmt(v)}</text>}
            </g>
          );
        }));
    } else {
      const step = n > 1 ? (right - left) / (n - 1) : 0;
      marks = series.map((s, si) => {
        const p = s.values.slice(0, n).map((v, i) => [left + step * i, plotBottom - ((plotBottom - plotTop) * Math.max(0, v)) / max]);
        const line = p.map(([x, y], i) => `${i ? 'L' : 'M'}${x} ${y}`).join(' ');
        return (
          <g key={si}>
            {el.chartType === 'area' && p.length > 0 && (
              <path d={`${line} L${p[p.length - 1][0]} ${plotBottom} L${p[0][0]} ${plotBottom} Z`} fill={s.color} fillOpacity={0.35} />
            )}
            <path d={line} fill="none" stroke={s.color} strokeWidth={Math.max(2, fs * 0.2)} strokeLinejoin="round" />
            {p.map(([x, y], i) => (
              <g key={i}>
                <circle cx={x} cy={y} r={fs * 0.3} fill={s.color} />
                {el.showValues && <text x={x} y={y - fs * 0.6} fontSize={fs * 0.8} fill={el.color} textAnchor="middle">{fmt(s.values[i])}</text>}
              </g>
            ))}
          </g>
        );
      });
    }
    const labels = el.labels.map((l, i) => {
      if (horizontal) {
        const band = (plotBottom - plotTop) / n;
        return <text key={i} x={left - fs * 0.4} y={plotTop + band * (i + 0.5)} fontSize={fs * 0.85} fill={el.color} textAnchor="end" dominantBaseline="middle">{l}</text>;
      }
      const x = el.chartType === 'column' ? left + ((right - left) / n) * (i + 0.5) : left + (n > 1 ? ((right - left) / (n - 1)) * i : 0);
      return <text key={i} x={x} y={plotBottom + fs * 1.2} fontSize={fs * 0.85} fill={el.color} textAnchor="middle">{l}</text>;
    });
    body = (
      <g>
        {grid}
        <line x1={left} x2={horizontal ? left : right} y1={plotBottom} y2={horizontal ? plotTop : plotBottom} stroke={el.color} strokeOpacity={0.5} />
        {horizontal && <line x1={left} x2={right} y1={plotBottom} y2={plotBottom} stroke={el.color} strokeOpacity={0.5} />}
        {marks}
        {!horizontal && labels}
        {horizontal && labels}
      </g>
    );
  }

  return (
    <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} style={{ fontFamily: font, overflow: 'visible' }}>
      {el.title && (
        <text x={w / 2} y={fs * 1.3} fontSize={fs * 1.3} fontWeight={700} fill={el.color} textAnchor="middle">{el.title}</text>
      )}
      {body}
      {legend}
    </svg>
  );
}

const PIE_FALLBACK = ['#2563eb', '#16a34a', '#f59e0b', '#dc2626', '#7c3aed', '#0891b2', '#db2777', '#65a30d'];

function pieColor(el: ChartElement, i: number): string {
  return el.series[i]?.color && el.series.length > 1 ? el.series[i].color : PIE_FALLBACK[i % PIE_FALLBACK.length];
}
