import { useEffect, useState } from "react";
import { useLanguage } from "../i18n/LanguageProvider";

type Theme = "light" | "dark";
const STORAGE_KEY = "bhorti-desk.theme";

// Light is the main theme. Dark only when the person picked it before.
export function startingTheme(): Theme {
  try {
    if (localStorage.getItem(STORAGE_KEY) === "dark") return "dark";
  } catch {
    // Storage blocked: stay light.
  }
  return "light";
}

export function applyTheme(theme: Theme) {
  document.documentElement.dataset.theme = theme;
}

// A small text link in the footer, never an icon in the header.
export function ThemeToggle() {
  const { t } = useLanguage();
  const [theme, setTheme] = useState<Theme>(startingTheme);

  useEffect(() => applyTheme(theme), [theme]);

  function toggle() {
    const next = theme === "dark" ? "light" : "dark";
    setTheme(next);
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // The choice lasts until the tab closes.
    }
  }

  return (
    <button type="button" className="text-link theme-toggle" onClick={toggle}>
      {theme === "dark" ? t("theme.toLight") : t("theme.toDark")}
    </button>
  );
}
