import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { Input, Select } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { Pagination } from "../../components/ui/Pagination";
import { IconClipboard, IconPlus } from "../../components/icons";
import { useDoctors } from "../../features/doctors/hooks";
import { useVisitsSearch } from "../../features/visits/hooks";
import { useAuth } from "../../features/auth/useAuth";
import { formatDateTime } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import { RoleDoctor } from "../../lib/roles";

const PAGE_SIZE = 10;

export function VisitsListPage() {
  const { t, locale } = useI18n();
  const { hasRole } = useAuth();
  const canCreate = hasRole(RoleDoctor);

  const [doctorId, setDoctorId] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [page, setPage] = useState(1);

  const doctorsQuery = useDoctors();

  const params = useMemo(
    () => ({
      doctorId: doctorId || undefined,
      fromUtc: fromDate ? `${fromDate}T00:00:00.000Z` : undefined,
      toUtc: toDate ? `${toDate}T23:59:59.999Z` : undefined,
      page,
      pageSize: PAGE_SIZE,
    }),
    [doctorId, fromDate, toDate, page],
  );

  const { data, isLoading, isError, isFetching, refetch } = useVisitsSearch(params);
  const visits = data?.items ?? [];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("visit.title")}
        subtitle={t("visit.subtitle")}
        actions={
          canCreate ? (
            <Link to="/visits/new">
              <Button>
                <IconPlus />
                {t("visit.newVisit")}
              </Button>
            </Link>
          ) : undefined
        }
      />

      <Card className="overflow-hidden">
        <div className="flex flex-col gap-3 border-b border-slate-200 p-4 lg:flex-row lg:items-center">
          <Select
            value={doctorId}
            onChange={(event) => {
              setDoctorId(event.target.value);
              setPage(1);
            }}
            aria-label={t("visit.doctor")}
            className="lg:w-56"
          >
            <option value="">{t("visit.doctor")} — {t("common.all")}</option>
            {(doctorsQuery.data ?? []).map((doctor) => (
              <option key={doctor.id} value={doctor.id}>
                {doctor.name}
              </option>
            ))}
          </Select>
          <div className="flex flex-1 flex-wrap items-center gap-3">
            <div className="flex items-center gap-2">
              <label className="text-xs font-medium text-slate-500">{t("common.from")}</label>
              <Input
                type="date"
                value={fromDate}
                onChange={(event) => {
                  setFromDate(event.target.value);
                  setPage(1);
                }}
                aria-label={t("common.from")}
              />
            </div>
            <div className="flex items-center gap-2">
              <label className="text-xs font-medium text-slate-500">{t("common.to")}</label>
              <Input
                type="date"
                value={toDate}
                onChange={(event) => {
                  setToDate(event.target.value);
                  setPage(1);
                }}
                aria-label={t("common.to")}
              />
            </div>
          </div>
        </div>

        {isLoading ? <LoadingState /> : null}

        {isError ? (
          <EmptyState
            title={t("common.error")}
            hint={
              <Button variant="secondary" size="sm" onClick={() => void refetch()}>
                {t("common.retry")}
              </Button>
            }
          />
        ) : null}

        {!isLoading && !isError && visits.length === 0 ? (
          <EmptyState icon={<IconClipboard className="size-8 text-slate-300" />} title={t("visit.noVisits")} />
        ) : null}

        {!isLoading && !isError && visits.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-4 py-3 text-start font-semibold">{t("visit.patient")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("visit.doctor")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("visit.dateTime")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("visit.diagnosis")}</th>
                  <th className="px-4 py-3 text-end font-semibold">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {visits.map((visit) => (
                  <tr key={visit.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                    <td className="px-4 py-3">
                      <Link to={`/visits/${visit.id}`} className="font-medium text-slate-900 hover:text-primary-600">
                        {visit.patientName ?? "—"}
                      </Link>
                    </td>
                    <td className="px-4 py-3 text-slate-600">{visit.doctorName ?? "—"}</td>
                    <td className="px-4 py-3 text-slate-600">{formatDateTime(visit.visitAtUtc, locale)}</td>
                    <td className="max-w-56 truncate px-4 py-3 text-slate-600">{visit.diagnosis ?? "—"}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-end gap-2">
                        <Link
                          to={`/visits/${visit.id}`}
                          className="text-sm font-medium text-primary-600 hover:underline"
                        >
                          {t("prescription.viewVisit")}
                        </Link>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}

        {data && (data.totalCount ?? 0) > 0 ? (
          <div className="border-t border-slate-200">
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount ?? 0} onChange={setPage} />
          </div>
        ) : null}

        {isFetching && !isLoading ? (
          <p className="border-t border-slate-100 px-5 py-2 text-xs text-slate-400">{t("common.loading")}</p>
        ) : null}
      </Card>
    </div>
  );
}

export default VisitsListPage;