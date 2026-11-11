import { Link } from "react-router";
import { useLanguage } from "../i18n/LanguageProvider";

export function NotFoundPage() {
  const { t } = useLanguage();
  return (
    <section className="sheet empty-state">
      <h1 className="page-title">{t("notFound.title")}</h1>
      <p>{t("notFound.body")}</p>
      <p className="empty-state__links">
        <Link to="/">{t("nav.classes")}</Link>
        <Link to="/applications">{t("nav.myApplications")}</Link>
      </p>
    </section>
  );
}
