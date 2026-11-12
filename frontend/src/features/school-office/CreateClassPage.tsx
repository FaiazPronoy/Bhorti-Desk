import { Link } from "react-router";
import { useMe } from "../account/useAccount";
import { useLanguage } from "../../i18n/LanguageProvider";
import { ClassRegister } from "../classes/ClassListPage";
import { CreateClassForm } from "./CreateClassForm";

export function CreateClassPage() {
  const { t } = useLanguage();
  const me = useMe();
  if (me.isPending) return null;
  // Only school office staff see the form. The API checks this again on every request.
  if (me.data?.kind !== "STAFF") {
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
    </section>
  );
}
