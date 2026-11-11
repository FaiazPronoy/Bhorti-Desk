import type { HoldStatus } from "../../api/apiTypes";

// The server's status can be up to five seconds old between polls. A HELD hold whose
// time has passed is EXPIRED, whatever the last answer said. Every surface that shows
// a hold uses this, so they all flip at the same second.
export function effectiveStatus(status: HoldStatus, expiresAt: string, nowMs: number): HoldStatus {
  if (status === "HELD" && Date.parse(expiresAt) <= nowMs) return "EXPIRED";
  return status;
}

export function secondsLeft(expiresAt: string, nowMs: number): number {
  return Math.max(0, Math.ceil((Date.parse(expiresAt) - nowMs) / 1000));
}

// Always Latin digits: "09:58".
export function formatCountdown(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}
