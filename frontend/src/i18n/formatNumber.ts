export type Language = "bn" | "en";

const banglaDigits = "০১২৩৪৫৬৭৮৯";

// Seat numbers follow the language. The countdown, the number box and the serial do
// not call this: they keep Latin tabular digits so they never jump in width.
export function formatNumber(value: number, language: Language): string {
  const latin = String(value);
  if (language === "en") return latin;
  return latin.replace(/[0-9]/g, (digit) => banglaDigits[Number(digit)]);
}

// Clock time such as "17:42" in 24 hours, so Bangla never ends in an English "PM".
export function formatClockTime(iso: string, language: Language): string {
  return new Intl.DateTimeFormat(language === "bn" ? "bn-BD" : "en-GB", {
    hour: "numeric",
    minute: "2-digit",
    hourCycle: "h23",
  }).format(new Date(iso));
}

// "10 Nov, 17:42" for the list of applications.
export function formatDateTime(iso: string, language: Language): string {
  return new Intl.DateTimeFormat(language === "bn" ? "bn-BD" : "en-GB", {
    day: "numeric",
    month: "short",
    hour: "numeric",
    minute: "2-digit",
    hourCycle: "h23",
  }).format(new Date(iso));
}
