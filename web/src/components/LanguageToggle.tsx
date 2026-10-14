import { useI18n } from "../lib/i18n/useI18n";

export function LanguageToggle() {
  const { lang, setLang } = useI18n();
  const label = lang === "en" ? "العربية" : "English";
  const next = lang === "en" ? "ar" : "en";

  return (
    <button
      type="button"
      onClick={() => setLang(next)}
      className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-600 shadow-sm hover:bg-slate-50 hover:text-slate-900"
      aria-label={label}
      title={label}
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="size-4" aria-hidden="true">
        <circle cx="12" cy="12" r="9" />
        <path d="M3 12h18" />
        <path d="M12 3a15 15 0 0 1 0 18a15 15 0 0 1 0-18Z" />
      </svg>
      {label}
    </button>
  );
}