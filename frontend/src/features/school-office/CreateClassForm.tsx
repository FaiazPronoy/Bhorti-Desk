import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRef, useState, type FormEvent } from "react";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey, isApiError } from "../../api/apiErrors";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { TextField } from "../../components/TextField";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";
import { classListKey } from "../classes/useClassList";

const MAX_NAME_LENGTH = 100;
const MAX_SEATS = 100_000;

interface FieldErrors {
  name?: string;
  totalSeats?: string;
}

export function CreateClassForm() {
  const { t } = useLanguage();
  const queryClient = useQueryClient();
  const nameInput = useRef<HTMLInputElement>(null);
  const [name, setName] = useState("");
  const [seats, setSeats] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<MessageKey | null>(null);
  const [createdName, setCreatedName] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: ({ name, totalSeats }: { name: string; totalSeats: number }) => admissionApi.createClass(name, totalSeats),
    onSettled: () => queryClient.invalidateQueries({ queryKey: classListKey }),
  });

  // The server checks everything again; this is only so people see mistakes early.
  function checkLocally(trimmedName: string, totalSeats: number): FieldErrors {
    const errors: FieldErrors = {};
    if (trimmedName.length === 0) errors.name = t("office.nameRequired");
    else if (trimmedName.length > MAX_NAME_LENGTH) errors.name = t("office.nameTooLong");
    if (!Number.isInteger(totalSeats) || totalSeats < 1 || totalSeats > MAX_SEATS) errors.totalSeats = t("office.seatsRange");
    return errors;
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setCreatedName(null);

    const trimmedName = name.trim();
    const totalSeats = Number(seats.trim());
    const errors = checkLocally(trimmedName, totalSeats);
    setFieldErrors(errors);
    if (errors.name || errors.totalSeats) return;

    create.mutate(
      { name: trimmedName, totalSeats },
      {
        onSuccess: (created) => {
          // Ready for the next class straight away.
          setName("");
          setSeats("");
          setFieldErrors({});
          setCreatedName(created.name);
          nameInput.current?.focus();
        },
        onError: (error) => {
          if (isApiError(error) && Object.keys(error.fieldErrors).length > 0) {
            setFieldErrors({
              name: error.fieldErrors.name,
              totalSeats: error.fieldErrors.totalSeats,
            });
          }
          setFormError(errorMessageKey(error));
        },
      },
    );
  }

  return (
    <form className="sheet office-form" onSubmit={onSubmit} noValidate>
      <h2 className="section-title">{t("office.formTitle")}</h2>
      <TextField
        ref={nameInput}
        label={t("office.className")}
        hint={t("office.classNameHint")}
        value={name}
        maxLength={MAX_NAME_LENGTH + 20}
        autoComplete="off"
        onChange={(event) => setName(event.target.value)}
        error={fieldErrors.name}
      />
      <TextField
        label={t("office.seats")}
        type="number"
        inputMode="numeric"
        min={1}
        max={MAX_SEATS}
        step={1}
        value={seats}
        onChange={(event) => setSeats(event.target.value)}
        error={fieldErrors.totalSeats}
        className="field--short"
      />
      <Button type="submit" pending={create.isPending}>
        {t("office.create")}
      </Button>
      {formError && <InlineAlert>{t(formError)}</InlineAlert>}
      {createdName && <InlineAlert tone="success">{t("office.created", { name: createdName })}</InlineAlert>}
    </form>
  );
}
