import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import {
  Bar,
  BarChart,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { Card, CardBody, CardHeader } from "../components/ui/Card";
import { Skeleton } from "../components/ui/Skeleton";
import {
  IconCalendar,
  IconChart,
  IconFileText,
  IconHeart,
  IconPill,
  IconShield,
  IconUsers,
} from "../components/icons";
import { useAuth } from "../features/auth/useAuth";
import { useOwnAppointments } from "../features/appointments/hooks";
import { useOutstandingReport, useRevenueReport } from "../features/reports/hooks";
import type { ReportRange } from "../lib/api/reports";
import { clinicName } from "../lib/branding";
import { useI18n } from "../lib/i18n/useI18n";
import type { TranslationKey } from "../lib/i18n/translations";

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

function formatDateTime(locale: string, value?: string | null): string {
  if (!value) {
    return "—";
  }
  return new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
}

function recentRange(days = 90): ReportRange {
  const to = new Date();
  const from = new Date(to.getTime() - days * 24 * 60 * 60 * 1000);
  return { fromUtc: from.toISOString(), toUtc: to.toISOString() };
}

const METHOD_COLORS = ["#217d6b", "#0ea5e9", "#f59e0b", "#8b5cf6"];

function methodLabelKey(method?: number): TranslationKey {
  switch (method) {
    case 1:
      return "paymentMethod.cash";
    case 2:
      return "paymentMethod.card";
    case 3:
      return "paymentMethod.bankTransfer";
    case 4:
      return "paymentMethod.insurance";
    default:
      return "paymentMethod.cash";
  }
}

function statusLabelKey(status?: number): TranslationKey {
  switch (status) {
    case 1:
      return "appointment.status.booked";
    case 2:
      return "appointment.status.confirmed";
    case 3:
      return "appointment.status.done";
    case 4:
      return "appointment.status.cancelled";
    case 5:
      return "appointment.status.noShow";
    default:
      return "appointment.status.booked";
  }
}

function Currency({ value }: { value?: number }) {
  return <span>{new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value ?? 0)}</span>;
}

export function HomePage() {
  const { user, isClinicalStaff, isPatient } = useAuth();
  const { t, locale } = useI18n();

  const today = formatToday(locale);
  const range = recentRange(90);

  const revenue = useRevenueReport(isClinicalStaff ? range : {});
  const outstanding = useOutstandingReport(isClinicalStaff ? range : {});
  const myAppointments = useOwnAppointments({ page: 1, pageSize: 5 }, isPatient);

  const methods = (revenue.data?.byMethod ?? []).map((entry) => ({
    label: t(methodLabelKey(entry.method)),
    amount: entry.amount ?? 0,
  }));

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <p className="text-sm text-slate-500">{today}</p>
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">
          {t("dashboard.title", { name: user?.fullName ?? "" })}
        </h1>
        <p className="text-sm text-slate-500">{t("dashboard.subtitle", { clinic: clinicName })}</p>
      </div>

      {isClinicalStaff ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            <KpiCard
              label={t("report.totalCollected")}
              loading={revenue.isPending}
              value={<Currency value={revenue.data?.totalCollected} />}
              icon={<IconFileText />}
              accent="bg-emerald-100 text-emerald-700"
            />
            <KpiCard
              label={t("report.totalOutstanding")}
              loading={outstanding.isPending}
              value={<Currency value={outstanding.data?.totalOutstanding} />}
              icon={<IconFileText />}
              accent="bg-amber-100 text-amber-700"
            />
            <KpiCard
              label={t("report.invoiceCount")}
              loading={revenue.isPending}
              value={<span>{revenue.data?.invoiceCount ?? 0}</span>}
              icon={<IconChart />}
              accent="bg-sky-100 text-sky-700"
            />
          </div>

          <div className="grid gap-6 lg:grid-cols-2">
            <Card>
              <CardHeader title={t("report.revenue")} subtitle={t("report.revenue.subtitle")} />
              <CardBody className="h-72">
                {revenue.isPending ? (
                  <Skeleton className="h-full w-full" />
                ) : methods.length === 0 ? (
                  <EmptyChart label={t("common.noRecords")} />
                ) : (
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={methods}>
                      <XAxis dataKey="label" tickLine={false} axisLine={false} fontSize={12} />
                      <YAxis tickLine={false} axisLine={false} width={48} fontSize={12} />
                      <Tooltip cursor={{ fill: "rgba(148,163,184,0.15)" }} />
                      <Bar dataKey="amount" radius={[6, 6, 0, 0]}>
                        {methods.map((entry, index) => (
                          <Cell key={entry.label} fill={METHOD_COLORS[index % METHOD_COLORS.length]} />
                        ))}
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                )}
              </CardBody>
            </Card>

            <Card>
              <CardHeader
                title={t("report.outstanding")}
                subtitle={t("report.outstanding.subtitle")}
                actions={
                  <Link to="/reports" className="text-sm font-medium text-primary-600 hover:underline">
                    {t("nav.reports")}
                  </Link>
                }
              />
              <CardBody className="flex flex-col gap-3">
                {outstanding.isPending ? (
                  <>
                    <Skeleton className="h-10 w-full" />
                    <Skeleton className="h-10 w-full" />
                    <Skeleton className="h-10 w-full" />
                  </>
                ) : (outstanding.data?.byPatient ?? []).length === 0 ? (
                  <p className="text-sm text-slate-500">{t("common.noRecords")}</p>
                ) : (
                  (outstanding.data?.byPatient ?? []).slice(0, 5).map((patient) => (
                    <div
                      key={patient.patientId ?? patient.patientName}
                      className="flex items-center justify-between gap-3 rounded-xl border border-slate-100 px-3 py-2"
                    >
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-slate-800">{patient.patientName ?? "—"}</p>
                        <p className="text-xs text-slate-500">{patient.medicalRecordNumber ?? ""}</p>
                      </div>
                      <span className="text-sm font-semibold text-amber-600">
                        <Currency value={patient.outstandingAmount} />
                      </span>
                    </div>
                  ))
                )}
              </CardBody>
            </Card>
          </div>
        </>
      ) : null}

      {isPatient ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2">
            <QuickLink to="/appointments/new" label={t("appointment.book")} icon={<IconCalendar />} accent="bg-primary-100 text-primary-700" />
            <QuickLink to="/me" label={t("patient.myRecord")} icon={<IconHeart />} accent="bg-rose-100 text-rose-700" />
            <QuickLink to="/prescriptions/me" label={t("prescription.myTitle")} icon={<IconPill />} accent="bg-violet-100 text-violet-700" />
            <QuickLink to="/invoices/me" label={t("invoice.myTitle")} icon={<IconFileText />} accent="bg-amber-100 text-amber-700" />
          </div>

          <Card>
            <CardHeader
              title={t("appointment.myTitle")}
              actions={
                <Link to="/appointments/me" className="text-sm font-medium text-primary-600 hover:underline">
                  {t("common.viewDetails")}
                </Link>
              }
            />
            <CardBody className="flex flex-col gap-3">
              {myAppointments.isPending ? (
                <>
                  <Skeleton className="h-10 w-full" />
                  <Skeleton className="h-10 w-full" />
                </>
              ) : (myAppointments.data?.items ?? []).length === 0 ? (
                <p className="text-sm text-slate-500">{t("appointment.noAppointments")}</p>
              ) : (
                (myAppointments.data?.items ?? []).map((appointment) => (
                  <Link
                    key={appointment.id}
                    to={`/appointments/${appointment.id}`}
                    className="flex items-center justify-between gap-3 rounded-xl border border-slate-100 px-3 py-2 transition-colors hover:bg-slate-50"
                  >
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-slate-800">{appointment.doctorName ?? "—"}</p>
                      <p className="text-xs text-slate-500">{formatDateTime(locale, appointment.startsAtUtc)}</p>
                    </div>
                    <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-600">
                      {t(statusLabelKey(appointment.status))}
                    </span>
                  </Link>
                ))
              )}
            </CardBody>
          </Card>
        </>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2">
        {!isPatient ? (
          <QuickLink to="/patients" label={t("dashboard.sectionPatients")} icon={<IconUsers />} accent="bg-primary-100 text-primary-700" />
        ) : null}
        <QuickLink to="/account/security" label={t("dashboard.sectionSecurity")} icon={<IconShield />} accent="bg-sky-100 text-sky-700" />
      </div>
    </div>
  );
}

function KpiCard({
  label,
  value,
  icon,
  accent,
  loading,
}: {
  label: string;
  value: ReactNode;
  icon: ReactNode;
  accent: string;
  loading: boolean;
}) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-medium text-slate-500">{label}</p>
        <span className={`flex size-9 items-center justify-center rounded-xl ${accent}`}>{icon}</span>
      </div>
      <div className="mt-3 text-2xl font-bold tracking-tight text-slate-900">
        {loading ? <Skeleton className="h-7 w-24" /> : value}
      </div>
    </div>
  );
}

function QuickLink({
  to,
  label,
  icon,
  accent,
}: {
  to: string;
  label: string;
  icon: ReactNode;
  accent: string;
}) {
  return (
    <Link
      to={to}
      className="group flex items-center gap-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md"
    >
      <span className={`flex size-11 items-center justify-center rounded-xl ${accent}`}>{icon}</span>
      <span className="font-semibold text-slate-900">{label}</span>
    </Link>
  );
}

function EmptyChart({ label }: { label: string }) {
  return (
    <div className="flex h-full items-center justify-center text-sm text-slate-400">{label}</div>
  );
}

export default HomePage;
