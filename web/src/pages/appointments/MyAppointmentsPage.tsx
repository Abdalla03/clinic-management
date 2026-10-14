import { useState } from "react";
import { Badge } from "../../components/ui/Badge";
import { Card } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { Pagination } from "../../components/ui/Pagination";
import { IconCalendar } from "../../components/icons";
import { useOwnAppointments } from "../../features/appointments/hooks";
import { appointmentStatusKey, formatDateTime } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";

const PAGE_SIZE = 10;

export function MyAppointmentsPage() {
  const { t, locale } = useI18n();
  const [page, setPage] = useState(1);

  const { data, isLoading, isError, refetch } = useOwnAppointments({ page, pageSize: PAGE_SIZE });
  const appointments = data?.items ?? [];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t("appointment.myTitle")} />
      <Card className="overflow-hidden">
        {isLoading ? <LoadingState /> : null}

        {isError ? (
          <EmptyState
            title={t("common.error")}
            hint={
              <button type="button" className="text-sm font-medium text-primary-600 hover:underline" onClick={() => void refetch()}>
                {t("common.retry")}
              </button>
            }
          />
        ) : null}

        {!isLoading && !isError && appointments.length === 0 ? (
          <EmptyState icon={<IconCalendar className="size-8 text-slate-300" />} title={t("appointment.noAppointments")} />
        ) : null}

        {!isLoading && !isError && appointments.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-4 py-3 text-start font-semibold">{t("appointment.time")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("appointment.doctor")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("appointment.reason")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("appointment.status")}</th>
                </tr>
              </thead>
              <tbody>
                {appointments.map((appointment) => {
                  const statusKey = appointmentStatusKey(appointment.status);
                  return (
                    <tr key={appointment.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                      <td className="px-4 py-3 text-slate-900">{formatDateTime(appointment.startsAtUtc, locale)}</td>
                      <td className="px-4 py-3 text-slate-700">{appointment.doctorName ?? "—"}</td>
                      <td className="px-4 py-3 text-slate-600">{appointment.reasonForVisit ?? "—"}</td>
                      <td className="px-4 py-3">
                        {statusKey ? (
                          <Badge
                            tone={appointment.status === 4 ? "red" : appointment.status === 3 ? "slate" : "green"}
                          >
                            {t(statusKey)}
                          </Badge>
                        ) : (
                          "—"
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : null}

        {data && (data.totalCount ?? 0) > 0 ? (
          <div className="border-t border-slate-200">
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount ?? 0} onChange={setPage} />
          </div>
        ) : null}
      </Card>
    </div>
  );
}

export default MyAppointmentsPage;