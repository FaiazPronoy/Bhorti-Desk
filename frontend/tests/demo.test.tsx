import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { mockApi, renderApp, turnSignInOn } from "./renderApp";

describe("public demo", () => {
  it("shows the demo banner and the sign-in code labelled as a demo", async () => {
    turnSignInOn();
    window.__RUNTIME_CONFIG__ = { ...window.__RUNTIME_CONFIG__, demoMode: true };
    mockApi((url) => (url === "/auth/otp/request" ? { status: 202, body: { demoCode: "482913" } } : url === "/classes" ? { status: 200, body: [] } : undefined));
    const user = userEvent.setup();
    renderApp("/login");

    expect(screen.getByText("Public demo. Data resets every day. Payments are sandbox only.")).toBeInTheDocument();
    await user.type(await screen.findByLabelText("Mobile number"), "01712345678");
    await user.click(screen.getByRole("button", { name: "Send code" }));

    expect(await screen.findByText("482913")).toBeInTheDocument();
    expect(screen.getByText("Demo only, your code is:")).toBeInTheDocument();
  });

  it("shows no banner outside demo mode", async () => {
    mockApi((url) => (url === "/classes" ? { status: 200, body: [] } : undefined));
    renderApp("/");

    expect(await screen.findByRole("heading", { name: "Classes" })).toBeInTheDocument();
    expect(screen.queryByText(/Public demo/)).not.toBeInTheDocument();
  });
});
