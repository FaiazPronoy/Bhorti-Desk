import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router";
import { admissionApi } from "../../api/admissionApiClient";
import { isApiError } from "../../api/apiErrors";
import { InlineAlert } from "../../components/InlineAlert";
import { useLanguage } from "../../i18n/LanguageProvider";

// Public: whoever scans the QR at the school gate sees only the class, the masked
// number and the status.
export function VerifyPage() {
  const { code = "" } = useParams();
  const { t } = useLanguage();
  const check = useQuery({ queryKey: ["verify", code], queryFn: () => admissionApi.verifySlip(code), retry: false });

  if (check.isPending) return <p className="loading-line">{t("common.loading")}</p>;
  if (!check.data) {
    const invalid = isApiError(check.error) && check.error.status === 404;
    return (
      <section className="page page--narrow">
        <h1 className="page-title">{t("verify.title")}</h1>
        <InlineAlert>{invalid ? t("verify.invalid") : t("error.network")}</InlineAlert>
      </section>
    );
  }

  const data = check.data;
  return (
    <section className="page page--narrow">
      <h1 className="page-title">{t("verify.title")}</h1>
      <div className={`verify verify--${data.admissionStatus.toLowerCase()}`}>
        <p className="verify__result">{data.admissionStatus === "REJECTED" ? t("verify.notAdmitted") : t("verify.real")}</p>
        <dl className="receipt__lines">
          <div>
            <dt>{t("application.class")}</dt>
            <dd>{data.className}</dd>
          </div>
          <div>
            <dt>{t("application.student")}</dt>
            <dd className="mono" translate="no">
              {data.maskedStudentId}
            </dd>
          </div>
          <div>
            <dt>{t("payment.state")}</dt>
            <dd>{t(`slip.status.${data.admissionStatus}`)}</dd>
          </div>
        </dl>
      </div>
    </section>
  );
}
