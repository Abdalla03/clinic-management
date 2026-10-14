import { useMemo, useState } from "react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { Button } from "../../components/ui/Button";
import { Card, CardHeader } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { Input } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { IconChart } from "../../components/icons";
import { useOutstandingReport, useRevenueReport, useVisitReport } from "../../features/reports/hooks";
import { useAuth } from "../../features/auth/useAuth";
import { downloadCsv } from "../../lib/csv";
import { formatCurrency, paymentMethodKey } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import type { TranslationKey } from "../../lib/i18n/translations";
import { cn } from "../../lib/cn";
import { RoleDoctor, RoleSecretary } from "../../lib/roles";

const CHART_COLORS = ["#217d6b", "#4cb49d", "#1d6457", "#80d0ba", "#18423b", "#b2e5d4"];

function nowIsoDate(): string {
  const date = new Date();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

const MONTH_START = (() => {
  const date = new Date();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  return `${date.getFullYear()}-${month}-01`;
})();

const TODAY = nowIsoDate();

type TabKey = "revenue" | "outstanding" | "visits";

export function ReportsPage() {
  const { t, locale } = useI18n();
  const { hasRole } = useAuth();
  const isSecretary = hasRole(RoleSecretary);

  const [fromDate, setFromDate] = useState(MONTH_START);
  const [toDate, setToDate] = useState(TODAY);
  const [tab, setTab] = useState<TabKey>(isSecretary ? "revenue" : "visits");

  const range = useMemo(
    () => ({
      fromUtc: fromDate ? `${fromDate}T00:00:00.000Z` : undefined,
      toUtc: toDate ? `${toDate}T23:59:59.999Z` : undefined,
    }),
    [fromDate, toDate],
  );

  const revenueQuery = useRevenueReport(range);
  const outstandingQuery = useOutstandingReport(range);
  const visitsQuery = useVisitReport(range);

  const tabs: Array<{ key: TabKey; labelKey: TranslationKey; visible: boolean }> = [
    { key: "revenue", labelKey: "report.revenue", visible: isSecretary },
    { key: "outstanding", labelKey: "report.outstanding", visible: isSecretary },
    { key: "visits", labelKey: "report.visits", visible: true },
  ];
  const visibleTabs = tabs.filter((item) => item.visible);
  const activeTab: TabKey = visibleTabs.some((item) => item.key === tab) ? tab : visibleTabs[0]!.key;

  const exportCurrent = () => {
    if (activeTab === "revenue") {
      const rows = (revenueQuery.data?.byMethod ?? []).map((entry) => [
        paymentMethodKey(entry.method) ? t(paymentMethodKey(entry.method)!) : entry.method,
        entry.paymentCount,
        entry.amount,
      ]);
      downloadCsv(
        "reports-revenue.csv",
        [t("report.method"), t("report.payments"), t("report.collected")],
        rows,
      );
      return;
    }
    if (activeTab === "outstanding") {
      const rows = (outstandingQuery.data?.byPatient ?? []).map((entry) => [
        entry.patientName ?? "",
        entry.medicalRecordNumber ?? "",
        entry.invoiceCount,
        entry.outstandingAmount,
      ]);
      downloadCsv(
        "reports-outstanding.csv",
        [t("report.patient"), t("report.medicalRecordNumber"), t("report.invoices"), t("report.totalOutstanding")],
        rows,
      );
      return;
    }
    const rows = (visitsQuery.data?.byDoctor ?? []).map((entry) => [entry.doctorName ?? "", entry.visitCount]);
    downloadCsv("reports-visits.csv", [t("report.doctor"), t("report.visitsCount")], rows);
  };

  const isLoading =
    (activeTab === "revenue" && revenueQuery.isLoading) ||
    (activeTab === "outstanding" && outstandingQuery.isLoading) ||
    (activeTab === "visits" && visitsQuery.isLoading);

  const revenueData = useMemo(
    () =>
      (revenueQuery.data?.byMethod ?? []).map((entry) => ({
        name: paymentMethodKey(entry.method) ? t(paymentMethodKey(entry.method)!) : `#${entry.method}`,
        value: entry.amount,
      })),
    [revenueQuery.data, t],
  );

  const visitsData = useMemo(
    () => (visitsQuery.data?.byDoctor ?? []).map((entry) => ({ name: entry.doctorName ?? "—", visits: entry.visitCount })),
    [visitsQuery.data],
  );

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("report.title")}
        subtitle={t("report.subtitle")}
        actions={
          <Button variant="secondary" onClick={exportCurrent}>
            {t("report.exportCsv")}
          </Button>
        }
      />

      <div className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-2">
            <label className="text-xs font-medium text-slate-500">{t("report.from")}</label>
            <Input
              type="date"
              value={fromDate}
              onChange={(event) => setFromDate(event.target.value)}
              aria-label={t("report.from")}
            />
          </div>
          <div className="flex items-center gap-2">
            <label className="text-xs font-medium text-slate-500">{t("report.to")}</label>
            <Input
              type="date"
              value={toDate}
              onChange={(event) => setToDate(event.target.value)}
              aria-label={t("report.to")}
            />
          </div>
        </div>

        <div className="flex flex-wrap gap-1.5">
          {visibleTabs.map((item) => (
            <button
              key={item.key}
              type="button"
              onClick={() => setTab(item.key)}
              className={cn(
                "rounded-lg px-3 py-1.5 text-sm font-semibold transition-colors",
                activeTab === item.key
                  ? "bg-primary-600 text-white shadow-sm"
                  : "bg-white text-slate-600 ring-1 ring-slate-200 hover:bg-slate-50",
              )}
            >
              {t(item.labelKey)}
            </button>
          ))}
        </div>
      </div>

      {isLoading ? <LoadingState /> : null}

      {activeTab === "revenue" ? (
        <Card>
          <CardHeader title={t("report.revenue")} subtitle={t("report.revenue.subtitle")} />
          {revenueQuery.isLoading ? <LoadingState /> : null}
          {revenueQuery.isError ? <EmptyState title={t("common.error")} /> : null}
          {!revenueQuery.isLoading && !revenueQuery.isError ? (
            <div className="flex flex-col gap-5 p-5">
              <div className="grid gap-4 sm:grid-cols-3">
                <SummaryStat label={t("report.totalCollected")} value={formatCurrency(revenueQuery.data?.totalCollected, "EGP", locale)} />
                <SummaryStat label={t("report.invoiceCount")} value={String(revenueQuery.data?.invoiceCount ?? 0)} />
                <SummaryStat label={t("report.paymentCount")} value={String(revenueQuery.data?.paymentCount ?? 0)} />
              </div>
              {(revenueQuery.data?.byMethod ?? []).length === 0 ? (
                <EmptyState title={t("common.noRecords")} icon={<IconChart className="size-8 text-slate-300" />} />
              ) : (
                <div className="flex flex-col gap-6 lg:flex-row">
                  <div className="h-72 min-w-0 flex-1">
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie
                          data={revenueData}
                          dataKey="value"
                          nameKey="name"
                          innerRadius={50}
                          outerRadius={90}
                          paddingAngle={2}
                          label
                        >
                          {revenueData.map((entry, index) => (
                            <Cell key={entry.name} fill={CHART_COLORS[index % CHART_COLORS.length]} />
                          ))}
                        </Pie>
                        <Tooltip formatter={(value) => formatCurrency(Number(value), "EGP", locale)} />
                        <Legend />
                      </PieChart>
                    </ResponsiveContainer>
                  </div>
                  <div className="min-w-0 flex-1 overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                          <th className="px-4 py-3 text-start font-semibold">{t("report.method")}</th>
                          <th className="px-4 py-3 text-end font-semibold">{t("report.payments")}</th>
                          <th className="px-4 py-3 text-end font-semibold">{t("report.collected")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(revenueQuery.data?.byMethod ?? []).map((entry) => {
                          const methodKey = paymentMethodKey(entry.method);
                          return (
                            <tr key={entry.method} className="border-b border-slate-100 last:border-0">
                              <td className="px-4 py-3 text-slate-900">{methodKey ? t(methodKey) : `#${entry.method}`}</td>
                              <td className="px-4 py-3 text-end text-slate-600">{entry.paymentCount}</td>
                              <td className="px-4 py-3 text-end font-medium text-slate-900">
                                {formatCurrency(entry.amount, "EGP", locale)}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          ) : null}
        </Card>
      ) : null}

      {activeTab === "outstanding" ? (
        <Card className="overflow-hidden">
          <CardHeader title={t("report.outstanding")} subtitle={t("report.outstanding.subtitle")} />
          {outstandingQuery.isLoading ? <LoadingState /> : null}
          {outstandingQuery.isError ? <EmptyState title={t("common.error")} /> : null}
          {!outstandingQuery.isLoading && !outstandingQuery.isError ? (
            <div className="p-5">
              <SummaryStat
                label={t("report.totalOutstanding")}
                value={formatCurrency(outstandingQuery.data?.totalOutstanding, "EGP", locale)}
              />
            </div>
          ) : null}
          {!outstandingQuery.isLoading && !outstandingQuery.isError &&
          (outstandingQuery.data?.byPatient ?? []).length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                    <th className="px-4 py-3 text-start font-semibold">{t("report.patient")}</th>
                    <th className="px-4 py-3 text-start font-semibold">{t("report.medicalRecordNumber")}</th>
                    <th className="px-4 py-3 text-end font-semibold">{t("report.invoices")}</th>
                    <th className="px-4 py-3 text-end font-semibold">{t("report.totalOutstanding")}</th>
                  </tr>
                </thead>
                <tbody>
                  {(outstandingQuery.data?.byPatient ?? []).map((entry) => (
                    <tr key={entry.patientId} className="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                      <td className="px-4 py-3 text-slate-900">{entry.patientName ?? "—"}</td>
                      <td className="px-4 py-3 font-mono text-xs text-slate-600">{entry.medicalRecordNumber ?? "—"}</td>
                      <td className="px-4 py-3 text-end text-slate-600">{entry.invoiceCount}</td>
                      <td className="px-4 py-3 text-end font-medium text-slate-900">
                        {formatCurrency(entry.outstandingAmount, "EGP", locale)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
          {!outstandingQuery.isLoading && !outstandingQuery.isError &&
          (outstandingQuery.data?.byPatient ?? []).length === 0 ? (
            <EmptyState title={t("common.noRecords")} icon={<IconChart className="size-8 text-slate-300" />} />
          ) : null}
        </Card>
      ) : null}

      {activeTab === "visits" ? (
        <Card>
          <CardHeader title={t("report.visits")} subtitle={t("report.visits.subtitle")} />
          {visitsQuery.isLoading ? <LoadingState /> : null}
          {visitsQuery.isError ? <EmptyState title={t("common.error")} /> : null}
          {!visitsQuery.isLoading && !visitsQuery.isError ? (
            <div className="flex flex-col gap-5 p-5">
              <SummaryStat label={t("report.totalVisits")} value={String(visitsQuery.data?.totalVisits ?? 0)} />
              {visitsData.length === 0 ? (
                <EmptyState title={t("common.noRecords")} icon={<IconChart className="size-8 text-slate-300" />} />
              ) : (
                <div className="flex flex-col gap-6 lg:flex-row">
                  <div className="h-72 min-w-0 flex-1">
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={visitsData} margin={{ top: 8, right: 16, left: 0, bottom: 8 }}>
                        <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                        <XAxis dataKey="name" tick={{ fontSize: 12 }} />
                        <YAxis allowDecimals={false} tick={{ fontSize: 12 }} />
                        <Tooltip />
                        <Legend />
                        <Bar dataKey="visits" name={t("report.visitsCount")} fill="#217d6b" radius={[4, 4, 0, 0]} />
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                  <div className="min-w-0 flex-1 overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                          <th className="px-4 py-3 text-start font-semibold">{t("report.doctor")}</th>
                          <th className="px-4 py-3 text-end font-semibold">{t("report.visitsCount")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(visitsQuery.data?.byDoctor ?? []).map((entry) => (
                          <tr key={entry.doctorId} className="border-b border-slate-100 last:border-0">
                            <td className="px-4 py-3 text-slate-900">{entry.doctorName ?? "—"}</td>
                            <td className="px-4 py-3 text-end font-medium text-slate-900">{entry.visitCount}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          ) : null}
        </Card>
      ) : null}

      {!isSecretary && !hasRole(RoleDoctor) ? <EmptyState title={t("common.error")} /> : null}
    </div>
  );
}

function SummaryStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-slate-50/60 p-4">
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</p>
      <p className="mt-1 truncate text-xl font-bold text-slate-900">{value}</p>
    </div>
  );
}

export default ReportsPage;