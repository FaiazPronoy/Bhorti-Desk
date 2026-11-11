import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router";
import { isApiError } from "./api/apiErrors";
import { StudentIdProvider } from "./features/student-identity/useStudentId";
import { LanguageProvider } from "./i18n/LanguageProvider";
import { routes } from "./routes";
import { applyTheme, startingTheme } from "./components/ThemeToggle";

import "@fontsource/onest/400.css";
import "@fontsource/onest/600.css";
import "@fontsource/baloo-da-2/600.css";
import "@fontsource/baloo-da-2/700.css";
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

// Set the theme before the first paint, so a dark-mode user never sees a white flash.
applyTheme(startingTheme());

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
