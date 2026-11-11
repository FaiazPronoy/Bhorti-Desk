import { NavLink } from "react-router";
import { StudentIdBar } from "../features/student-identity/StudentIdBar";
import { useLanguage } from "../i18n/LanguageProvider";
import { LanguageToggle } from "./LanguageToggle";
import { ThemeToggle } from "./ThemeToggle";

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
          <svg viewBox="0 0 32 32" width="26" height="26" aria-hidden="true">
            <rect width="32" height="32" rx="3" fill="var(--stamp)" />
            <rect x="7" y="9" width="18" height="4" fill="var(--sheet)" />
            <rect x="9" y="13" width="3" height="10" fill="var(--sheet)" />
            <rect x="20" y="13" width="3" height="10" fill="var(--sheet)" />
          </svg>
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
          <ThemeToggle />
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
