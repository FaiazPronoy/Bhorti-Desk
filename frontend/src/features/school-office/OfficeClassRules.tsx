import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey, isApiError } from "../../api/apiErrors";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { TextField } from "../../components/TextField";
import { useLanguage } from "../../i18n/LanguageProvider";
import { en, type MessageKey } from "../../i18n/en";
import { useClassList } from "../classes/useClassList";
import { classRulesKey, useClassRules } from "../classes/useClassRules";

export function OfficeClassRules() {
  const { t } = useLanguage();
  const classes = useClassList();
  const rules = useClassRules();
  const queryClient = useQueryClient();
  const [classId, setClassId] = useState("");
  const [minAge, setMinAge] = useState("");
  const [maxAge, setMaxAge] = useState("");
  const [cutoff, setCutoff] = useState("");
  const [fee, setFee] = useState("");
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<MessageKey | null>(null);
  const [saved, setSaved] = useState(false);

  const save = useMutation({
    mutationFn: () =>
      admissionApi.saveClassRules(classId, {
        minAge: minAge ? Number(minAge) : null,
        maxAge: maxAge ? Number(maxAge) : null,
        ageCutoffDate: cutoff || null,
        feeTaka: Number(fee || 0),
      }),
    onSuccess: () => {
      setFieldErrors({});
      setSaved(true);
      void queryClient.invalidateQueries({ queryKey: classRulesKey });
    },
    onError: (error) => {
      setFieldErrors(isApiError(error) ? error.fieldErrors : {});
      setFormError(errorMessageKey(error));
    },
  });

  function choose(id: string) {
    setClassId(id);
    setSaved(false);
    setFieldErrors({});
    setFormError(null);
    const current = rules.data?.find((item) => item.classId === id);
    setMinAge(current?.minAge?.toString() ?? "");
    setMaxAge(current?.maxAge?.toString() ?? "");
    setCutoff(current?.ageCutoffDate ?? "");
    setFee(current?.feeTaka?.toString() ?? "");
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setSaved(false);
    save.mutate();
  }

  const errorFor = (field: string) => {
    const value = fieldErrors[field];
    if (!value) return undefined;
    return value in en ? t(value as MessageKey) : value;
  };
  const digitsOnly = (value: string) => value.replace(/\D/g, "");

  return (
    <form className="sheet office-form" onSubmit={onSubmit} noValidate>
      <h3 className="section-title">{t("office.rulesTitle")}</h3>
      <div className="field">
        <label className="field__label" htmlFor="rules-class">
          {t("office.className")}
        </label>
        <select id="rules-class" className="field__input field__select" value={classId} onChange={(event) => choose(event.target.value)}>
          <option value="">{t("office.sessionChooseClass")}</option>
          {(classes.data ?? []).map((item) => (
            <option key={item.id} value={item.id}>
              {item.name}
            </option>
          ))}
        </select>
      </div>
      {classId && (
        <>
          <TextField label={t("office.rulesFee")} inputMode="numeric" maxLength={7} className="field--short" value={fee} onChange={(event) => setFee(digitsOnly(event.target.value))} error={errorFor("feeTaka")} />
          <TextField label={t("office.rulesMinAge")} inputMode="numeric" maxLength={2} className="field--short" value={minAge} onChange={(event) => setMinAge(digitsOnly(event.target.value))} error={errorFor("minAge")} />
          <TextField label={t("office.rulesMaxAge")} inputMode="numeric" maxLength={2} className="field--short" value={maxAge} onChange={(event) => setMaxAge(digitsOnly(event.target.value))} error={errorFor("maxAge")} />
          <TextField label={t("office.rulesCutoff")} type="date" value={cutoff} onChange={(event) => setCutoff(event.target.value)} error={errorFor("ageCutoffDate")} />
          <Button type="submit" pending={save.isPending}>
            {t("office.rulesSave")}
          </Button>
        </>
      )}
      {formError && !saved && <InlineAlert>{t(formError)}</InlineAlert>}
      {saved && <InlineAlert tone="success">{t("office.rulesSaved")}</InlineAlert>}
    </form>
  );
}
