import { Outlet, useLocation } from "react-router";
import { ErrorBoundary } from "./components/ErrorBoundary";
import { ServerStatusBanner } from "./components/ServerStatusBanner";
import { SiteHeader } from "./components/SiteHeader";
import { ThemeToggle } from "./components/ThemeToggle";
import { StudentIdProvider } from "./features/student-identity/useStudentId";
import { RunningHoldBar } from "./features/applications/RunningHoldBar";
import { useLanguage } from "./i18n/LanguageProvider";

// The frame around every page: header with the account bar, the server banner, the
// running hold bar, the page itself and a one line footer.
export function App() {
  const { t } = useLanguage();
  const location = useLocation();

  return (
    <StudentIdProvider>
      <div className="app">
        <SiteHeader />
        <ServerStatusBanner />
        <RunningHoldBar />
        <main id="main" className="page-width main" tabIndex={-1}>
          {/* A new address gets a fresh boundary, so one broken page does not stick. */}
          <ErrorBoundary key={location.pathname} message={t("error.pageCrashed")} reloadLabel={t("error.reload")}>
            <Outlet />
          </ErrorBoundary>
        </main>
        <footer className="site-footer">
          <div className="page-width site-footer__inner">
            <p>
              {t("site.footerBuiltBy")} {t("application.payNote")}
            </p>
            <ThemeToggle />
          </div>
        </footer>
      </div>
    </StudentIdProvider>
  );
}
