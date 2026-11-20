import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router";
import { isApiError } from "./api/apiErrors";
import { LanguageProvider } from "./i18n/LanguageProvider";
import { routes } from "./routes";
import { applyTheme, startingTheme } from "./components/ThemeToggle";

// Onest for Latin text and every number, Baloo Da 2 for Bangla letters only.
import "@fontsource/onest/latin-400.css";
import "@fontsource/onest/latin-600.css";
import "@fontsource/onest/latin-700.css";
import "@fontsource/baloo-da-2/bengali-400.css";
import "@fontsource/baloo-da-2/bengali-600.css";
import "@fontsource/baloo-da-2/bengali-700.css";
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
        <RouterProvider router={router} />
      </LanguageProvider>
    </QueryClientProvider>
  </StrictMode>,
);
