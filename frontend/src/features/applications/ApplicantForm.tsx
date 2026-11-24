import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey, isApiError } from "../../api/apiErrors";
import type { Applicant, ApplicantInput, ApplicationDetails } from "../../api/apiTypes";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { TextField } from "../../components/TextField";
import { useLanguage } from "../../i18n/LanguageProvider";
import { en, type MessageKey } from "../../i18n/en";
import { toLatinDigits } from "../student-identity/birthRegistrationNumber";

const emptyForm: Applicant = { studentNameBn: "", studentNameEn: "", dateOfBirth: "", guardianName: "", guardianPhone: "", previousSchool: "" };

export const applicationKey = (holdId: string, studentId: string | null) => ["student", studentId, "application", holdId] as const;

// The server sends a message key for each bad field, so it shows in the chosen language.
function isMessageKey(value: string): value is MessageKey {
  return value in en;
}

export function ApplicantForm({ application, studentId }: { application: ApplicationDetails; studentId: string }) {
  const { t } = useLanguage();
  const queryClient = useQueryClient();
  const [form, setForm] = useState<Applicant>(application.applicant ?? emptyForm);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<MessageKey | null>(null);
  const [saved, setSaved] = useState(false);

  const save = useMutation({
    mutationFn: (applicant: ApplicantInput) => admissionApi.saveApplicant(application.holdId, studentId, applicant),
    onSuccess: (next) => {
      queryClient.setQueryData(applicationKey(application.holdId, studentId), next);
      setFieldErrors({});
      setSaved(true);
    },
    onError: (error) => {
      setFieldErrors(isApiError(error) ? error.fieldErrors : {});
      setFormError(errorMessageKey(error));
    },
  });

  const errorFor = (field: keyof Applicant) => {
    const value = fieldErrors[field];
    if (!value) return undefined;
    return isMessageKey(value) ? t(value) : value;
  };
  const set = (field: keyof Applicant) => (value: string) => {
    setSaved(false);
    setForm((current) => ({ ...current, [field]: value }));
  };

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setSaved(false);
    save.mutate({ ...form, dateOfBirth: form.dateOfBirth || null, guardianPhone: toLatinDigits(form.guardianPhone) });
  }

  return (
    <form className="applicant-form" onSubmit={onSubmit} noValidate>
      <h2 className="section-title">{t("applicant.title")}</h2>
      <div className="applicant-form__grid">
        <TextField label={t("applicant.nameBn")} lang="bn" autoComplete="off" maxLength={100} value={form.studentNameBn} onChange={(event) => set("studentNameBn")(event.target.value)} error={errorFor("studentNameBn")} />
        <TextField label={t("applicant.nameEn")} lang="en" autoComplete="off" maxLength={100} value={form.studentNameEn} onChange={(event) => set("studentNameEn")(event.target.value)} error={errorFor("studentNameEn")} />
        <TextField label={t("applicant.birthDate")} type="date" value={form.dateOfBirth} onChange={(event) => set("dateOfBirth")(event.target.value)} error={errorFor("dateOfBirth")} />
        <TextField label={t("applicant.guardianName")} autoComplete="name" maxLength={100} value={form.guardianName} onChange={(event) => set("guardianName")(event.target.value)} error={errorFor("guardianName")} />
        <TextField label={t("applicant.guardianPhone")} hint={t("account.phoneHint")} type="tel" inputMode="tel" autoComplete="tel" maxLength={16} value={form.guardianPhone} onChange={(event) => set("guardianPhone")(event.target.value)} error={errorFor("guardianPhone")} />
        <TextField label={t("applicant.previousSchool")} autoComplete="off" maxLength={150} value={form.previousSchool ?? ""} onChange={(event) => set("previousSchool")(event.target.value)} error={errorFor("previousSchool")} />
      </div>
      <Button type="submit" variant="secondary" pending={save.isPending}>
        {t("applicant.save")}
      </Button>
      {formError && !saved && <InlineAlert>{t(formError)}</InlineAlert>}
      {saved && <InlineAlert tone="success">{t("applicant.saved")}</InlineAlert>}
    </form>
  );
}
