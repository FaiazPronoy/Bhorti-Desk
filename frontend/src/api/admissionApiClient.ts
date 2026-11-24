import { useSyncExternalStore } from "react";
import { ApiError } from "./apiErrors";
import type {
  AdmissionClass,
  AdmissionSession,
  ApiErrorBody,
  ApplicantInput,
  ApplicationDetails,
  ClassRules,
  ClassRulesForm,
  ClassSeatSummary,
  ConfirmedHold,
  CreatedHold,
  CurrentSeason,
  DocumentKind,
  HoldDetails,
  Me,
  NewStudent,
  Notice,
  OfficeApplication,
  WaitlistPlace,
  PaymentInfo,
  PaymentStart,
  SandboxPage,
  SessionForm,
  Slip,
  SlipCheck,
  Student,
  StudentHold,
} from "./apiTypes";
import { apiBaseUrl } from "../lib/runtimeConfig";
import { rememberServerTime } from "../lib/serverClock";

const REQUEST_TIMEOUT_MS = 10_000;

// ---- Can we reach the server? Read by the banner at the top of every page.

let serverReachable = true;
const listeners = new Set<() => void>();

function setServerReachable(value: boolean) {
  if (serverReachable === value) return;
  serverReachable = value;
  listeners.forEach((listener) => listener());
}

export function useServerReachable(): boolean {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => serverReachable,
  );
}

// ---- The one function that talks to the API.

interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "DELETE";
  studentId?: string;
  body?: unknown;
  idempotencyKey?: string;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  // Marks every call as coming from this app. The API refuses a signed-in request
  // without it, which stops forged form posts from other sites.
  const headers: Record<string, string> = { "X-Bhorti-Request": "1" };
  if (options.body !== undefined) headers["Content-Type"] = "application/json";
  // The birth registration number only ever travels in this header, never in the URL.
  if (options.studentId) headers["X-Student-Id"] = options.studentId;
  if (options.idempotencyKey) headers["Idempotency-Key"] = options.idempotencyKey;

  let response: Response;
  try {
    response = await fetch(apiBaseUrl() + path, {
      method: options.method ?? "GET",
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      // The session cookie is HttpOnly: the page never sees it, the browser just sends it.
      credentials: "include",
      signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
    });
  } catch {
    // No connection, DNS failure, CORS failure or the timeout.
    setServerReachable(false);
    throw new ApiError("network", 0);
  }

  if (response.status >= 500) {
    setServerReachable(false);
    const body = await readJson<ApiErrorBody>(response);
    // 503 SERVER_BUSY is the server telling us something useful, so keep its code.
    if (body?.code === "SERVER_BUSY") throw new ApiError("api", response.status, body);
    throw new ApiError("network", response.status, body);
  }

  setServerReachable(true);

  if (!response.ok) {
    const body = await readJson<ApiErrorBody>(response);
    throw new ApiError("api", response.status, body);
  }

  if (response.status === 204) return undefined as T;
  return (await readJson<T>(response)) as T;
}

// Files need a multipart body and come back as bytes, so they skip the JSON helper but
// keep the same headers, timeout and error handling.
async function sendFile(path: string, studentId: string, method: "GET" | "PUT", body?: FormData): Promise<Response> {
  let response: Response;
  try {
    response = await fetch(apiBaseUrl() + path, {
      method,
      headers: { "X-Bhorti-Request": "1", "X-Student-Id": studentId },
      body,
      credentials: "include",
      signal: AbortSignal.timeout(30_000),
    });
  } catch {
    setServerReachable(false);
    throw new ApiError("network", 0);
  }
  setServerReachable(response.status < 500);
  if (!response.ok) throw new ApiError(response.status >= 500 ? "network" : "api", response.status, await readJson<ApiErrorBody>(response));
  return response;
}

async function readJson<T>(response: Response): Promise<T | undefined> {
  try {
    return (await response.json()) as T;
  } catch {
    return undefined;
  }
}

// ---- The eight endpoints.

export const admissionApi = {
  createClass: (name: string, totalSeats: number) =>
    request<AdmissionClass>("/classes", { method: "POST", body: { name, totalSeats } }),

  listClasses: () => request<ClassSeatSummary[]>("/classes"),

  getClass: (classId: string) => request<ClassSeatSummary>(`/classes/${encodeURIComponent(classId)}`),

  applyForSeat: async (classId: string, studentId: string) => {
    const hold = await request<CreatedHold>(`/classes/${encodeURIComponent(classId)}/holds`, {
      method: "POST",
      studentId,
    });
    rememberServerTime(hold.serverTime);
    return hold;
  },

  getHold: async (holdId: string) => {
    const hold = await request<HoldDetails>(`/holds/${encodeURIComponent(holdId)}`);
    rememberServerTime(hold.serverTime);
    return hold;
  },

  payFee: (holdId: string, studentId: string) =>
    request<ConfirmedHold>(`/holds/${encodeURIComponent(holdId)}/confirm`, { method: "POST", studentId }),

  cancelHold: (holdId: string, studentId: string) =>
    request<void>(`/holds/${encodeURIComponent(holdId)}`, { method: "DELETE", studentId }),

  currentSeason: async () => {
    const season = await request<CurrentSeason>("/season/current");
    rememberServerTime(season.serverTime);
    return season;
  },

  officeSessions: () => request<AdmissionSession[]>("/office/sessions"),

  createSession: (form: SessionForm) => request<AdmissionSession>("/office/sessions", { method: "POST", body: form }),

  publishSession: (id: string) => request<AdmissionSession>(`/office/sessions/${encodeURIComponent(id)}/publish`, { method: "POST" }),

  closeSession: (id: string) => request<AdmissionSession>(`/office/sessions/${encodeURIComponent(id)}/close`, { method: "POST" }),

  addClassToSession: (id: string, classId: string) =>
    request<AdmissionSession>(`/office/sessions/${encodeURIComponent(id)}/classes`, { method: "POST", body: { classId } }),

  application: (holdId: string, studentId: string) =>
    request<ApplicationDetails>(`/applications/${encodeURIComponent(holdId)}`, { studentId }),

  saveApplicant: (holdId: string, studentId: string, applicant: ApplicantInput) =>
    request<ApplicationDetails>(`/applications/${encodeURIComponent(holdId)}/applicant`, { method: "PUT", studentId, body: applicant }),

  uploadDocument: async (holdId: string, studentId: string, kind: DocumentKind, file: File) => {
    const body = new FormData();
    body.append("file", file);
    await sendFile(`/applications/${encodeURIComponent(holdId)}/documents/${kind.toLowerCase()}`, studentId, "PUT", body);
  },

  // The file comes back as bytes and is shown through a short-lived object URL, because
  // an <img src> cannot send the X-Student-Id header.
  documentBlob: async (holdId: string, studentId: string, kind: DocumentKind) =>
    (await sendFile(`/applications/${encodeURIComponent(holdId)}/documents/${kind.toLowerCase()}`, studentId, "GET")).blob(),

  startPayment: (holdId: string, studentId: string, idempotencyKey: string) =>
    request<PaymentStart>(`/applications/${encodeURIComponent(holdId)}/payments`, { method: "POST", studentId, idempotencyKey }),

  sandboxPage: (paymentId: string) => request<SandboxPage>(`/sandbox-gateway/payments/${encodeURIComponent(paymentId)}`),

  sandboxDecide: (paymentId: string, outcome: "PAID" | "FAILED") =>
    request<{ status: PaymentInfo["status"] }>(`/sandbox-gateway/payments/${encodeURIComponent(paymentId)}/decide`, { method: "POST", body: { outcome } }),

  officeApplications: () => request<OfficeApplication[]>("/office/applications"),

  verifyApplication: (holdId: string) => request<OfficeApplication>(`/office/applications/${encodeURIComponent(holdId)}/verify`, { method: "POST" }),

  rejectApplication: (holdId: string, reason: string) =>
    request<OfficeApplication>(`/office/applications/${encodeURIComponent(holdId)}/reject`, { method: "POST", body: { reason } }),

  officeDocumentBlob: async (holdId: string, kind: DocumentKind) => {
    let response: Response;
    try {
      response = await fetch(`${apiBaseUrl()}/office/applications/${encodeURIComponent(holdId)}/documents/${kind.toLowerCase()}`, {
        headers: { "X-Bhorti-Request": "1" },
        credentials: "include",
        signal: AbortSignal.timeout(30_000),
      });
    } catch {
      throw new ApiError("network", 0);
    }
    if (!response.ok) throw new ApiError("api", response.status, await readJson<ApiErrorBody>(response));
    return response.blob();
  },

  myWaitlist: (studentId: string) => request<WaitlistPlace[]>("/me/waitlist", { studentId }),

  joinWaitlist: (classId: string, studentId: string) =>
    request<WaitlistPlace>(`/classes/${encodeURIComponent(classId)}/waitlist`, { method: "POST", studentId }),

  leaveWaitlist: (classId: string, studentId: string) =>
    request<void>(`/classes/${encodeURIComponent(classId)}/waitlist`, { method: "DELETE", studentId }),

  slip: (holdId: string, studentId: string) => request<Slip>(`/applications/${encodeURIComponent(holdId)}/slip`, { studentId }),

  verifySlip: (code: string) => request<SlipCheck>(`/verify/${encodeURIComponent(code)}`),

  myNotices: (studentId: string) => request<Notice[]>("/me/notices", { studentId }),

  classRules: () => request<ClassRules[]>("/season/class-rules"),

  saveClassRules: (classId: string, rules: ClassRulesForm) =>
    request<ClassRules>(`/office/classes/${encodeURIComponent(classId)}/rules`, { method: "PUT", body: rules }),

  me: () => request<Me>("/auth/me"),

  requestOtp: (phone: string) => request<void>("/auth/otp/request", { method: "POST", body: { phone } }),

  verifyOtp: (phone: string, code: string) => request<Me>("/auth/otp/verify", { method: "POST", body: { phone, code } }),

  staffLogin: (email: string, password: string) =>
    request<Me>("/auth/staff/login", { method: "POST", body: { email, password } }),

  logout: () => request<void>("/auth/logout", { method: "POST" }),

  myStudents: () => request<Student[]>("/me/students"),

  addStudent: (student: NewStudent) => request<Student>("/me/students", { method: "POST", body: student }),

  myHolds: async (studentId: string) => {
    const holds = await request<StudentHold[]>("/me/holds", { studentId });
    rememberServerTime(holds[0]?.serverTime);
    return holds;
  },
};
