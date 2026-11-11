import { act, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { formatCountdown, secondsLeft } from "../src/features/applications/holdStatus";
import { mockApi, renderApp, VALID_STUDENT_ID } from "./renderApp";

// Whole seconds, like the API sends them.
const isoInSeconds = (ms: number) => new Date(ms).toISOString().replace(/\.\d+Z$/, "Z");

function holdApi(expiresAt: string, serverTime: string) {
  return mockApi((url) => {
    if (url === "/holds/h1") {
      // The server keeps saying HELD for a moment after the time passes, as it may
      // between polls. The page must not believe it.
      return { status: 200, body: { holdId: "h1", classId: "c1", className: "Class 6", status: "HELD", expiresAt, serverTime } };
    }
    if (url === "/classes") {
      return { status: 200, body: [{ id: "c1", name: "Class 6", totalSeats: 1, heldSeats: 1, confirmedSeats: 0, availableSeats: 0 }] };
    }
    if (url === "/me/holds") return { status: 200, body: [] };
  });
}

describe("countdown", () => {
  it("prints minutes and seconds with Latin digits", () => {
    expect(formatCountdown(600)).toBe("10:00");
    expect(formatCountdown(59)).toBe("00:59");
    expect(secondsLeft("2030-01-01T00:00:10Z", Date.parse("2030-01-01T00:00:00Z"))).toBe(10);
  });

  it("reaches zero, shows EXPIRED and disables the pay button without a reload", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    localStorage.setItem("first-bench.student-id", VALID_STUDENT_ID);
    const start = Date.now();
    holdApi(isoInSeconds(start + 3000), isoInSeconds(start));

    renderApp("/applications/h1");

    expect(await screen.findByText("Seat on hold (HELD)")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Pay admission fee" })).toBeEnabled();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(4000);
    });

    expect(screen.getByText("Time over (EXPIRED)")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Pay admission fee" })).toBeDisabled();
    expect(screen.queryByText("Seat on hold (HELD)")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancel" })).not.toBeInTheDocument();

    // Later polls that still say HELD must not bring the old status back.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(6000);
    });
    expect(screen.getByText("Time over (EXPIRED)")).toBeInTheDocument();
  });

  it("shows EXPIRED on first load for a hold that has already run out", async () => {
    const now = Date.now();
    holdApi(isoInSeconds(now - 60_000), isoInSeconds(now));

    renderApp("/applications/h1");

    expect(await screen.findByText("Time over (EXPIRED)")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Pay admission fee" })).toBeDisabled();
  });

  it("follows the server clock, not the device clock", async () => {
    // The device is five minutes behind the server. The hold ends 30 seconds after the
    // server's "now", so the countdown must say 00:30, not 05:30.
    const deviceNow = Date.now();
    const serverNow = deviceNow + 5 * 60_000;
    holdApi(isoInSeconds(serverNow + 30_000), isoInSeconds(serverNow));

    renderApp("/applications/h1");

    const timer = await screen.findByRole("timer");
    expect(timer.textContent).toMatch(/^00:(29|30)$/);
  });
});
