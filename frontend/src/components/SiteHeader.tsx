import { NavLink } from "react-router";
import { StudentIdBar } from "../features/student-identity/StudentIdBar";
import { useLanguage } from "../i18n/LanguageProvider";
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
          {/* A register book with a pencil-yellow cover band. */}
          <svg viewBox="0 0 32 32" width="28" height="28" aria-hidden="true">
            <rect x="5" y="3" width="22" height="26" rx="2" fill="var(--sheet)" stroke="var(--ink)" strokeWidth="2" />
            <rect x="5" y="3" width="6" height="26" rx="1" fill="var(--pencil)" stroke="var(--ink)" strokeWidth="2" />
            <path d="M15 11h8M15 16h8M15 21h5" stroke="var(--ink)" strokeWidth="2" />
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
