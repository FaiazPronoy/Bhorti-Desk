import { useLanguage } from "../../i18n/LanguageProvider";
import { formatClockTime } from "../../i18n/formatNumber";
import { formatCountdown, secondsLeft } from "./holdStatus";

interface HoldCountdownProps {
  expiresAt: string;
  now: number;
  size?: "large" | "small";
}

// Worked out again on every tick from the absolute expiry time, never by counting
// down a number of our own.
export function HoldCountdown({ expiresAt, now, size = "large" }: HoldCountdownProps) {
  const { t, language } = useLanguage();
  const left = secondsLeft(expiresAt, now);
  const lastMinute = left > 0 && left <= 60;

  if (size === "small") {
    return <span className={`countdown countdown--small ${lastMinute ? "countdown--urgent" : ""}`}>{formatCountdown(left)}</span>;
  }

  return (
    <div className={`countdown-block ${lastMinute ? "countdown-block--urgent" : ""}`}>
      <span className="countdown-block__label">{t("application.timeLeft")}</span>
      <span className="countdown" role="timer" aria-label={`${t("application.timeLeft")} ${formatCountdown(left)}`}>
        {formatCountdown(left)}
      </span>
      <span className="countdown-block__ends">{t("application.expiresAt", { time: formatClockTime(expiresAt, language) })}</span>
      {/* Read out once when the last minute starts, not every second. */}
      <p className="countdown-block__warning" aria-live="assertive">
        {lastMinute ? t("application.lastMinute") : ""}
      </p>
    </div>
  );
}
