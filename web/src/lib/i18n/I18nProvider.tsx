import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { translations, type Lang, type TranslationKey } from "./translations";

export interface I18nContextValue {
  lang: Lang;
  dir: "ltr" | "rtl";
  setLang: (lang: Lang) => void;
  t: (key: TranslationKey, params?: Record<string, string | number>) => string;
  locale: string;
}

export const I18nContext = createContext<I18nContextValue | null>(null);

const STORAGE_KEY = "clinic.lang";

function resolveInitialLang(): Lang {
  if (typeof localStorage !== "undefined") {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === "en" || stored === "ar") {
      return stored;
    }
  }
  if (typeof navigator !== "undefined" && navigator.language.toLowerCase().startsWith("ar")) {
    return "ar";
  }
  return "en";
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(resolveInitialLang);

  useEffect(() => {
    document.documentElement.lang = lang;
    document.documentElement.dir = lang === "ar" ? "rtl" : "ltr";
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // storage unavailable; keep the in-memory preference
    }
  }, [lang]);

  const t = useCallback(
    (key: TranslationKey, params?: Record<string, string | number>) => {
      const dictionary = translations[lang];
      const template: string = dictionary[key] ?? translations.en[key] ?? key;
      if (!params) {
        return template;
      }
      return template.replace(/\{(\w+)\}/g, (match, name: string) =>
        params[name] !== undefined ? String(params[name]) : match,
      );
    },
    [lang],
  );

  const value = useMemo<I18nContextValue>(
    () => ({
      lang,
      dir: lang === "ar" ? "rtl" : "ltr",
      setLang: setLangState,
      t,
      locale: lang,
    }),
    [lang, t],
  );

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}