import { Link } from "react-router";
import { errorMessageKey } from "../../api/apiErrors";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { useLanguage } from "../../i18n/LanguageProvider";
import { formatDateTime } from "../../i18n/formatNumber";
import { useServerNow } from "../../lib/serverClock";
import { useStudentId } from "../student-identity/useStudentId";
import { HoldCountdown } from "./HoldCountdown";
import { effectiveStatus } from "./holdStatus";
import { StatusStamp } from "./StatusStamp";
import { useMyHolds } from "./useMyHolds";

export function MyApplicationsPage() {
  const { t, language } = useLanguage();
  const { studentId, askForNumber } = useStudentId();
  const holds = useMyHolds();
  const now = useServerNow();

  let content;
  if (!studentId) {
    content = (
      <div className="sheet empty-state">
        <p>{t("myApplications.needNumber")}</p>
        <Button variant="secondary" onClick={askForNumber}>
          {t("student.label")}
        </Button>
      </div>
    );
  } else if (holds.isPending) {
    content = (
      <div className="register register--loading" aria-busy="true" aria-label={t("common.loading")}>
        {[0, 1].map((row) => (
          <div key={row} className="skeleton-row" />
        ))}
      </div>
    );
  } else if (holds.isError && !holds.data) {
    content = (
      <div className="sheet empty-state">
        <InlineAlert>{t(errorMessageKey(holds.error))}</InlineAlert>
        <Button variant="secondary" pending={holds.isFetching} onClick={() => void holds.refetch()}>
          {t("server.retry")}
        </Button>
      </div>
    );
  } else if (holds.data.length === 0) {
    content = (
      <div className="sheet empty-state">
        <p>{t("myApplications.empty")}</p>
        <Link to="/" className="button button--primary">
          <span>{t("nav.classes")}</span>
        </Link>
      </div>
    );
  } else {
    // Newest first from the server; the running one goes to the top.
    const rows = holds.data
      .map((hold) => ({ hold, status: effectiveStatus(hold.status, hold.expiresAt, now) }))
      .sort((a, b) => Number(b.status === "HELD") - Number(a.status === "HELD"));

    content = (
      <ul className="applications">
        {rows.map(({ hold, status }) => (
          <li key={hold.holdId} className={`application-row application-row--${status.toLowerCase()}`}>
            <div className="application-row__main">
              <h2 className="application-row__class">{hold.className}</h2>
              {hold.createdAt && (
                <p className="application-row__date">{t("myApplications.appliedOn", { time: formatDateTime(hold.createdAt, language) })}</p>
              )}
            </div>
            <StatusStamp status={status} small />
            {status === "HELD" && <HoldCountdown expiresAt={hold.expiresAt} now={now} size="small" />}
            <Link to={`/applications/${hold.holdId}`} className="application-row__open">
              {t("myApplications.open")}
            </Link>
          </li>
        ))}
      </ul>
    );
  }

  return (
    <section className="page page--narrow">
      <h1 className="page-title">{t("myApplications.title")}</h1>
      <p className="page-intro">{t("myApplications.intro")}</p>
      {content}
    </section>
  );
}
