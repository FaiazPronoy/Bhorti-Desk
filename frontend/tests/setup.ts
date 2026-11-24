import "@testing-library/jest-dom/vitest";
import { cleanup, configure } from "@testing-library/react";
import { afterEach, vi } from "vitest";

// The default one second wait for findBy is too short on a busy laptop running Docker.
// Same checks, more patience.
configure({ asyncUtilTimeout: 3000 });

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  localStorage.clear();
  delete window.__RUNTIME_CONFIG__;
});

// jsdom has no layout, so scrolling is a no-op.
Element.prototype.scrollIntoView = () => {};
