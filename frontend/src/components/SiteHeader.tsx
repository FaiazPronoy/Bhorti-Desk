import { NavLink } from "react-router";
import { StudentIdBar } from "../features/student-identity/StudentIdBar";
import { useLanguage } from "../i18n/LanguageProvider";
import { BenchMark } from "./BenchMark";
import { LanguageToggle } from "./LanguageToggle";

export function SiteHeader() {
  const { t } = useLanguage();
  const linkClass = ({ isActive }: { isActive: boolean }) => `nav__link ${isActive ? "nav__link--active" : ""}`;

  return (
    <header className="site-header">
      <a href="#main" className="skip-link">
        {t("site.skipToContent")}
      </a>
      <div className="page-width site-header__top">
        <NavLink to="/" className="site-name">
          <BenchMark />
          <span translate="no">{t("site.name")}</span>
        </NavLink>
        <nav className="nav" aria-label={t("nav.main")}>
          <NavLink to="/" end className={linkClass}>
            {t("nav.classes")}
          </NavLink>
          <NavLink to="/applications" className={linkClass}>
            {t("nav.myApplications")}
          </NavLink>
          <NavLink to="/school-office" className={({ isActive }) => `${linkClass({ isActive })} nav__link--quiet`}>
            {t("nav.schoolOffice")}
          </NavLink>
        </nav>
        <div className="header-tools">
          <LanguageToggle />
        </div>
      </div>
      <div className="site-header__student">
        <div className="page-width">
          <StudentIdBar />
        </div>
      </div>
    </header>
  );
}
