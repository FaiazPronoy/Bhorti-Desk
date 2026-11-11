// Used by "npm run dev" only. In Docker, nginx answers this path itself with the real
// API address from the API_BASE_URL setting.
window.__RUNTIME_CONFIG__ = { apiBaseUrl: "" };
