import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useId, useState } from "react";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey, isApiError } from "../../api/apiErrors";
import type { ApplicationDetails, DocumentKind } from "../../api/apiTypes";
import { InlineAlert } from "../../components/InlineAlert";
import { Spinner } from "../../components/Spinner";
import { useLanguage } from "../../i18n/LanguageProvider";
import { en, type MessageKey } from "../../i18n/en";
import { applicationKey } from "./ApplicantForm";

const limits: Record<DocumentKind, number> = { PHOTO: 300 * 1024, CERTIFICATE: 1024 * 1024 };

export function DocumentUploads({ application, studentId }: { application: ApplicationDetails; studentId: string }) {
  const { t } = useLanguage();
  return (
    <section className="documents" aria-labelledby="documents-title">
      <h2 id="documents-title" className="section-title">
        {t("document.title")}
      </h2>
      <div className="documents__grid">
        <DocumentSlot kind="PHOTO" application={application} studentId={studentId} />
        <DocumentSlot kind="CERTIFICATE" application={application} studentId={studentId} />
      </div>
    </section>
  );
}

function DocumentSlot({ kind, application, studentId }: { kind: DocumentKind; application: ApplicationDetails; studentId: string }) {
  const { t, number } = useLanguage();
  const queryClient = useQueryClient();
  const inputId = useId();
  const [error, setError] = useState<string | null>(null);
  const saved = application.documents.find((document) => document.kind === kind);

  const preview = useQuery({
    queryKey: [...applicationKey(application.holdId, studentId), "document", kind, saved?.sizeBytes],
    queryFn: () => admissionApi.documentBlob(application.holdId, studentId, kind),
    enabled: Boolean(saved),
    staleTime: Infinity,
  });
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!preview.data) return;
    const objectUrl = URL.createObjectURL(preview.data);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [preview.data]);

  const upload = useMutation({
    mutationFn: (file: File) => admissionApi.uploadDocument(application.holdId, studentId, kind, file),
    onSuccess: () => {
      setError(null);
      void queryClient.invalidateQueries({ queryKey: applicationKey(application.holdId, studentId) });
    },
    onError: (caught) => {
      const field = isApiError(caught) ? caught.fieldErrors.file : undefined;
      setError(field && field in en ? t(field as MessageKey) : t(errorMessageKey(caught)));
    },
  });

  function onPick(file: File | undefined) {
    if (!file) return;
    // Checked here only to save a wasted upload on a slow connection. The server checks again.
    if (file.size > limits[kind]) {
      setError(t(kind === "PHOTO" ? "document.error.photoTooBig" : "document.error.certificateTooBig"));
      return;
    }
    upload.mutate(file);
  }

  const label = kind === "PHOTO" ? t("document.photo") : t("document.certificate");
  return (
    <div className="document-slot">
      <div className="document-slot__preview">
        {saved && url && saved.contentType.startsWith("image/") && <img src={url} alt={label} />}
        {saved && saved.contentType === "application/pdf" && <span>{t("document.pdfSaved")}</span>}
        {!saved && <span className="document-slot__empty">{t("document.none")}</span>}
      </div>
      <label htmlFor={inputId} className="button button--secondary document-slot__pick">
        {upload.isPending && <Spinner />}
        <span>{saved ? t("document.replace", { name: label }) : t("document.add", { name: label })}</span>
      </label>
      <input
        id={inputId}
        className="sr-only"
        type="file"
        accept={kind === "PHOTO" ? "image/jpeg,image/png" : "image/jpeg,image/png,application/pdf"}
        disabled={upload.isPending}
        onChange={(event) => {
          onPick(event.target.files?.[0]);
          event.target.value = "";
        }}
      />
      <p className="field__hint">{t(kind === "PHOTO" ? "document.photoHint" : "document.certificateHint", { size: number(kind === "PHOTO" ? 300 : 1) })}</p>
      {error && <InlineAlert>{error}</InlineAlert>}
    </div>
  );
}
