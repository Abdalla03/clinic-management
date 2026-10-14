import { Link } from "react-router-dom";
import { useI18n } from "../lib/i18n/useI18n";

export function ForbiddenPage() {
  const { t } = useI18n();

  return (
    <div className="flex min-h-full flex-col items-center justify-center gap-4 px-6 py-20 text-center">
      <p className="text-6xl font-black tracking-tight text-primary-600">403</p>
      <h1 className="text-xl font-bold text-slate-900">{t("error.forbidden")}</h1>
      <p className="max-w-md text-sm text-slate-500">
        {t("error.forbiddenHint")}
      </p>
      <Link
        to="/"
        className="mt-2 rounded-lg bg-primary-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-primary-700"
      >
        {t("nav.dashboard")}
      </Link>
    </div>
  );
}