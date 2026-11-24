import { Link } from "react-router";
import { useMe } from "../account/useAccount";
import { useLanguage } from "../../i18n/LanguageProvider";
import { signInRequired } from "../../lib/runtimeConfig";
import { ClassRegister } from "../classes/ClassListPage";
import { CreateClassForm } from "./CreateClassForm";
import { OfficeSessions } from "./OfficeSessions";

export function CreateClassPage() {
  const { t } = useLanguage();
  const me = useMe();
  const staffOnly = signInRequired();
  if (staffOnly && me.isPending) return null;
  // With sign-in on, only school office staff see the form. The API checks this too.
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
      <p className="page-intro">{t("office.intro")}</p>
      <div className="office-layout">
        <CreateClassForm />
        <div>
          <h2 className="section-title">{t("office.liveTable")}</h2>
          <ClassRegister showActions={false} />
        </div>
      </div>
      <section className="office-section">
        <h2 className="section-title">{t("office.sessionsTitle")}</h2>
        <OfficeSessions />
      </section>
    </section>
  );
}
