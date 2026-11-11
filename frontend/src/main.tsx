import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router";
import { isApiError } from "./api/apiErrors";
import { StudentIdProvider } from "./features/student-identity/useStudentId";
import { LanguageProvider } from "./i18n/LanguageProvider";
import { routes } from "./routes";

import "@fontsource/atkinson-hyperlegible-next/400.css";
import "@fontsource/atkinson-hyperlegible-next/700.css";
import "@fontsource/atkinson-hyperlegible-mono/400.css";
import "@fontsource/atkinson-hyperlegible-mono/700.css";
import "@fontsource/noto-sans-bengali/400.css";
import "@fontsource/noto-sans-bengali/600.css";
import "./styles/global.css";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // A 4xx will not change by asking again; a network failure might.
      retry: (failureCount, error) => !(isApiError(error) && error.kind === "api") && failureCount < 2,
      // Polling keeps the data fresh; coming back to the tab reads it again at once.
      refetchOnWindowFocus: true,
      staleTime: 0,
    },
  },
});

const router = createBrowserRouter(routes);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <LanguageProvider>
        <StudentIdProvider>
          <RouterProvider router={router} />
        </StudentIdProvider>
      </LanguageProvider>
    </QueryClientProvider>
  </StrictMode>,
);
