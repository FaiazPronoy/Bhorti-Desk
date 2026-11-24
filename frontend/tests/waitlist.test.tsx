import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { mockApi, renderApp, VALID_STUDENT_ID } from "./renderApp";

const fullClass = { id: "c1", name: "Class 1", totalSeats: 1, heldSeats: 1, confirmedSeats: 0, availableSeats: 0 };

describe("waiting list", () => {
  it("lets a guardian join the line of a full class, and the Full button stays disabled", async () => {
    localStorage.setItem("bhorti-desk.student-id", VALID_STUDENT_ID);
    let joined = false;
    const { calls } = mockApi((url, init) => {
      if (url === "/classes") return { status: 200, body: [fullClass] };
      if (url === "/me/holds") return { status: 200, body: [] };
      if (url === "/me/waitlist") {
        return { status: 200, body: joined ? [{ entryId: 1, classId: "c1", className: "Class 1", status: "WAITING", position: 3, offeredHoldId: null }] : [] };
      }
      if (url === "/classes/c1/waitlist" && init.method === "POST") {
        joined = true;
        return { status: 201, body: { entryId: 1, classId: "c1", className: "Class 1", status: "WAITING", position: 3, offeredHoldId: null } };
      }
    });
    const user = userEvent.setup();
    renderApp("/");

    expect(await screen.findByRole("button", { name: "Full" })).toBeDisabled();
    await user.click(await screen.findByRole("button", { name: "Join the waiting list" }));

    expect(await screen.findByText("Number 3 in line")).toBeInTheDocument();
    const join = calls.find((call) => call.url.endsWith("/classes/c1/waitlist"))!;
    expect((join.init.headers as Record<string, string>)["X-Student-Id"]).toBe(VALID_STUDENT_ID);
  });

  it("shows an offered seat and the notices on My applications", async () => {
    localStorage.setItem("bhorti-desk.student-id", VALID_STUDENT_ID);
    mockApi((url) => {
      if (url === "/me/holds") return { status: 200, body: [] };
      if (url === "/me/waitlist") return { status: 200, body: [{ entryId: 1, classId: "c1", className: "Class 1", status: "OFFERED", position: 0, offeredHoldId: "h9" }] };
      if (url === "/me/notices") return { status: 200, body: [{ id: 5, holdId: "h9", className: "Class 1", kind: "OFFERED", createdAt: "2026-11-24T10:00:00Z" }] };
    });
    renderApp("/applications");

    expect(await screen.findByText("A seat opened for you. It is held now, with its own timer.")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("link", { name: "A seat in Class 1 was offered to you from the waiting list." })).toHaveAttribute("href", "/applications/h9"));
  });
});
