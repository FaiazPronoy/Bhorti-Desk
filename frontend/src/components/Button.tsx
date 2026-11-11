import type { ButtonHTMLAttributes, Ref } from "react";
import { Spinner } from "./Spinner";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "quiet" | "danger";
  pending?: boolean;
  wide?: boolean;
  ref?: Ref<HTMLButtonElement>;
}

// While a request is on its way the button is disabled and shows a spinner, so a
// double tap cannot send the same request twice.
export function Button({ variant = "primary", pending = false, wide = false, disabled, className, children, type, ...rest }: ButtonProps) {
  const classes = ["button", `button--${variant}`, wide ? "button--wide" : "", className ?? ""].filter(Boolean).join(" ");
  return (
    <button type={type ?? "button"} className={classes} disabled={disabled || pending} aria-busy={pending || undefined} {...rest}>
      {pending && <Spinner />}
      <span>{children}</span>
    </button>
  );
}
