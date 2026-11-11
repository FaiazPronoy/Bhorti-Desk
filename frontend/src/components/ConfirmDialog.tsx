import { useEffect, useId, useRef, type KeyboardEvent } from "react";
import { Button } from "./Button";

interface ConfirmDialogProps {
  open: boolean;
  question: string;
  confirmLabel: string;
  cancelLabel: string;
  pending?: boolean;
  onConfirm: () => void;
  onClose: () => void;
}

// A small modal for "are you sure?". Focus starts on the safe choice, Escape closes it,
// and Tab stays inside it while it is open.
export function ConfirmDialog({ open, question, confirmLabel, cancelLabel, pending, onConfirm, onClose }: ConfirmDialogProps) {
  const questionId = useId();
  const safeButton = useRef<HTMLButtonElement>(null);
  const panel = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const returnFocusTo = document.activeElement as HTMLElement | null;
    safeButton.current?.focus();
    return () => returnFocusTo?.focus();
  }, [open]);

  if (!open) return null;

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === "Escape") {
      event.stopPropagation();
      onClose();
      return;
    }
    if (event.key !== "Tab" || !panel.current) return;
    const buttons = Array.from(panel.current.querySelectorAll<HTMLButtonElement>("button:not(:disabled)"));
    if (buttons.length === 0) return;
    const first = buttons[0];
    const last = buttons[buttons.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  return (
    <div className="dialog-backdrop" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <div ref={panel} className="dialog" role="alertdialog" aria-modal="true" aria-labelledby={questionId} onKeyDown={onKeyDown}>
        <p id={questionId} className="dialog__question">
          {question}
        </p>
        <div className="dialog__actions">
          <Button ref={safeButton} variant="secondary" onClick={onClose} disabled={pending}>
            {cancelLabel}
          </Button>
          <Button variant="danger" pending={pending} onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
