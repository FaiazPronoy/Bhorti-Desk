import { useLanguage } from "../../i18n/LanguageProvider";
import { formatDateTime } from "../../i18n/formatNumber";
import { useServerNow } from "../../lib/serverClock";
import { timeUntil, useSeason, windowAt } from "./useSeason";

// First thing a guardian sees: is admission open, and for how long.
export function SessionStatus() {
  const { t, language, number } = useLanguage();
  const season = useSeason();
  const now = useServerNow();
  const session = season.data?.session;
  if (!session) return null;

  const state = windowAt(session, now);
  const target = state === "UPCOMING" ? session.opensAt : session.closesAt;
  const left = timeUntil(target, now);
  const pad = (value: number) => String(value).padStart(2, "0");
  const parts = [
    { value: number(left.days), label: t("season.days") },
    { value: pad(left.hours), label: t("season.hours") },
    { value: pad(left.minutes), label: t("season.minutes") },
    { value: pad(left.seconds), label: t("season.seconds") },
  ];

  return (
    <section className={`season season--${state.toLowerCase()}`} aria-labelledby="season-title">
      <div>
        <h2 id="season-title" className="season__title">
          {session.name}
        </h2>
        <p className="season__state">
          {state === "UPCOMING" && t("season.opensAt", { time: formatDateTime(session.opensAt, language) })}
          {state === "OPEN" && t("season.closesAt", { time: formatDateTime(session.closesAt, language) })}
          {state === "ENDED" && t("season.ended")}
        </p>
      </div>
      {state !== "ENDED" && (
        <div className="season__clock" role="timer" aria-label={t(state === "UPCOMING" ? "season.timeToOpen" : "season.timeToClose")}>
          {parts.map((part) => (
            <span key={part.label} className="season__unit">
              <span className="season__digits">{part.value}</span>
              <span className="season__label">{part.label}</span>
            </span>
          ))}
        </div>
      )}
    </section>
  );
}
