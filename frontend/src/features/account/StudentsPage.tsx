import { HowItWorks } from "../../components/HowItWorks";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useId, useState, type FormEvent } from "react";
import { Navigate, useLocation, useNavigate } from "react-router";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey, isApiError } from "../../api/apiErrors";
import type { Gender, NewStudent } from "../../api/apiTypes";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { TextField } from "../../components/TextField";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";
import { cleanDigits, maskBirthRegistrationNumber } from "../student-identity/birthRegistrationNumber";
import { useStudentId } from "../student-identity/useStudentId";
import { myStudentsKey, useMe } from "./useAccount";

const emptyForm: NewStudent = { birthRegistrationNumber: "", fullName: "", dateOfBirth: "", gender: "" };

// The children on this guardian's account, and the form to add one.
export function StudentsPage() {
  const { t } = useLanguage();
  const me = useMe();
  const { students, selectStudent } = useStudentId();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const genderId = useId();
  const [form, setForm] = useState<NewStudent>(emptyForm);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<MessageKey | null>(null);
  const [added, setAdded] = useState<string | null>(null);
  const from = (location.state as { from?: string } | null)?.from;

  const add = useMutation({
    mutationFn: admissionApi.addStudent,
    onSuccess: async (student) => {
      await queryClient.invalidateQueries({ queryKey: myStudentsKey });
      selectStudent(student.birthRegistrationNumber);
      setForm(emptyForm);
      setFieldErrors({});
      setAdded(student.fullName);
      // Came here on the way to applying: go straight back.
      if (from) navigate(from, { replace: true });
    },
    onError: (error) => {
      setFieldErrors(isApiError(error) ? error.fieldErrors : {});
      setFormError(errorMessageKey(error));
    },
  });

  if (me.isPending) return null;
  if (me.data?.kind !== "GUARDIAN") return <Navigate to="/login" replace state={{ from: "/students" }} />;

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setAdded(null);
    add.mutate({ ...form, birthRegistrationNumber: cleanDigits(form.birthRegistrationNumber), fullName: form.fullName.trim() });
  }

  const set = (field: keyof NewStudent) => (value: string) => setForm((current) => ({ ...current, [field]: value }));

  return (
    <section className="page page--narrow">
      <h1 className="page-title">{t("students.title")}</h1>
      <HowItWorks text={t("students.intro")} />

      {students.length === 0 ? (
        <p className="sheet empty-state">{t("students.empty")}</p>
      ) : (
        <ul className="applications">
          {students.map((student) => (
            <li key={student.id} className="application-row">
              <div className="application-row__main">
                <h2 className="application-row__class">{student.fullName}</h2>
                <p className="application-row__date mono" translate="no">{maskBirthRegistrationNumber(student.birthRegistrationNumber)}</p>
              </div>
            </li>
          ))}
        </ul>
      )}

      <form className="sheet office-form students-form" onSubmit={onSubmit} noValidate>
        <h2 className="section-title">{t("students.addTitle")}</h2>
        <TextField
          label={t("students.brn")}
          hint={t("students.brnHint")}
          inputMode="numeric"
          autoComplete="off"
          maxLength={25}
          value={form.birthRegistrationNumber}
          onChange={(event) => set("birthRegistrationNumber")(event.target.value)}
          error={fieldErrors.birthRegistrationNumber}
        />
        <TextField
          label={t("students.fullName")}
          autoComplete="off"
          maxLength={100}
          value={form.fullName}
          onChange={(event) => set("fullName")(event.target.value)}
          error={fieldErrors.fullName}
        />
        <TextField
          label={t("students.dob")}
          type="date"
          value={form.dateOfBirth}
          onChange={(event) => set("dateOfBirth")(event.target.value)}
          error={fieldErrors.dateOfBirth}
          className="field--short"
        />
        <div className="field">
          <label htmlFor={genderId} className="field__label">
            {t("students.gender")}
          </label>
          <select
            id={genderId}
            className="field__input field__select"
            value={form.gender}
            aria-invalid={fieldErrors.gender ? true : undefined}
            onChange={(event) => set("gender")(event.target.value as Gender | "")}
          >
            <option value="">{t("students.chooseGender")}</option>
            <option value="MALE">{t("students.male")}</option>
            <option value="FEMALE">{t("students.female")}</option>
            <option value="OTHER">{t("students.other")}</option>
          </select>
          {fieldErrors.gender && <p className="field__error">{fieldErrors.gender}</p>}
        </div>
        <Button type="submit" pending={add.isPending}>
          {t("students.add")}
        </Button>
        {formError && <InlineAlert>{t(formError)}</InlineAlert>}
        {added && <InlineAlert tone="success">{t("students.added", { name: added })}</InlineAlert>}
      </form>
    </section>
  );
}
