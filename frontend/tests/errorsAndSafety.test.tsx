import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { mockApi, renderApp, VALID_STUDENT_ID } from "./renderApp";

const fullClass = { id: "c1", name: "Class 6", totalSeats: 2, heldSeats: 1, confirmedSeats: 1, availableSeats: 0 };
const openClass = { id: "c2", name: "Class 7", totalSeats: 2, heldSeats: 0, confirmedSeats: 0, availableSeats: 2 };

describe("errors and safety", () => {
  it("disables Apply on a full class", async () => {
    mockApi((url) => (url === "/classes" ? { status: 200, body: [fullClass] } : undefined));
    renderApp("/");
    expect(await screen.findByRole("button", { name: "Full" })).toBeDisabled();
  });

  it("shows a class name with HTML in it as plain text", async () => {
    const name = "<img src=x onerror=alert(1)>";
    mockApi((url) => (url === "/classes" ? { status: 200, body: [{ ...openClass, name }] } : undefined));
    const { container } = renderApp("/");
    expect(await screen.findByText(name)).toBeInTheDocument();
    expect(container.querySelector("img")).toBeNull();
  });

  it("shows the plain message for CLASS_FULL next to the button", async () => {
    localStorage.setItem("first-bench.student-id", VALID_STUDENT_ID);
    mockApi((url, init) => {
      if (url === "/classes") return { status: 200, body: [openClass] };
      if (url === "/me/holds") return { status: 200, body: [] };
      if (url === "/classes/c2/holds" && init.method === "POST") {
        return { status: 409, body: { code: "CLASS_FULL", message: "No seats are left in this class." } };
      }
    });
    const user = userEvent.setup();
    renderApp("/");

    await user.click(await screen.findByRole("button", { name: "Apply" }));
    expect(
      await screen.findByText("No seats are left in this class. If someone does not pay in time, a seat will open again."),
    ).toBeInTheDocument();
  });

  it("shows the server banner, not a blank page, when the server cannot be reached", async () => {
    mockApi(() => {
      throw new TypeError("Failed to fetch");
    });
    renderApp("/");

    expect(await screen.findByRole("button", { name: "Try again" }, { timeout: 3000 })).toBeInTheDocument();
    expect(screen.getAllByText(/We cannot reach the server/).length).toBeGreaterThan(0);
    expect(screen.getByRole("heading", { name: "Classes" })).toBeInTheDocument();
  });

  it("shows Bangla by default", async () => {
    mockApi((url) => (url === "/classes" ? { status: 200, body: [openClass] } : undefined));
    renderApp("/", "stored");
    expect(await screen.findByRole("button", { name: "আবেদন করুন" })).toBeInTheDocument();
  });
});
