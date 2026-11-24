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
  rules: { classId: "c1", minAge: null, maxAge: null, ageCutoffDate: null, feeTaka: 1500 },
  applicant: null,
  documents: [],
  payment: null,
  review: null,
};

describe("sandbox payment", () => {
  it("starts one payment with an idempotency key and opens the sandbox page", async () => {
    localStorage.setItem("bhorti-desk.student-id", VALID_STUDENT_ID);
    const { calls } = mockApi((url, init) => {
      if (url === "/holds/h1") return { status: 200, body: { holdId: "h1", classId: "c1", className: "Class 1", status: "HELD", expiresAt } };
      if (url === "/applications/h1") return { status: 200, body: application };
      if (url === "/applications/h1/payments" && init.method === "POST") {
        return { status: 200, body: { paymentId: "p1", amountTaka: 1500, gateway: "SANDBOX", status: "PENDING", hostedPageUrl: "/pay/p1" } };
      }
      if (url === "/sandbox-gateway/payments/p1") {
        return { status: 200, body: { paymentId: "p1", holdId: "h1", className: "Class 1", serial: "BD-2026-0000042", amountTaka: 1500, status: "PENDING" } };
      }
      if (url === "/classes") return { status: 200, body: [] };
      if (url === "/me/holds") return { status: 200, body: [] };
    });
    const user = userEvent.setup();
    renderApp("/applications/h1");

    await user.click(await screen.findByRole("button", { name: "Pay admission fee" }));

    expect(await screen.findByText("Sandbox payment. This is a test gateway. No real money moves.")).toBeInTheDocument();
    expect(screen.getByText("1500 taka")).toBeInTheDocument();
    const start = calls.find((call) => call.url.endsWith("/applications/h1/payments"))!;
    expect((start.init.headers as Record<string, string>)["Idempotency-Key"]).toMatch(/^[0-9a-f-]{36}$/);
    // The original confirm endpoint is not used for a class with a fee.
    await waitFor(() => expect(calls.some((call) => call.url.endsWith("/confirm"))).toBe(false));
  });

  it("shows a plain failure message when the sandbox fails", async () => {
    mockApi((url, init) => {
      if (url === "/sandbox-gateway/payments/p1") {
        return { status: 200, body: { paymentId: "p1", holdId: "h1", className: "Class 1", serial: "BD-2026-0000042", amountTaka: 1500, status: "PENDING" } };
      }
      if (url === "/sandbox-gateway/payments/p1/decide" && init.method === "POST") return { status: 200, body: { status: "FAILED" } };
      if (url === "/classes") return { status: 200, body: [] };
    });
    const user = userEvent.setup();
    renderApp("/pay/p1");

    await user.click(await screen.findByRole("button", { name: "Make it fail (sandbox)" }));

    expect(await screen.findByText(/The sandbox payment failed/)).toBeInTheDocument();
  });
});
