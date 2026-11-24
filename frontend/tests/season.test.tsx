import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { timeUntil, windowAt } from "../src/features/season/useSeason";
import type { AdmissionSession } from "../src/api/apiTypes";
import { mockApi, renderApp } from "./renderApp";

const inMinutes = (minutes: number) => new Date(Date.now() + minutes * 60_000).toISOString().replace(/\.\d+Z$/, "Z");
const oneClass = { id: "c1", name: "Class 1", totalSeats: 10, heldSeats: 0, confirmedSeats: 0, availableSeats: 10 };
const session = (opensAt: string, closesAt: string): AdmissionSession => ({
  id: "s1",
  academicYear: 2027,
  name: "Admission 2027",
  opensAt,
  closesAt,
  status: "PUBLISHED",
  window: "UPCOMING",
  classIds: ["c1"],
});

function seasonApi(current: AdmissionSession | null) {
  mockApi((url) => {
    if (url === "/classes") return { status: 200, body: [oneClass] };
    if (url === "/season/current") return { status: 200, body: { session: current, serverTime: new Date().toISOString() } };
  });
}

describe("admission season", () => {
  it("works out the window from the clock it is given", () => {
    const s = session("2030-01-10T00:00:00Z", "2030-01-20T00:00:00Z");
    expect(windowAt(s, Date.parse("2030-01-09T23:59:59Z"))).toBe("UPCOMING");
    expect(windowAt(s, Date.parse("2030-01-10T00:00:00Z"))).toBe("OPEN");
    expect(windowAt(s, Date.parse("2030-01-20T00:00:00Z"))).toBe("ENDED");
    expect(windowAt({ ...s, status: "CLOSED" }, Date.parse("2030-01-15T00:00:00Z"))).toBe("ENDED");
  });

  it("splits the time left into days, hours, minutes and seconds", () => {
    expect(timeUntil("2030-01-02T01:02:03Z", Date.parse("2030-01-01T00:00:00Z"))).toEqual({ days: 1, hours: 1, minutes: 2, seconds: 3 });
  });

  it("shows the countdown and blocks Apply before the session opens", async () => {
    seasonApi(session(inMinutes(90), inMinutes(600)));
    renderApp("/");

    expect(await screen.findByRole("heading", { name: "Admission 2027" })).toBeInTheDocument();
    expect(screen.getByRole("timer", { name: "Time until applications open" })).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Not open yet" })).toBeDisabled();
  });

  it("lets a guardian apply while the session is open", async () => {
    seasonApi(session(inMinutes(-5), inMinutes(600)));
    renderApp("/");

    expect(await screen.findByRole("timer", { name: "Time until applications close" })).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Apply" })).toBeEnabled();
  });

  it("keeps the page as it was when there is no session", async () => {
    seasonApi(null);
    renderApp("/");

    expect(await screen.findByRole("button", { name: "Apply" })).toBeEnabled();
    expect(screen.queryByRole("timer")).not.toBeInTheDocument();
  });
});
