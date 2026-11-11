import type { ReactNode } from "react";
import type { HoldStatus } from "../../api/apiTypes";
import { useLanguage } from "../../i18n/LanguageProvider";
import { formatDateTime } from "../../i18n/formatNumber";
import { StatusStamp } from "./StatusStamp";

interface SeatTokenProps {
  className: string;
  holdId: string;
  status: HoldStatus;
  studentId: string | null;
  createdAt?: string;
  children?: ReactNode;
}

// The application drawn as a paper token: the main part with the class, the number
// and the countdown, and a stub with the serial, behind a perforated edge.
export function SeatToken({ className, holdId, status, studentId, createdAt, children }: SeatTokenProps) {
  const { t, language } = useLanguage();
  return (
    <article className={`token token--${status.toLowerCase()}`} aria-labelledby="token-class">
      <div className="token__body">
        <div className="token__heading">
          <div>
            <p className="token__label">{t("application.class")}</p>
            <h2 id="token-class" className="token__class">
              {className}
            </h2>
          </div>
          <StatusStamp status={status} />
        </div>
        {studentId && (
          <p className="token__student">
            <span className="token__label">{t("application.student")}</span>
            <span className="mono">{studentId}</span>
          </p>
        )}
        {children}
      </div>
      <div className="token__stub">
        <span className="token__label">{t("application.serial")}</span>
        <span className="token__serial mono">{holdId}</span>
        {createdAt && <span className="token__issued">{t("application.issued", { time: formatDateTime(createdAt, language) })}</span>}
        {status === "CONFIRMED" && (
          <div className="seal" aria-hidden="true">
            <div>
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
                <path d="M5 12.5l4.5 4.5L19 7.5" />
              </svg>
              {t("application.sealPaid")}
            </div>
          </div>
        )}
      </div>
    </article>
  );
}
