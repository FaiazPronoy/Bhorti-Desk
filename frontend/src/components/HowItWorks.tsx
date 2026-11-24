import { useLanguage } from "../i18n/LanguageProvider";

// Page help waits behind a small disclosure, so no heading carries a sentence under it.
export function HowItWorks({ text }: { text: string }) {
  const { t } = useLanguage();
  return (
    <details className="how">
      <summary>{t("student.howItWorks")}</summary>
      <p>{text}</p>
    </details>
  );
}
