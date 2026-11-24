import type { HoldStatus } from "../../api/apiTypes";
import { useLanguage } from "../../i18n/LanguageProvider";

// The status as a rubber stamp: always a word, never colour alone. "landing" plays the
// one stamp motion, once, right after a payment succeeds.
export function StatusStamp({ status, small = false, landing = false }: { status: HoldStatus; small?: boolean; landing?: boolean }) {
  const { t } = useLanguage();
  return (
    <span className={`stamp stamp--${status.toLowerCase()} ${small ? "stamp--small" : ""} ${landing ? "stamp--landing" : ""}`} data-status={status}>
      {t(`status.${status}`)}
    </span>
  );
}
