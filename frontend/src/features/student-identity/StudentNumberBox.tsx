import { useId, useState } from "react";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";
import { BIRTH_REGISTRATION_LENGTH, checkBirthRegistrationNumber, countDigits, toLatinDigits } from "./birthRegistrationNumber";
import { useStudentId } from "./useStudentId";

// The number box used when sign-in is off. A valid number is saved as soon as it is
// complete. Once saved it is hidden on screen until the guardian presses Show.
export function StudentNumberBox() {
  const { t } = useLanguage();
  const { studentId, saveStudentId, clearStudentId, numberWasAskedFor, inputRef } = useStudentId();
  const [draft, setDraft] = useState(studentId ?? "");
  const [touched, setTouched] = useState(false);
  const [focused, setFocused] = useState(false);
  const [revealed, setRevealed] = useState(false);
  const inputId = useId();
  const messageId = useId();

  const check = checkBirthRegistrationNumber(draft);
  const count = countDigits(draft);
  const isValid = check.kind === "ok";
  const hidden = isValid && !focused && !revealed;

  function onChange(typed: string) {
    // The box always shows Latin digits, so Bangla digits are swapped as they are typed.
    const value = toLatinDigits(typed);
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
    setRevealed(false);
    clearStudentId();
  }

  let message: MessageKey | null = null;
  if (check.kind === "badCharacters") message = "student.badCharacters";
  else if (check.kind === "tooLong") message = "student.tooLong";
  else if (check.kind === "tooShort" && touched) message = "student.tooShort";
  else if (check.kind === "empty" && numberWasAskedFor) message = "student.needed";
  else if (check.kind === "tooShort" && numberWasAskedFor) message = "student.tooShort";

  return (
    <div className={`student-bar ${isValid ? "student-bar--saved" : ""}`}>
      <div className="student-bar__row">
        <label htmlFor={inputId} className="student-bar__label">
          {t("student.label")}
        </label>
        <div className="student-bar__field">
          <input
            ref={inputRef}
            id={inputId}
            className={`student-bar__input ${hidden ? "student-bar__input--hidden" : ""}`}
            type="text"
            inputMode="numeric"
            autoComplete="off"
            spellCheck={false}
            placeholder={t("student.placeholder")}
            value={draft}
            onChange={(event) => onChange(event.target.value)}
            onFocus={() => setFocused(true)}
            onBlur={() => {
              setFocused(false);
              setTouched(true);
            }}
            aria-invalid={message !== null && message !== "student.needed" ? true : undefined}
            aria-describedby={messageId}
          />
          <span className={`student-bar__count ${isValid ? "student-bar__count--done" : ""}`} aria-hidden="true">
            {hidden && studentId ? t("student.lastFour", { digits: studentId.slice(-4) }) : t("student.count", { count })}
          </span>
          {studentId && (
            <>
              <button type="button" className="button button--quiet" aria-pressed={revealed} onClick={() => setRevealed((value) => !value)}>
                {revealed ? t("student.hide") : t("student.show")}
              </button>
              <button type="button" className="button button--quiet student-bar__change" onClick={changeNumber}>
                {t("student.change")}
              </button>
            </>
          )}
        </div>
      </div>
      <p id={messageId} className={message ? "student-bar__message" : "student-bar__message student-bar__message--ok"} aria-live="polite">
        {message ? t(message) : isValid ? t("student.saved") : ""}
        <span className="sr-only">{` ${count} / ${BIRTH_REGISTRATION_LENGTH}`}</span>
      </p>
      <details className="student-bar__more">
        <summary>{t("student.howItWorks")}</summary>
        <p>
          {t("student.help")} {t("student.sharedComputer")}
        </p>
      </details>
    </div>
  );
}
