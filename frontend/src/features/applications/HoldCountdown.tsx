import { useLanguage } from "../../i18n/LanguageProvider";
import { formatClockTime } from "../../i18n/formatNumber";
import { formatCountdown, secondsLeft } from "./holdStatus";

interface HoldCountdownProps {
  expiresAt: string;
  // When the hold began, for the length of the pencil line. Without it, ten minutes.
  startedAt?: string;
  now: number;
  size?: "large" | "small";
}

// Worked out again on every tick from the absolute expiry time, never by counting
// down a number of our own.
export function HoldCountdown({ expiresAt, startedAt, now, size = "large" }: HoldCountdownProps) {
  const { t, language } = useLanguage();
  const left = secondsLeft(expiresAt, now);
  const lastMinute = left > 0 && left <= 60;
  // The pencil line turns to the danger colour for the last two minutes.
  const lastTwoMinutes = left > 0 && left <= 120;

  if (size === "small") {
    return <span className={`countdown countdown--small ${lastMinute ? "countdown--urgent" : ""}`}>{formatCountdown(left)}</span>;
  }

  const total = startedAt ? Math.max(1, (Date.parse(expiresAt) - Date.parse(startedAt)) / 1000) : 600;
  const share = Math.max(0, Math.min(1, left / total));

  return (
    <div className={`countdown-block ${lastMinute ? "countdown-block--urgent" : ""}`}>
      <span className="countdown-block__label">{t("application.timeLeft")}</span>
      <span className="countdown" role="timer" aria-label={`${t("application.timeLeft")} ${formatCountdown(left)}`}>
        {formatCountdown(left)}
      </span>
      {/* A pencil line that shortens with the time left. A picture only; the digits say it. */}
      <span className="pencil-line" aria-hidden="true">
        <span className={`pencil-line__lead ${lastTwoMinutes ? "pencil-line__lead--danger" : ""}`} style={{ transform: `scaleX(${share})` }} />
      </span>
      <span className="countdown-block__ends">{t("application.expiresAt", { time: formatClockTime(expiresAt, language) })}</span>
      {/* Read out once when the last minute starts, not every second. */}
      <p className="countdown-block__warning" aria-live="assertive">
        {lastMinute ? t("application.lastMinute") : ""}
      </p>
    </div>
  );
}
