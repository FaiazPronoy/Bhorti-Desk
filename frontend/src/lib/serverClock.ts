import { useEffect, useState } from "react";

// The countdown must follow the server's clock, not the phone's, which may be minutes
// off. Every hold response carries serverTime; we keep the difference and add it to
// Date.now() whenever we need "now".
let offsetMs = 0;
let hasOffset = false;
const listeners = new Set<() => void>();

export function rememberServerTime(serverTime: string | undefined) {
  if (!serverTime) return;
  const parsed = Date.parse(serverTime);
  if (Number.isNaN(parsed)) return;
  // Network delay only ever makes an answer look older than it is, so the largest
  // offset seen is the best guess. Taking the newest one instead could move the clock
  // back and flip an EXPIRED hold back to HELD for a moment.
  const measured = parsed - Date.now();
  offsetMs = hasOffset ? Math.max(offsetMs, measured) : measured;
  hasOffset = true;
  // Countdowns already on screen redraw at once instead of on their next tick.
  listeners.forEach((listener) => listener());
}

export function serverNow(): number {
  return Date.now() + offsetMs;
}

// Re-renders once a second with the current server time. The countdown is worked out
// from the absolute expiry on every tick, so a tab that slept does not drift.
export function useServerNow(): number {
  const [now, setNow] = useState(serverNow);

  useEffect(() => {
    const update = () => setNow(serverNow());
    const timer = setInterval(update, 1000);
    const onVisible = () => {
      if (document.visibilityState === "visible") update();
    };
    listeners.add(update);
    document.addEventListener("visibilitychange", onVisible);
    return () => {
      clearInterval(timer);
      listeners.delete(update);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, []);

  return now;
}
