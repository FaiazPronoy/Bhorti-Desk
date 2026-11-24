import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Link } from "react-router";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey, isApiError } from "../../api/apiErrors";
import type { DocumentKind, OfficeApplication } from "../../api/apiTypes";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { TextField } from "../../components/TextField";
import { useLanguage } from "../../i18n/LanguageProvider";
import { en, type MessageKey } from "../../i18n/en";
import { classListKey } from "../classes/useClassList";

const officeApplicationsKey = ["office", "applications"] as const;

// Paid applications waiting for the office to verify or reject them.
export function OfficeReview() {
  const { t } = useLanguage();
  const list = useQuery({ queryKey: officeApplicationsKey, queryFn: admissionApi.officeApplications, retry: false, refetchInterval: 15_000 });

  if (list.isPending) return <p className="loading-line">{t("common.loading")}</p>;
  if (list.isError) {
    if (isApiError(list.error) && list.error.status === 401) {
      return (
        <div className="sheet empty-state">
          <p>{t("review.needSignIn")}</p>
          <Link to="/staff/login" className="button button--secondary">
            <span>{t("account.signIn")}</span>
          </Link>
        </div>
      );
    }
    return <InlineAlert>{t(errorMessageKey(list.error))}</InlineAlert>;
  }
  if (list.data.length === 0) return <p className="sheet empty-state">{t("review.empty")}</p>;

  return (
    <ul className="review-list">
      {list.data.map((item) => (
        <ReviewRow key={item.holdId} item={item} />
      ))}
    </ul>
  );
}

function ReviewRow({ item }: { item: OfficeApplication }) {
  const { t, number } = useLanguage();
  const queryClient = useQueryClient();
  const [rejecting, setRejecting] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const done = () => {
    setError(null);
    setRejecting(false);
    void queryClient.invalidateQueries({ queryKey: officeApplicationsKey });
    void queryClient.invalidateQueries({ queryKey: classListKey });
  };
  const fail = (caught: unknown) => {
    const field = isApiError(caught) ? caught.fieldErrors.reason : undefined;
    setError(field && field in en ? t(field as MessageKey) : t(errorMessageKey(caught)));
  };
  const verify = useMutation({ mutationFn: () => admissionApi.verifyApplication(item.holdId), onSuccess: done, onError: fail });
  const reject = useMutation({ mutationFn: () => admissionApi.rejectApplication(item.holdId, reason.trim()), onSuccess: done, onError: fail });

  return (
    <li className="review-row">
      <div className="review-row__head">
        <div>
          <h3 className="application-row__class">{item.applicant?.studentNameBn ?? t("review.noDetails")}</h3>
          <p className="application-row__date">
            {item.className}, <span className="mono">{item.serial}</span>, {t("student.lastFour", { digits: item.studentIdLastFour })}
          </p>
          {item.applicant && (
            <p className="application-row__date">
              {item.applicant.studentNameEn}, {item.applicant.dateOfBirth}, {item.applicant.guardianName}
            </p>
          )}
          {item.payment && (
            <p className="application-row__date">
              {t("payment.sandboxMethod")}: {t("payment.taka", { amount: number(item.payment.amountTaka) })}, {t(`payment.status.${item.payment.status}`)}
            </p>
          )}
        </div>
        <div className="review-row__docs">
          <OfficeDocument holdId={item.holdId} kind="PHOTO" present={item.documents.some((d) => d.kind === "PHOTO")} />
          <OfficeDocument holdId={item.holdId} kind="CERTIFICATE" present={item.documents.some((d) => d.kind === "CERTIFICATE")} />
        </div>
      </div>
      {item.review ? (
        <p className={`receipt__review receipt__review--${item.review.decision.toLowerCase()}`}>
          {item.review.decision === "VERIFIED" ? t("review.verified") : t("review.rejectedNote", { reason: item.review.reason ?? "" })}
          {item.review.refundDue && ` ${t("review.refundDue")}`}
        </p>
      ) : rejecting ? (
        <div className="review-row__reject">
          <TextField label={t("review.reason")} maxLength={300} value={reason} onChange={(event) => setReason(event.target.value)} />
          <div className="review-row__actions">
            <Button variant="danger" pending={reject.isPending} onClick={() => reject.mutate()}>
              {t("review.rejectConfirm")}
            </Button>
            <Button variant="quiet" onClick={() => setRejecting(false)}>
              {t("application.cancelNo")}
            </Button>
          </div>
        </div>
      ) : (
        <div className="review-row__actions">
          <Button pending={verify.isPending} onClick={() => verify.mutate()}>
            {t("review.verify")}
          </Button>
          <Button variant="secondary" onClick={() => setRejecting(true)}>
            {t("review.reject")}
          </Button>
        </div>
      )}
      {error && <InlineAlert>{error}</InlineAlert>}
    </li>
  );
}

// Loaded only when staff open it, because every view is written to the audit log.
function OfficeDocument({ holdId, kind, present }: { holdId: string; kind: DocumentKind; present: boolean }) {
  const { t } = useLanguage();
  const [asked, setAsked] = useState(false);
  const blob = useQuery({ queryKey: ["office", "document", holdId, kind], queryFn: () => admissionApi.officeDocumentBlob(holdId, kind), enabled: asked, staleTime: Infinity });
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!blob.data) return;
    const objectUrl = URL.createObjectURL(blob.data);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [blob.data]);

  const label = kind === "PHOTO" ? t("document.photo") : t("document.certificate");
  if (!present) return <span className="review-row__doc review-row__doc--missing">{t("review.missing", { name: label })}</span>;
  if (!asked) {
    return (
      <Button variant="quiet" onClick={() => setAsked(true)}>
        {t("review.view", { name: label })}
      </Button>
    );
  }
  if (!url) return <span className="review-row__doc">{t("common.loading")}</span>;
  if (blob.data?.type === "application/pdf") {
    return (
      <a className="review-row__doc" href={url} target="_blank" rel="noreferrer">
        {t("review.openPdf", { name: label })}
      </a>
    );
  }
  return <img className="review-row__preview" src={url} alt={label} />;
}
