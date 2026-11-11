export const BIRTH_REGISTRATION_LENGTH = 17;

const banglaDigits = "০১২৩৪৫৬৭৮৯";

export type NumberCheck =
  | { kind: "empty" }
  | { kind: "badCharacters"; digits: string }
  | { kind: "tooShort"; digits: string }
  | { kind: "tooLong"; digits: string }
  | { kind: "ok"; digits: string };

// Guardians copy the number from a paper certificate, often in Bangla digits and with
// spaces or hyphens. Turn whatever they type into the 17 ASCII digits the API wants.
export function cleanDigits(input: string): string {
  return toLatinDigits(input).replace(/[\s-]/g, "");
}

export function toLatinDigits(input: string): string {
  return input.replace(/[০-৯]/g, (digit) => String(banglaDigits.indexOf(digit)));
}

export function checkBirthRegistrationNumber(input: string): NumberCheck {
  const digits = cleanDigits(input);
  if (digits.length === 0) return { kind: "empty" };
  if (!/^[0-9]+$/.test(digits)) return { kind: "badCharacters", digits };
  if (digits.length < BIRTH_REGISTRATION_LENGTH) return { kind: "tooShort", digits };
  if (digits.length > BIRTH_REGISTRATION_LENGTH) return { kind: "tooLong", digits };
  return { kind: "ok", digits };
}

// How many digits are typed so far, for the "n of 17" counter.
export function countDigits(input: string): number {
  return cleanDigits(input).replace(/[^0-9]/g, "").length;
}
