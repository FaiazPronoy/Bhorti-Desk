import type { ApiErrorBody } from "./apiTypes";
import type { MessageKey } from "../i18n/en";

// One error type for everything the API client can fail with. "network" covers no
// connection, the 10 second timeout and any 5xx: in all three the server did not give
// us a usable answer, and the user sees the same message.
export class ApiError extends Error {
  readonly kind: "api" | "network";
  readonly status: number;
  readonly code: string;
  readonly fieldErrors: Record<string, string>;
  readonly holdId?: string;

  constructor(kind: "api" | "network", status: number, body?: Partial<ApiErrorBody>) {
    super(body?.message ?? (kind === "network" ? "Network error" : `HTTP ${status}`));
    this.kind = kind;
    this.status = status;
    this.code = body?.code ?? (kind === "network" ? "NETWORK" : "UNKNOWN");
    this.fieldErrors = body?.fieldErrors ?? {};
    this.holdId = body?.holdId;
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}

const messageByCode: Record<string, MessageKey> = {
  CLASS_FULL: "error.classFull",
  ALREADY_APPLIED: "error.alreadyApplied",
  HOLD_EXPIRED: "error.holdExpired",
  HOLD_CANCELLED: "error.holdCancelled",
  HOLD_ALREADY_CONFIRMED: "error.holdAlreadyConfirmed",
  HOLD_NOT_OWNED: "error.holdNotOwned",
  STUDENT_ID_INVALID: "error.studentIdInvalid",
  STUDENT_ID_REQUIRED: "error.studentIdRequired",
  INVALID_REQUEST: "error.invalidRequest",
  CLASS_NOT_FOUND: "error.notFound",
  HOLD_NOT_FOUND: "error.notFound",
  NOT_FOUND: "error.notFound",
  SERVER_BUSY: "error.serverBusy",
  NETWORK: "error.network",
};

// The plain words to show for an error, as a dictionary key.
export function errorMessageKey(error: unknown): MessageKey {
  if (isApiError(error)) {
    return messageByCode[error.code] ?? "error.unknown";
  }
  return "error.unknown";
}
