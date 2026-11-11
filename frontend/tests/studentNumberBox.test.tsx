import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { checkBirthRegistrationNumber, cleanDigits } from "../src/features/student-identity/birthRegistrationNumber";
import { mockApi, renderApp, VALID_STUDENT_ID } from "./renderApp";

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

describe("number box", () => {
  it("shows the count, says when 16 digits are too short, and sends the clean number in the header", async () => {
    const { calls } = mockApi((url, init) => {
      if (url === "/classes") return { status: 200, body: oneClass };
      if (url === "/me/holds") return { status: 200, body: [] };
      if (url === "/classes/c1/holds" && init.method === "POST") {
        return {
          status: 201,
          body: { holdId: "h1", classId: "c1", studentId: VALID_STUDENT_ID, status: "HELD", expiresAt: "2030-01-01T00:10:00Z" },
        };
      }
      if (url === "/holds/h1") {
        return { status: 200, body: { holdId: "h1", classId: "c1", status: "HELD", expiresAt: "2030-01-01T00:10:00Z" } };
      }
    });
    const user = userEvent.setup();
    renderApp("/");

    const box = screen.getByLabelText("Student's birth registration number");
    await user.type(box, "2015 0123 4567 890");
    expect(screen.getByText("15 of 17")).toBeInTheDocument();

    await user.type(box, "1");
    await user.tab();
    expect(screen.getByText("Too short. The number has 17 digits.")).toBeInTheDocument();

    await user.clear(box);
    await user.type(box, "২০১৫-০১২৩-৪৫৬৭-৮৯০১২");
    expect(screen.getByText("17 of 17")).toBeInTheDocument();
    expect(localStorage.getItem("bhorti-desk.student-id")).toBe(VALID_STUDENT_ID);

    await user.click(await screen.findByRole("button", { name: "Apply" }));

    await waitFor(() => expect(calls.some((call) => call.url.endsWith("/classes/c1/holds"))).toBe(true));
    const applyCall = calls.find((call) => call.url.endsWith("/classes/c1/holds"))!;
    expect((applyCall.init.headers as Record<string, string>)["X-Student-Id"]).toBe(VALID_STUDENT_ID);
    // The number never goes into a URL.
    expect(calls.every((call) => !call.url.includes(VALID_STUDENT_ID))).toBe(true);
  });

  it("moves focus to the box and says why when Apply is pressed without a number", async () => {
    mockApi((url) => (url === "/classes" ? { status: 200, body: oneClass } : undefined));
    const user = userEvent.setup();
    renderApp("/");

    await user.click(await screen.findByRole("button", { name: "Apply" }));

    expect(screen.getByLabelText("Student's birth registration number")).toHaveFocus();
    expect(screen.getByText("Enter the student's birth registration number first.")).toBeInTheDocument();
  });

  it("reads a stored number on load and shows the running hold", async () => {
    localStorage.setItem("bhorti-desk.student-id", VALID_STUDENT_ID);
    const soon = new Date(Date.now() + 5 * 60_000).toISOString().replace(/\.\d+Z$/, "Z");
    mockApi((url) => {
      if (url === "/classes") return { status: 200, body: oneClass };
      if (url === "/me/holds") {
        return { status: 200, body: [{ holdId: "h1", classId: "c1", className: "Class 6", status: "HELD", expiresAt: soon }] };
      }
    });
    renderApp("/");

    expect(screen.getByLabelText("Student's birth registration number")).toHaveValue(VALID_STUDENT_ID);
    expect(await screen.findByText("Your seat in Class 6 is on hold.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Pay now" })).toHaveAttribute("href", "/applications/h1");
  });
});
