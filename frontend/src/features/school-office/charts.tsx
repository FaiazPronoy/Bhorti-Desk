import { useLanguage } from "../../i18n/LanguageProvider";
import { formatClockTime } from "../../i18n/formatNumber";

// Plain SVG charts, one series each. Bars are graphite; the one that matters (the latest
// hour) is pencil yellow. Every bar has a native tooltip, and each chart has a table.

export function HourlyBars({ hours }: { hours: { hour: string; applications: number }[] }) {
  const { t, number, language } = useLanguage();
  const width = 640;
  const height = 180;
  const top = 12;
  const bottom = 28;
  const max = Math.max(1, ...hours.map((item) => item.applications));
  const step = hours.length > 0 ? width / hours.length : width;
  const barWidth = Math.max(4, Math.min(28, step - 4));
  const plot = height - top - bottom;

  if (hours.length === 0) return <p className="chart__empty">{t("dashboard.noApplications")}</p>;

  return (
    <figure className="chart">
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={t("dashboard.perHourLabel")}>
        <line x1="0" x2={width} y1={height - bottom} y2={height - bottom} className="chart__axis" />
        {hours.map((item, index) => {
          const barHeight = Math.max(2, (item.applications / max) * plot);
          const x = index * step + (step - barWidth) / 2;
          const y = height - bottom - barHeight;
          const latest = index === hours.length - 1;
          const label = `${formatClockTime(item.hour, language)}: ${number(item.applications)}`;
          return (
            <g key={item.hour}>
              <title>{label}</title>
              {/* A taller invisible target, so the tooltip is easy to reach. */}
              <rect x={index * step} y={top} width={step} height={plot} fill="transparent" />
              <path
                className={latest ? "chart__bar chart__bar--accent" : "chart__bar"}
                d={`M${x} ${height - bottom}V${y + 4}q0 -4 4 -4h${barWidth - 8}q4 0 4 4V${height - bottom}z`}
              />
              {(index === 0 || latest || index === Math.floor(hours.length / 2)) && (
                <text x={x + barWidth / 2} y={height - 8} textAnchor="middle" className="chart__tick">
                  {formatClockTime(item.hour, language)}
                </text>
              )}
              {latest && (
                <text x={x + barWidth / 2} y={y - 4} textAnchor="middle" className="chart__value">
                  {number(item.applications)}
                </text>
              )}
            </g>
          );
        })}
      </svg>
      <details className="chart__table">
        <summary>{t("dashboard.showTable")}</summary>
        <table>
          <thead>
            <tr>
              <th scope="col">{t("dashboard.hour")}</th>
              <th scope="col">{t("dashboard.applications")}</th>
            </tr>
          </thead>
          <tbody>
            {hours.map((item) => (
              <tr key={item.hour}>
                <td>{formatClockTime(item.hour, language)}</td>
                <td>{number(item.applications)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </figure>
  );
}

// Each stage is a subset of the one before, so the stages are drawn as one bar each,
// sharing one scale, rather than stacked.
export function FunnelBars({ stages }: { stages: { label: string; value: number }[] }) {
  const { number } = useLanguage();
  const max = Math.max(1, stages[0]?.value ?? 1);
  return (
    <ol className="funnel">
      {stages.map((stage, index) => (
        <li key={stage.label} className="funnel__row" title={`${stage.label}: ${number(stage.value)}`}>
          <span className="funnel__label">{stage.label}</span>
          <span className="funnel__track">
            <span
              className={index === stages.length - 1 ? "funnel__fill funnel__fill--accent" : "funnel__fill"}
              style={{ width: `${(stage.value / max) * 100}%` }}
            />
          </span>
          <span className="funnel__value">{number(stage.value)}</span>
        </li>
      ))}
    </ol>
  );
}
