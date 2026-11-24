import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { vi } from "vitest";
import { LanguageProvider } from "../src/i18n/LanguageProvider";
import type { Language } from "../src/i18n/formatNumber";
import { routes } from "../src/routes";

export const API = "http://api.test";

type Handler = (url: string, init: RequestInit) => { status: number; body?: unknown } | undefined;

// Replaces fetch with a small table of answers. Every call is recorded so a test can
// look at the headers that were sent.
export function mockApi(handler: Handler) {
  window.__RUNTIME_CONFIG__ = { ...window.__RUNTIME_CONFIG__, apiBaseUrl: API };
  const calls: { url: string; init: RequestInit }[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const url = String(input);
    calls.push({ url, init });
    const path = url.replace(API, "");
    const answer = handler(path, init)
      // Nobody is signed in unless the test says so.
      ?? (path === "/auth/me" ? { status: 401, body: { code: "SIGN_IN_REQUIRED", message: "Sign in first." } } : undefined)
      ?? { status: 404, body: { code: "NOT_FOUND", message: "Not found" } };
    return new Response(answer.body === undefined ? null : JSON.stringify(answer.body), {
      status: answer.status,
      headers: { "Content-Type": "application/json" },
    });
  });
  vi.stubGlobal("fetch", fetchMock);
  return { calls, fetchMock };
}

// "stored" leaves the language to the app, which defaults to Bangla.
export function renderApp(path: string, language: Language | "stored" = "en") {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  return render(
    <QueryClientProvider client={queryClient}>
      <LanguageProvider initialLanguage={language === "stored" ? undefined : language}>
        <RouterProvider router={router} />
      </LanguageProvider>
    </QueryClientProvider>,
  );
}

export const VALID_STUDENT_ID = "20150123456789012";

// Runs the app as a deployment with SIGN_IN_REQUIRED on. Default is off.
export function turnSignInOn() {
  window.__RUNTIME_CONFIG__ = { ...window.__RUNTIME_CONFIG__, signInRequired: true };
}

// Answers as a signed-in guardian with one student on the account, then asks the test's
// own handler for everything else.
export function asGuardian(handler: Handler): Handler {
  turnSignInOn();
  return (url, init) => {
    if (url === "/auth/me") return { status: 200, body: { kind: "GUARDIAN", id: "g1", role: null } };
    if (url === "/me/students") {
      return {
        status: 200,
        body: [{ id: "s1", birthRegistrationNumber: VALID_STUDENT_ID, fullName: "Ayesha Rahman", dateOfBirth: "2015-03-14", gender: "FEMALE" }],
      };
    }
    return handler(url, init);
  };
}
