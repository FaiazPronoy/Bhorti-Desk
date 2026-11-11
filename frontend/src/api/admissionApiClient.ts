import { useSyncExternalStore } from "react";
import { ApiError } from "./apiErrors";
import type {
  AdmissionClass,
  ApiErrorBody,
  ClassSeatSummary,
  ConfirmedHold,
  CreatedHold,
  HoldDetails,
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
  method?: "GET" | "POST" | "DELETE";
  studentId?: string;
  body?: unknown;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = {};
  if (options.body !== undefined) headers["Content-Type"] = "application/json";
  // The birth registration number only ever travels in this header, never in the URL.
  if (options.studentId) headers["X-Student-Id"] = options.studentId;

  let response: Response;
  try {
    response = await fetch(apiBaseUrl() + path, {
      method: options.method ?? "GET",
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
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

  myHolds: async (studentId: string) => {
    const holds = await request<StudentHold[]>("/me/holds", { studentId });
    rememberServerTime(holds[0]?.serverTime);
    return holds;
  },
};
