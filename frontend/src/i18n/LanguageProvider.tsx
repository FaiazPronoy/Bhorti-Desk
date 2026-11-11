import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { bn } from "./bn";
import { en, type MessageKey } from "./en";
import { formatNumber, type Language } from "./formatNumber";

const STORAGE_KEY = "first-bench.language";

interface LanguageContextValue {
  language: Language;
  setLanguage: (language: Language) => void;
  t: (key: MessageKey, values?: Record<string, string | number>) => string;
  number: (value: number) => string;
}

const LanguageContext = createContext<LanguageContextValue | null>(null);

function readStoredLanguage(): Language {
  try {
    return localStorage.getItem(STORAGE_KEY) === "en" ? "en" : "bn";
  } catch {
    return "bn";
  }
}

export function LanguageProvider({ children, initialLanguage }: { children: ReactNode; initialLanguage?: Language }) {
  const [language, setLanguageState] = useState<Language>(() => initialLanguage ?? readStoredLanguage());

  const setLanguage = useCallback((next: Language) => {
    setLanguageState(next);
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // Private mode or blocked storage: the choice lasts until the tab closes.
    }
  }, []);

  useEffect(() => {
    document.documentElement.lang = language;
    document.title = (language === "bn" ? bn : en)["site.name"];
  }, [language]);

  const value = useMemo<LanguageContextValue>(() => {
    const words = language === "bn" ? bn : en;
    return {
      language,
      setLanguage,
      t: (key, values) => {
        let text: string = words[key];
        if (values) {
          for (const [name, value] of Object.entries(values)) {
            text = text.replaceAll(`{${name}}`, String(value));
          }
        }
        return text;
      },
      number: (value) => formatNumber(value, language),
    };
  }, [language, setLanguage]);

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

export function useLanguage(): LanguageContextValue {
  const context = useContext(LanguageContext);
  if (!context) throw new Error("useLanguage must be used inside LanguageProvider");
  return context;
}
