import { useId, type InputHTMLAttributes, type Ref } from "react";

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  hint?: string;
  error?: string;
  ref?: Ref<HTMLInputElement>;
}

export function TextField({ label, hint, error, className, ...rest }: TextFieldProps) {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const describedBy = [hint ? hintId : "", error ? errorId : ""].filter(Boolean).join(" ") || undefined;

  return (
    <div className={`field ${className ?? ""}`}>
      <label htmlFor={id} className="field__label">
        {label}
      </label>
      {hint && (
        <p id={hintId} className="field__hint">
          {hint}
        </p>
      )}
      <input id={id} className="field__input" aria-invalid={error ? true : undefined} aria-describedby={describedBy} {...rest} />
      {error && (
        <p id={errorId} className="field__error">
          {error}
        </p>
      )}
    </div>
  );
}
