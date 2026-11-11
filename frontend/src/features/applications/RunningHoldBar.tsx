import { Link, useLocation } from "react-router";
import { useLanguage } from "../../i18n/LanguageProvider";
import { useServerNow } from "../../lib/serverClock";
import { HoldCountdown } from "./HoldCountdown";
import { effectiveStatus } from "./holdStatus";
import { useMyHolds } from "./useMyHolds";

// A slim bar on every page while this student has a seat on hold. After reopening the
// browser on any address, this is the first thing that shows where the hold is.
export function RunningHoldBar() {
  const { t } = useLanguage();
  const holds = useMyHolds();
  const now = useServerNow();
  const location = useLocation();

  const running = holds.data?.find((hold) => effectiveStatus(hold.status, hold.expiresAt, now) === "HELD");
  if (!running) return null;
  // The application page already shows the big countdown for this hold.
  if (location.pathname === `/applications/${running.holdId}`) return null;

  return (
    <div className="running-hold">
      <div className="page-width running-hold__inner">
        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="1.8">
          <circle cx="12" cy="12" r="9" />
          <path d="M12 7v5l3 2" />
        </svg>
        <p className="running-hold__text">{t("runningHold.text", { className: running.className })}</p>
        <HoldCountdown expiresAt={running.expiresAt} now={now} size="small" />
        <Link to={`/applications/${running.holdId}`} className="running-hold__link">
          {t("runningHold.open")}
        </Link>
      </div>
    </div>
  );
}
