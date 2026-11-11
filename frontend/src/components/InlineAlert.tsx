import type { ReactNode } from "react";

// A message next to the thing it is about. Never a browser alert.
export function InlineAlert({ tone = "danger", children }: { tone?: "danger" | "info" | "success"; children: ReactNode }) {
  return (
    <div className={`inline-alert inline-alert--${tone}`} role={tone === "danger" ? "alert" : "status"}>
      {children}
    </div>
  );
}
