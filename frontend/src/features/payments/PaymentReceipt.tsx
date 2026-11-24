import type { ApplicationDetails } from "../../api/apiTypes";
import { StampedSlip } from "../../components/Illustrations";
import { useLanguage } from "../../i18n/LanguageProvider";
import { formatDateTime } from "../../i18n/formatNumber";

// What the guardian keeps after paying. It says plainly that this was a sandbox payment
// and shows no bank name or transaction number that could pass for a real one.
export function PaymentReceipt({ application }: { application: ApplicationDetails }) {
  const { t, number, language } = useLanguage();
  const payment = application.payment;
  const review = application.review;
  if (!payment && !review) return null;

  return (
    <section className="receipt" aria-labelledby="receipt-title">
      <h2 id="receipt-title" className="section-title">
        {t("payment.receiptTitle")}
      </h2>
      {payment?.status === "PAID" && <StampedSlip />}
      {payment && (
        <dl className="receipt__lines">
          <div>
            <dt>{t("payment.method")}</dt>
            <dd>{t("payment.sandboxMethod")}</dd>
          </div>
          <div>
            <dt>{t("payment.amount")}</dt>
            <dd>{t("payment.taka", { amount: number(payment.amountTaka) })}</dd>
          </div>
          <div>
            <dt>{t("payment.state")}</dt>
            <dd>{t(`payment.status.${payment.status}`)}</dd>
          </div>
          {payment.finishedAt && (
            <div>
              <dt>{t("payment.when")}</dt>
              <dd>{formatDateTime(payment.finishedAt, language)}</dd>
            </div>
          )}
        </dl>
      )}
      {review && (
        <p className={`receipt__review receipt__review--${review.decision.toLowerCase()}`}>
          {review.decision === "VERIFIED" ? t("review.verifiedNote") : t("review.rejectedNote", { reason: review.reason ?? "" })}
          {review.refundDue && ` ${t("review.refundDue")}`}
        </p>
      )}
    </section>
  );
}
