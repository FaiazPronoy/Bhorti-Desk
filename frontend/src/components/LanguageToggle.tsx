import { useLanguage } from "../i18n/LanguageProvider";

export function LanguageToggle() {
  const { language, setLanguage, t } = useLanguage();
  const next = language === "bn" ? "en" : "bn";
  return (
    <button
      type="button"
      className="button button--quiet language-toggle"
      onClick={() => setLanguage(next)}
      aria-label={t("language.switchLabel")}
      lang={next}
    >
      <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="1.6">
        <circle cx="12" cy="12" r="9" />
        <path d="M3 12h18M12 3c2.5 2.6 3.8 5.6 3.8 9s-1.3 6.4-3.8 9c-2.5-2.6-3.8-5.6-3.8-9S9.5 5.6 12 3z" />
      </svg>
      <span>{t("language.switchTo")}</span>
    </button>
  );
}
