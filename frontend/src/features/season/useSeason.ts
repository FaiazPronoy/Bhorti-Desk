import { useQuery } from "@tanstack/react-query";
import { admissionApi } from "../../api/admissionApiClient";
import type { AdmissionSession, SessionWindow } from "../../api/apiTypes";

export const seasonKey = ["season"] as const;

export function useSeason() {
  return useQuery({
    queryKey: seasonKey,
    queryFn: admissionApi.currentSeason,
    refetchInterval: 30_000,
    retry: false,
  });
}

// The server sends the window, but a page left open must change the moment the time
// passes, so it is worked out again from the server clock on every tick.
export function windowAt(session: AdmissionSession, now: number): SessionWindow {
  if (session.status === "CLOSED" || now >= Date.parse(session.closesAt)) return "ENDED";
  if (now < Date.parse(session.opensAt)) return "UPCOMING";
  return "OPEN";
}

export interface TimeParts {
  days: number;
  hours: number;
  minutes: number;
  seconds: number;
}

export function timeUntil(targetIso: string, now: number): TimeParts {
  const total = Math.max(0, Math.ceil((Date.parse(targetIso) - now) / 1000));
  return {
    days: Math.floor(total / 86_400),
    hours: Math.floor((total % 86_400) / 3600),
    minutes: Math.floor((total % 3600) / 60),
    seconds: total % 60,
  };
}
