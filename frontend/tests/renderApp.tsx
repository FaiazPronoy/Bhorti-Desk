import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { vi } from "vitest";
import { StudentIdProvider } from "../src/features/student-identity/useStudentId";
import { LanguageProvider } from "../src/i18n/LanguageProvider";
import type { Language } from "../src/i18n/formatNumber";
import { routes } from "../src/routes";

export const API = "http://api.test";

type Handler = (url: string, init: RequestInit) => { status: number; body?: unknown } | undefined;

// Replaces fetch with a small table of answers. Every call is recorded so a test can
// look at the headers that were sent.
export function mockApi(handler: Handler) {
  window.__RUNTIME_CONFIG__ = { apiBaseUrl: API };
  const calls: { url: string; init: RequestInit }[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const url = String(input);
    calls.push({ url, init });
    const answer = handler(url.replace(API, ""), init) ?? { status: 404, body: { code: "NOT_FOUND", message: "Not found" } };
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
        <StudentIdProvider>
          <RouterProvider router={router} />
        </StudentIdProvider>
      </LanguageProvider>
    </QueryClientProvider>,
  );
}

export const VALID_STUDENT_ID = "20150123456789012";
