import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { mockApi, renderApp } from "./renderApp";

const report = {
  applicationsPerHour: [
    { hour: "2026-11-24T08:00:00Z", applications: 4 },
    { hour: "2026-11-24T09:00:00Z", applications: 12 },
  ],
  funnel: { applied: 16, onHoldNow: 3, paid: 9, verified: 5, rejected: 1 },
  classes: [{ id: "c1", name: "Class 1", totalSeats: 10, inUse: 9, closed: false, waiting: 4 }],
  sandboxRevenueTaka: 18000,
};

describe("school office", () => {
  it("shows the dashboard with the chart, its table, the funnel and the fill per class", async () => {
    mockApi((url) => (url === "/office/reports" ? { status: 200, body: report } : url === "/classes" ? { status: 200, body: [] } : undefined));
    renderApp("/school-office?tab=dashboard");

    expect(await screen.findByRole("img", { name: "Bar chart of applications per hour" })).toBeInTheDocument();
    expect(screen.getByText("Show as a table")).toBeInTheDocument();
    expect(screen.getByText("Verified")).toBeInTheDocument();
    expect(screen.getByText("Sandbox payments so far: 18000 taka. No real money.")).toBeInTheDocument();
    expect(screen.getByRole("rowheader", { name: "Class 1" })).toBeInTheDocument();
  });

  it("asks for a staff sign-in when the office API says 401", async () => {
    mockApi((url) =>
      url === "/office/audit" ? { status: 401, body: { code: "SIGN_IN_REQUIRED", message: "Sign in first." } } : url === "/classes" ? { status: 200, body: [] } : undefined,
    );
    renderApp("/school-office?tab=audit");

    expect(await screen.findByText("Sign in as school office staff to see this.")).toBeInTheDocument();
  });

  it("keeps the original create class form on the first tab without signing in", async () => {
    mockApi((url) => (url === "/classes" ? { status: 200, body: [] } : undefined));
    renderApp("/school-office");

    expect(await screen.findByLabelText("Class name")).toBeInTheDocument();
  });
});
