import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it } from "vitest";
import { checkBirthRegistrationNumber, cleanDigits } from "../src/features/student-identity/birthRegistrationNumber";
import { asGuardian, mockApi, renderApp, turnSignInOn, VALID_STUDENT_ID } from "./renderApp";

const oneClass = [{ id: "c1", name: "Class 6", totalSeats: 10, heldSeats: 0, confirmedSeats: 0, availableSeats: 10 }];

describe("birth registration number", () => {
  it("turns Bangla digits into ASCII and drops spaces and hyphens", () => {
    expect(cleanDigits("২০১৫ ০১২৩-৪৫৬৭ ৮৯০১২")).toBe(VALID_STUDENT_ID);
    expect(checkBirthRegistrationNumber("২০১৫০১২৩৪৫৬৭৮৯০১২")).toEqual({ kind: "ok", digits: VALID_STUDENT_ID });
  });

  it("rejects 16 and 18 digits, and letters", () => {
    expect(checkBirthRegistrationNumber("2015012345678901").kind).toBe("tooShort");
    expect(checkBirthRegistrationNumber("201501234567890123").kind).toBe("tooLong");
    expect(checkBirthRegistrationNumber("2015abc").kind).toBe("badCharacters");
  });
});

describe("account", () => {
  beforeEach(() => turnSignInOn());

  it("sends someone who is not signed in to the sign-in page when they press Apply", async () => {
    mockApi((url) => (url === "/classes" ? { status: 200, body: oneClass } : undefined));
    const user = userEvent.setup();
    renderApp("/");

    await user.click(await screen.findByRole("button", { name: "Apply" }));

    expect(await screen.findByRole("heading", { name: "Sign in with your mobile" })).toBeInTheDocument();
  });

  it("signs in with a mobile number and a code, with the app header on every call", async () => {
    let signedIn = false;
    const { calls } = mockApi((url) => {
      if (url === "/auth/otp/request") return { status: 202 };
      if (url === "/auth/otp/verify") {
        signedIn = true;
        return { status: 200, body: { kind: "GUARDIAN", id: "g1", role: null } };
      }
      if (url === "/auth/me" && signedIn) return { status: 200, body: { kind: "GUARDIAN", id: "g1", role: null } };
      if (url === "/me/students") return { status: 200, body: [] };
      if (url === "/classes") return { status: 200, body: oneClass };
    });
    const user = userEvent.setup();
    renderApp("/login");

    await user.type(await screen.findByLabelText("Mobile number"), "০১৭১২৩৪৫৬৭৮");
    await user.click(screen.getByRole("button", { name: "Send code" }));
    await user.type(await screen.findByLabelText("6 digit code"), "123456");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("heading", { name: "My students" })).toBeInTheDocument();
    const request = calls.find((call) => call.url.endsWith("/auth/otp/request"))!;
    // Bangla digits are turned into the plain number the API expects.
    expect(JSON.parse(String(request.init.body))).toEqual({ phone: "01712345678" });
    expect(calls.every((call) => (call.init.headers as Record<string, string>)["X-Bhorti-Request"] === "1")).toBe(true);
    expect(calls.every((call) => call.init.credentials === "include")).toBe(true);
  });

  it("applies for the student chosen in the header, never with the number in the URL", async () => {
    const { calls } = mockApi(
      asGuardian((url, init) => {
        if (url === "/classes") return { status: 200, body: oneClass };
        if (url === "/me/holds") return { status: 200, body: [] };
        if (url === "/classes/c1/holds" && init.method === "POST") {
          return { status: 201, body: { holdId: "h1", classId: "c1", studentId: VALID_STUDENT_ID, status: "HELD", expiresAt: "2030-01-01T00:10:00Z" } };
        }
        if (url === "/holds/h1") {
          return { status: 200, body: { holdId: "h1", classId: "c1", status: "HELD", expiresAt: "2030-01-01T00:10:00Z" } };
        }
      }),
    );
    const user = userEvent.setup();
    renderApp("/");

    expect(await screen.findByRole("combobox", { name: "Applying for" })).toHaveDisplayValue("Ayesha Rahman (9012)");
    await user.click(await screen.findByRole("button", { name: "Apply" }));

    await waitFor(() => expect(calls.some((call) => call.url.endsWith("/classes/c1/holds"))).toBe(true));
    const apply = calls.find((call) => call.url.endsWith("/classes/c1/holds"))!;
    expect((apply.init.headers as Record<string, string>)["X-Student-Id"]).toBe(VALID_STUDENT_ID);
    expect(calls.every((call) => !call.url.includes(VALID_STUDENT_ID))).toBe(true);
  });

  it("shows the running hold to a returning guardian", async () => {
    const soon = new Date(Date.now() + 5 * 60_000).toISOString().replace(/\.\d+Z$/, "Z");
    mockApi(
      asGuardian((url) => {
        if (url === "/classes") return { status: 200, body: oneClass };
        if (url === "/me/holds") {
          return { status: 200, body: [{ holdId: "h1", classId: "c1", className: "Class 6", status: "HELD", expiresAt: soon }] };
        }
      }),
    );
    renderApp("/");

    expect(await screen.findByText("Your seat in Class 6 is on hold.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Pay now" })).toHaveAttribute("href", "/applications/h1");
  });

  it("keeps the school office form away from guardians", async () => {
    mockApi(asGuardian((url) => (url === "/classes" ? { status: 200, body: oneClass } : undefined)));
    renderApp("/school-office");

    expect(await screen.findByText("Only school office staff can open this page.")).toBeInTheDocument();
    expect(screen.queryByLabelText("Class name")).not.toBeInTheDocument();
  });
});
