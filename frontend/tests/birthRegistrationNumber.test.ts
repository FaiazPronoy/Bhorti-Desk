import { describe, expect, it } from "vitest";
import { maskBirthRegistrationNumber } from "../src/features/student-identity/birthRegistrationNumber";

describe("birth registration number on screen", () => {
  it("shows only the last 4 digits", () => {
    const masked = maskBirthRegistrationNumber("20150123456789012");
    expect(masked).toBe("•••• •••• •••• •9012");
    expect(masked).not.toContain("2015012345678");
  });
});
