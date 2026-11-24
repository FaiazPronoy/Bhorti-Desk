import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey } from "../../api/apiErrors";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { useLanguage } from "../../i18n/LanguageProvider";
import { useStudentId } from "../student-identity/useStudentId";
import { QrCode } from "./QrCode";

// The admission slip: a sheet a guardian prints or saves as a PDF with the browser's own
// "Save as PDF". The QR opens the public verify page.
export function SlipPage() {
  const { holdId = "" } = useParams();
  const { t } = useLanguage();
  const { studentId } = useStudentId();
  const slip = useQuery({
    queryKey: ["student", studentId, "slip", holdId],
    queryFn: () => admissionApi.slip(holdId, studentId!),
    enabled: studentId !== null,
    retry: false,
  });
  const photo = useQuery({
    queryKey: ["student", studentId, "slip-photo", holdId],
    queryFn: () => admissionApi.documentBlob(holdId, studentId!, "PHOTO"),
    enabled: Boolean(slip.data?.hasPhoto),
    staleTime: Infinity,
  });
  const [photoUrl, setPhotoUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!photo.data) return;
    const url = URL.createObjectURL(photo.data);
    setPhotoUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [photo.data]);

  if (!studentId) return <InlineAlert>{t("myApplications.needNumber")}</InlineAlert>;
  if (slip.isPending) return <p className="loading-line">{t("common.loading")}</p>;
  if (!slip.data) {
    return (
      <section className="page page--narrow">
        <InlineAlert>{t(errorMessageKey(slip.error))}</InlineAlert>
        <p className="back-link">
          <Link to={`/applications/${holdId}`}>{t("payment.backToApplication")}</Link>
        </p>
      </section>
    );
  }

  const data = slip.data;
  return (
    <section className="page page--narrow">
      <article className="slip" aria-labelledby="slip-title">
        <header className="slip__head">
          <h1 id="slip-title" className="slip__title">
            {t("slip.title")}
          </h1>
          <span className={`stamp stamp--${data.admissionStatus === "REJECTED" ? "cancelled" : "confirmed"}`}>{t(`slip.status.${data.admissionStatus}`)}</span>
        </header>
        <div className="slip__body">
          <div className="slip__photo">{photoUrl ? <img src={photoUrl} alt={t("document.photo")} /> : <span>{t("slip.noPhoto")}</span>}</div>
          <dl className="slip__facts">
            {data.studentNameBn && (
              <div>
                <dt>{t("slip.student")}</dt>
                <dd>
                  <span lang="bn">{data.studentNameBn}</span>
                  {data.studentNameEn && <span className="slip__latin" lang="en">{data.studentNameEn}</span>}
                </dd>
              </div>
            )}
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
              <dt>{t("application.serial")}</dt>
              <dd className="mono" translate="no">
                {data.serial}
              </dd>
            </div>
          </dl>
          <figure className="slip__qr">
            <QrCode modules={data.qr} label={t("slip.qrLabel")} />
            <figcaption>{t("slip.qrCaption")}</figcaption>
          </figure>
        </div>
        <p className="slip__note">{t("slip.note")}</p>
      </article>
      <div className="slip-actions">
        <Button onClick={() => window.print()}>{t("slip.print")}</Button>
        <Link to={`/applications/${holdId}`}>{t("payment.backToApplication")}</Link>
      </div>
    </section>
  );
}
