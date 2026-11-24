import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey } from "../../api/apiErrors";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";

// The sandbox gateway's hosted page. A real gateway would show its own page on its own
// address; this one lives in the app and says on every line that no money moves.
export function SandboxPayPage() {
  const { paymentId = "" } = useParams();
  const { t, number } = useLanguage();
  const navigate = useNavigate();
  const page = useQuery({ queryKey: ["sandbox", paymentId], queryFn: () => admissionApi.sandboxPage(paymentId), retry: false });
  const [error, setError] = useState<MessageKey | null>(null);
  const [failed, setFailed] = useState(false);

  const decide = useMutation({
    mutationFn: (outcome: "PAID" | "FAILED") => admissionApi.sandboxDecide(paymentId, outcome),
    onSuccess: (result) => {
      if (result.status === "PAID" && page.data) navigate(`/applications/${page.data.holdId}`, { state: { justPaid: true } });
      else if (result.status === "FAILED") setFailed(true);
      else void page.refetch();
    },
    onError: (caught) => setError(errorMessageKey(caught)),
  });

  if (page.isPending) return <p className="loading-line">{t("common.loading")}</p>;
  if (!page.data) {
    return (
      <section className="page page--narrow">
        <InlineAlert>{t(errorMessageKey(page.error))}</InlineAlert>
      </section>
    );
  }

  const data = page.data;
  const finished = data.status !== "PENDING";
  return (
    <section className="page page--narrow">
      <div className="sandbox" role="region" aria-labelledby="sandbox-title">
        <p className="sandbox__banner">{t("payment.sandboxBanner")}</p>
        <h1 id="sandbox-title" className="page-title">
          {t("payment.sandboxTitle")}
        </h1>
        <dl className="sandbox__facts">
          <div>
            <dt>{t("application.class")}</dt>
            <dd>{data.className}</dd>
          </div>
          <div>
            <dt>{t("application.serial")}</dt>
            <dd className="mono" translate="no">
              {data.serial}
            </dd>
          </div>
          <div>
            <dt>{t("payment.amount")}</dt>
            <dd className="sandbox__amount">{t("payment.taka", { amount: number(data.amountTaka) })}</dd>
          </div>
        </dl>
        {!finished && !failed && (
          <div className="sandbox__actions">
            <Button pending={decide.isPending && decide.variables === "PAID"} disabled={decide.isPending} onClick={() => decide.mutate("PAID")}>
              {t("payment.sandboxPay")}
            </Button>
            <Button variant="secondary" pending={decide.isPending && decide.variables === "FAILED"} disabled={decide.isPending} onClick={() => decide.mutate("FAILED")}>
              {t("payment.sandboxFail")}
            </Button>
          </div>
        )}
        {(failed || data.status === "FAILED") && <InlineAlert>{t("payment.failed")}</InlineAlert>}
        {data.status === "LATE" && <InlineAlert>{t("payment.late")}</InlineAlert>}
        {error && <InlineAlert>{t(error)}</InlineAlert>}
        <p className="back-link">
          <Link to={`/applications/${data.holdId}`}>{t("payment.backToApplication")}</Link>
        </p>
      </div>
    </section>
  );
}
