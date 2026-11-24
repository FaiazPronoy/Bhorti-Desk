import { Link, useSearchParams } from "react-router";
import { useMe } from "../account/useAccount";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";
import { signInRequired } from "../../lib/runtimeConfig";
import { ClassRegister } from "../classes/ClassListPage";
import { CreateClassForm } from "./CreateClassForm";
import { OfficeAudit } from "./OfficeAudit";
import { OfficeClassAdmin } from "./OfficeClassAdmin";
import { OfficeDashboard } from "./OfficeDashboard";
import { OfficeReview } from "./OfficeReview";
import { OfficeSessions } from "./OfficeSessions";

const tabs: { id: string; label: MessageKey }[] = [
  { id: "classes", label: "officeTabs.classes" },
  { id: "review", label: "officeTabs.review" },
  { id: "dashboard", label: "officeTabs.dashboard" },
  { id: "session", label: "officeTabs.session" },
  { id: "audit", label: "officeTabs.audit" },
];

// A quiet application frame: one tab rail, one white work area. The first tab keeps the
// original "create a class" screen, which works without signing in by default.
export function CreateClassPage() {
  const { t } = useLanguage();
  const me = useMe();
  const [params] = useSearchParams();
  const tab = tabs.some((item) => item.id === params.get("tab")) ? params.get("tab")! : "classes";
  const staffOnly = signInRequired();
  if (staffOnly && me.isPending) return null;
  // With sign-in on, only school office staff see the page. The API checks this too.
  if (staffOnly && me.data?.kind !== "STAFF") {
    return (
      <section className="page page--narrow">
        <h1 className="page-title">{t("office.title")}</h1>
        <div className="sheet empty-state">
          <p>{t("office.signInNeeded")}</p>
          <Link to="/staff/login" className="button button--primary">
            <span>{t("account.signIn")}</span>
          </Link>
        </div>
      </section>
    );
  }
  return (
    <section className="page">
      <h1 className="page-title">{t("office.title")}</h1>
      <nav className="office-tabs" aria-label={t("officeTabs.label")}>
        {tabs.map((item) => (
          <Link key={item.id} to={`/school-office?tab=${item.id}`} className={`office-tabs__tab ${tab === item.id ? "office-tabs__tab--active" : ""}`} aria-current={tab === item.id ? "page" : undefined}>
            {t(item.label)}
          </Link>
        ))}
      </nav>
      {tab === "classes" && (
        <>
          <div className="office-layout">
            <CreateClassForm />
            <div>
              <h2 className="section-title">{t("office.liveTable")}</h2>
              <ClassRegister showActions={false} />
            </div>
          </div>
          <section className="office-section">
            <h2 className="section-title">{t("officeClasses.title")}</h2>
            <OfficeClassAdmin />
          </section>
        </>
      )}
      {tab === "review" && (
        <section>
          <h2 className="section-title">{t("review.title")}</h2>
          <OfficeReview />
        </section>
      )}
      {tab === "dashboard" && (
        <section>
          <h2 className="section-title">{t("officeTabs.dashboard")}</h2>
          <OfficeDashboard />
        </section>
      )}
      {tab === "session" && (
        <section>
          <h2 className="section-title">{t("office.sessionsTitle")}</h2>
          <OfficeSessions />
        </section>
      )}
      {tab === "audit" && (
        <section>
          <h2 className="section-title">{t("officeTabs.audit")}</h2>
          <OfficeAudit />
        </section>
      )}
    </section>
  );
}
