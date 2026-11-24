import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { mockApi, renderApp, VALID_STUDENT_ID } from "./renderApp";

const expiresAt = new Date(Date.now() + 5 * 60_000).toISOString().replace(/\.\d+Z$/, "Z");
const application = {
  holdId: "h1",
  serial: "BD-2026-0000042",
  classId: "c1",
  className: "Class 1",
  status: "HELD",
  expiresAt,
  serverTime: new Date().toISOString(),
  rules: null,
  applicant: null,
  documents: [],
};

function api(onSave: () => { status: number; body?: unknown }) {
  localStorage.setItem("bhorti-desk.student-id", VALID_STUDENT_ID);
  return mockApi((url, init) => {
    if (url === "/holds/h1") return { status: 200, body: { holdId: "h1", classId: "c1", className: "Class 1", status: "HELD", expiresAt } };
    if (url === "/applications/h1") return { status: 200, body: application };
    if (url === "/applications/h1/applicant" && init.method === "PUT") return onSave();
    if (url === "/classes") return { status: 200, body: [] };
    if (url === "/me/holds") return { status: 200, body: [] };
  });
}

describe("applicant details", () => {
  it("shows the real serial from the application", async () => {
    api(() => ({ status: 200, body: application }));
    renderApp("/applications/h1");

    expect(await screen.findByText("BD-2026-0000042")).toBeInTheDocument();
  });

  it("shows a server field error in the chosen language", async () => {
    api(() => ({
      status: 400,
      body: { code: "INVALID_REQUEST", message: "Some information is not correct.", fieldErrors: { dateOfBirth: "applicant.error.tooYoung" } },
    }));
    const user = userEvent.setup();
    renderApp("/applications/h1");

    await user.click(await screen.findByRole("button", { name: "Save details" }));

    expect(await screen.findByText("The student is younger than this class allows on the cut-off date.")).toBeInTheDocument();
  });

  it("sends Bangla digits in the phone as plain digits, with the number only in the header", async () => {
    const { calls } = api(() => ({ status: 200, body: application }));
    const user = userEvent.setup();
    renderApp("/applications/h1");

    await user.type(await screen.findByLabelText("Guardian's mobile number"), "০১৭১২৩৪৫৬৭৮");
    await user.click(screen.getByRole("button", { name: "Save details" }));

    await waitFor(() => expect(calls.some((call) => call.url.endsWith("/applicant"))).toBe(true));
    const save = calls.find((call) => call.url.endsWith("/applicant"))!;
    const body = JSON.parse(String(save.init.body));
    expect(body.guardianPhone).toBe("01712345678");
    // An empty date must be null, or the API rejects the whole body with one vague error.
    expect(body.dateOfBirth).toBeNull();
    expect(calls.every((call) => !call.url.includes(VALID_STUDENT_ID))).toBe(true);
    expect(await screen.findByText("Details saved.")).toBeInTheDocument();
  });

  it("stops a photo that is too large before it is sent", async () => {
    const { calls } = api(() => ({ status: 200, body: application }));
    const user = userEvent.setup();
    renderApp("/applications/h1");

    const big = new File([new Uint8Array(400 * 1024)], "big.png", { type: "image/png" });
    await user.upload(await screen.findByLabelText("Add Student's photo"), big);

    expect(await screen.findByText("The photo is too large. Use one under 300 KB.")).toBeInTheDocument();
    expect(calls.some((call) => call.url.includes("/documents/"))).toBe(false);
  });
});
