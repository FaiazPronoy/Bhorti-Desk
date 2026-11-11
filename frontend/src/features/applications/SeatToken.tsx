import type { ReactNode } from "react";
import type { HoldStatus } from "../../api/apiTypes";
import { useLanguage } from "../../i18n/LanguageProvider";
import { StatusStamp } from "./StatusStamp";

interface SeatTokenProps {
  className: string;
  holdId: string;
  status: HoldStatus;
  studentId: string | null;
  children?: ReactNode;
}

// The application drawn as a paper token: the main part with the class, the number
// and the countdown, and a stub with the serial, behind a perforated edge.
export function SeatToken({ className, holdId, status, studentId, children }: SeatTokenProps) {
  const { t } = useLanguage();
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
      </div>
    </article>
  );
}
