import { Link } from "react-router";
import { FullBenches, OfferRising } from "../../components/Illustrations";
import type { NoticeKind } from "../../api/apiTypes";
import { useLanguage } from "../../i18n/LanguageProvider";
import { formatDateTime } from "../../i18n/formatNumber";
import { useNotices, useWaitlist } from "./useWaitlist";

// The places in line, with any seat offered from the waiting list first.
export function WaitlistPlaces() {
  const { t, number } = useLanguage();
  const places = useWaitlist().data ?? [];
  if (places.length === 0) return null;

  return (
    <section className="waitlist" aria-labelledby="waitlist-title">
      <h2 id="waitlist-title" className="section-title">
        {t("waitlist.title")}
      </h2>
      <ul className="applications">
        {places.map((place) =>
          place.status === "OFFERED" && place.offeredHoldId ? (
            <li key={place.entryId} className="application-row waitlist-offer">
              <OfferRising />
              <div className="application-row__main">
                <h3 className="application-row__class">{place.className}</h3>
                <p className="application-row__date">{t("waitlist.offered")}</p>
              </div>
              <Link to={`/applications/${place.offeredHoldId}`} className="application-row__open">
                {t("runningHold.open")}
              </Link>
            </li>
          ) : (
            <li key={place.entryId} className="application-row">
              <FullBenches />
              <div className="application-row__main">
                <h3 className="application-row__class">{place.className}</h3>
                <p className="application-row__date">{t("waitlist.position", { position: number(place.position) })}</p>
              </div>
            </li>
          ),
        )}
      </ul>
    </section>
  );
}

const noticeKey: Record<NoticeKind, `notice.${NoticeKind}`> = {
  HELD: "notice.HELD",
  TWO_MINUTES_LEFT: "notice.TWO_MINUTES_LEFT",
  PAID: "notice.PAID",
  VERIFIED: "notice.VERIFIED",
  REJECTED: "notice.REJECTED",
  EXPIRED: "notice.EXPIRED",
  OFFERED: "notice.OFFERED",
};

export function NoticeList() {
  const { t, language } = useLanguage();
  const notices = useNotices().data ?? [];
  if (notices.length === 0) return null;

  return (
    <section className="notices" aria-labelledby="notices-title">
      <h2 id="notices-title" className="section-title">
        {t("notice.title")}
      </h2>
      <ol className="notices__list">
        {notices.map((notice) => (
          <li key={notice.id} className="notices__item">
            <Link to={`/applications/${notice.holdId}`}>{t(noticeKey[notice.kind], { className: notice.className })}</Link>
            <span className="notices__time">{formatDateTime(notice.createdAt, language)}</span>
          </li>
        ))}
      </ol>
      <p className="field__hint">{t("notice.smsDemo")}</p>
    </section>
  );
}
