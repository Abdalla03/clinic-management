import { Link } from "react-router-dom";
import { useAuth } from "../features/auth/useAuth";
import { Card, CardBody, CardHeader } from "../components/ui/Card";
import { IconHeart, IconShield, IconUsers } from "../components/icons";
import { useI18n } from "../lib/i18n/useI18n";

const dateFormatters = new Map<string, Intl.DateTimeFormat>();

function formatToday(locale: string): string {
  let formatter = dateFormatters.get(locale);
  if (!formatter) {
    formatter = new Intl.DateTimeFormat(locale, {
      weekday: "long",
      year: "numeric",
      month: "long",
      day: "numeric",
    });
    dateFormatters.set(locale, formatter);
  }
  return formatter.format(new Date());
}

export function HomePage() {
  const { user, isClinicalStaff } = useAuth();
  const { t, locale } = useI18n();

  const today = formatToday(locale);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <p className="text-sm text-slate-500">{today}</p>
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">
          {t("dashboard.title", { name: user?.fullName ?? "" })}
        </h1>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        {isClinicalStaff ? (
          <Link
            to="/patients"
            className="group rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition-shadow hover:shadow-md"
          >
            <div className="flex items-start gap-4">
              <div className="flex size-11 items-center justify-center rounded-xl bg-primary-100 text-primary-700">
                <IconUsers />
              </div>
              <div>
                <h2 className="font-semibold text-slate-900">{t("dashboard.sectionPatients")}</h2>
                <p className="mt-1 text-sm text-slate-500">{t("dashboard.sectionPatientsHint")}</p>
                <p className="mt-2 text-sm font-medium text-primary-600">
                  {t("dashboard.goToPatients")} →
                </p>
              </div>
            </div>
          </Link>
        ) : (
          <Link
            to="/me"
            className="group rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition-shadow hover:shadow-md"
          >
            <div className="flex items-start gap-4">
              <div className="flex size-11 items-center justify-center rounded-xl bg-rose-100 text-rose-700">
                <IconHeart />
              </div>
              <div>
                <h2 className="font-semibold text-slate-900">{t("patient.myRecord")}</h2>
                <p className="mt-1 text-sm text-slate-500">
                  {t("dashboard.sectionPatientsHint")}
                </p>
                <p className="mt-2 text-sm font-medium text-primary-600">
                  {t("dashboard.goToMyHealth")} →
                </p>
              </div>
            </div>
          </Link>
        )}

        <Link
          to="/account/security"
          className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition-shadow hover:shadow-md"
        >
          <div className="flex items-start gap-4">
            <div className="flex size-11 items-center justify-center rounded-xl bg-sky-100 text-sky-700">
              <IconShield />
            </div>
            <div>
              <h2 className="font-semibold text-slate-900">{t("dashboard.sectionSecurity")}</h2>
              <p className="mt-1 text-sm text-slate-500">{t("dashboard.sectionSecurityHint")}</p>
            </div>
          </div>
        </Link>
      </div>

      <Card>
        <CardHeader title={t("dashboard.whatsNew")} />
        <CardBody>
          <p className="text-sm text-slate-500">{t("dashboard.sectionPatientsHint")}</p>
        </CardBody>
      </Card>
    </div>
  );
}

export default HomePage;