declare global {
  interface Window {
    __RUNTIME_CONFIG__?: { apiBaseUrl?: string };
  }
}

// Where the API lives. In Docker the value comes from /runtime-config.js, which nginx
// writes from API_BASE_URL at start. In development it comes from .env.development.
// With neither, assume the API is on port 8080 of the same host.
export function apiBaseUrl(): string {
  const fromRuntime = window.__RUNTIME_CONFIG__?.apiBaseUrl?.trim();
  const fromBuild = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim();
  const chosen = fromRuntime || fromBuild || `${window.location.protocol}//${window.location.hostname}:8080`;
  return chosen.replace(/\/+$/, "");
}
