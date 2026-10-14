import { useState } from "react";
import { Link } from "react-router-dom";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { Pagination } from "../../components/ui/Pagination";
import { IconClipboard } from "../../components/icons";
import { useOwnVisits } from "../../features/visits/hooks";
import { formatDateTime } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";

const PAGE_SIZE = 10;

export function MyVisitsPage() {
  const { t, locale } = useI18n();
  const [page, setPage] = useState(1);

  const visitsQuery = useOwnVisits({ page, pageSize: PAGE_SIZE });
  const visits = visitsQuery.data?.items ?? [];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("visit.myTitle")}
        actions={
          <Link to="/me">
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      <Card className="overflow-hidden">
        {visitsQuery.isLoading ? <LoadingState /> : null}

        {visitsQuery.isError ? <EmptyState title={t("common.error")} /> : null}

        {!visitsQuery.isLoading && !visitsQuery.isError && visits.length === 0 ? (
          <EmptyState icon={<IconClipboard className="size-8 text-slate-300" />} title={t("visit.noVisits")} />
        ) : null}

        {!visitsQuery.isLoading && !visitsQuery.isError && visits.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-4 py-3 text-start font-semibold">{t("visit.doctor")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("visit.dateTime")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("visit.diagnosis")}</th>
                  <th className="px-4 py-3 text-end font-semibold">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {visits.map((visit) => (
                  <tr key={visit.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50">
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

        {visitsQuery.data && (visitsQuery.data.totalCount ?? 0) > 0 ? (
          <div className="border-t border-slate-200">
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={visitsQuery.data.totalCount ?? 0} onChange={setPage} />
          </div>
        ) : null}
      </Card>
    </div>
  );
}

export default MyVisitsPage;