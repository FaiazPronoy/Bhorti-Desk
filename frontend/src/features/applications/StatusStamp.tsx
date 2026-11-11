import type { HoldStatus } from "../../api/apiTypes";
import { useLanguage } from "../../i18n/LanguageProvider";

// The status as a rubber stamp: always the word and the code, never colour alone.
export function StatusStamp({ status, small = false }: { status: HoldStatus; small?: boolean }) {
  const { t } = useLanguage();
  return (
    <span className={`stamp stamp--${status.toLowerCase()} ${small ? "stamp--small" : ""}`} data-status={status}>
      {t(`status.${status}`)}
    </span>
  );
}
