import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { mockApi, renderApp, VALID_STUDENT_ID } from "./renderApp";

const qr = Array.from({ length: 21 }, (_, y) => Array.from({ length: 21 }, (_, x) => (x + y) % 3 === 0));

describe("admission slip", () => {
  it("draws the slip with the masked number, the serial and the QR", async () => {
    localStorage.setItem("bhorti-desk.student-id", VALID_STUDENT_ID);
    mockApi((url) => {
      if (url === "/applications/h1/slip") {
        return {
          status: 200,
          body: {
            holdId: "h1", serial: "BD-2026-0000042", className: "Class 1", maskedStudentId: "•••••••••••••9012",
            admissionStatus: "VERIFIED", studentNameBn: "আয়েশা রহমান", studentNameEn: "Ayesha Rahman", hasPhoto: false, verifyCode: "abc", qr,
          },
        };
      }
      if (url === "/me/holds") return { status: 200, body: [] };
    });
    renderApp("/applications/h1/slip");

    expect(await screen.findByRole("heading", { name: "Admission slip" })).toBeInTheDocument();
    expect(screen.getByText("BD-2026-0000042")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "QR code to check this slip" })).toBeInTheDocument();
    expect(document.body.textContent).not.toContain(VALID_STUDENT_ID);
  });

  it("says plainly when a code is forged", async () => {
    mockApi((url) => (url === "/verify/forged" ? { status: 404, body: { code: "SLIP_INVALID", message: "This slip code is not valid." } } : undefined));
    renderApp("/verify/forged");

    expect(await screen.findByText(/does not belong to any Bhorti Desk slip/)).toBeInTheDocument();
  });
});
