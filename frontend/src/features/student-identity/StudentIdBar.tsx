import { useId, useState } from "react";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";
import { BIRTH_REGISTRATION_LENGTH, checkBirthRegistrationNumber, countDigits } from "./birthRegistrationNumber";
import { useStudentId } from "./useStudentId";

// The number box at the top of every page. It is a plain input that is always visible
// and always editable. A valid number is saved as soon as it is complete.
export function StudentIdBar() {
  const { t } = useLanguage();
  const { studentId, saveStudentId, clearStudentId, numberWasAskedFor, inputRef } = useStudentId();
  const [draft, setDraft] = useState(studentId ?? "");
  const [touched, setTouched] = useState(false);
  const inputId = useId();
  const messageId = useId();
  const helpId = useId();

  const check = checkBirthRegistrationNumber(draft);
  const count = countDigits(draft);

  function onChange(value: string) {
    setDraft(value);
    const next = checkBirthRegistrationNumber(value);
    if (next.kind === "ok") {
      saveStudentId(next.digits);
    } else if (studentId) {
      // The saved number is being edited: it no longer counts until it is whole again.
      clearStudentId();
    }
  }

  function changeNumber() {
    setDraft("");
    setTouched(false);
    clearStudentId();
  }

  let message: MessageKey | null = null;
  if (check.kind === "badCharacters") message = "student.badCharacters";
  else if (check.kind === "tooLong") message = "student.tooLong";
  else if (check.kind === "tooShort" && touched) message = "student.tooShort";
  else if (check.kind === "empty" && numberWasAskedFor) message = "student.needed";
  else if (check.kind === "tooShort" && numberWasAskedFor) message = "student.tooShort";

  const isValid = check.kind === "ok";

  return (
    <div className="student-bar">
      <div className="student-bar__row">
        <label htmlFor={inputId} className="student-bar__label">
          {t("student.label")}
        </label>
        <div className="student-bar__field">
          <input
            ref={inputRef}
            id={inputId}
            className="student-bar__input"
            type="text"
            inputMode="numeric"
            autoComplete="off"
            spellCheck={false}
            placeholder={t("student.placeholder")}
            value={draft}
            onChange={(event) => onChange(event.target.value)}
            onBlur={() => setTouched(true)}
            aria-invalid={message !== null && message !== "student.needed" ? true : undefined}
            aria-describedby={`${messageId} ${helpId}`}
          />
          <span className={`student-bar__count ${isValid ? "student-bar__count--done" : ""}`} aria-hidden="true">
            {t("student.count", { count })}
          </span>
          {studentId && (
            <button type="button" className="button button--quiet student-bar__change" onClick={changeNumber}>
              {t("student.change")}
            </button>
          )}
        </div>
      </div>
      <p id={messageId} className={message ? "student-bar__message" : "student-bar__message student-bar__message--ok"} aria-live="polite">
        {message ? t(message) : isValid ? t("student.saved") : ""}
        <span className="sr-only">{` ${count} / ${BIRTH_REGISTRATION_LENGTH}`}</span>
      </p>
      <p id={helpId} className="student-bar__help">
        {t("student.help")} {t("student.sharedComputer")}
      </p>
    </div>
  );
}
