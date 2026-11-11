export type Language = "bn" | "en";

const banglaDigits = "০১২৩৪৫৬৭৮৯";

// Seat numbers follow the language. The countdown and the number box do not call
// this: they always use Latin digits so the mono font never falls back mid-second.
export function formatNumber(value: number, language: Language): string {
  const latin = String(value);
  if (language === "en") return latin;
  return latin.replace(/[0-9]/g, (digit) => banglaDigits[Number(digit)]);
}

// Clock time such as "10:42 AM", in the user's own time zone.
export function formatClockTime(iso: string, language: Language): string {
  return new Intl.DateTimeFormat(language === "bn" ? "bn-BD" : "en-GB", {
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(iso));
}

// "10 Nov, 10:42" for the list of applications.
export function formatDateTime(iso: string, language: Language): string {
  return new Intl.DateTimeFormat(language === "bn" ? "bn-BD" : "en-GB", {
    day: "numeric",
    month: "short",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(iso));
}
