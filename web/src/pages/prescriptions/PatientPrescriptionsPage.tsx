import { useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { Pagination } from "../../components/ui/Pagination";
import { IconPill } from "../../components/icons";
import { usePatient } from "../../features/patients/hooks";
import { usePatientPrescriptions } from "../../features/prescriptions/hooks";
import { formatDateTime } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";

const PAGE_SIZE = 10;

export function PatientPrescriptionsPage() {
  const { patientId } = useParams<{ patientId: string }>();
  const { t, locale } = useI18n();
  const [page, setPage] = useState(1);

  const patientQuery = usePatient(patientId);
  const prescriptionsQuery = usePatientPrescriptions(patientId, { page, pageSize: PAGE_SIZE });
  const prescriptions = prescriptionsQuery.data?.items ?? [];

  const patientName = useMemo(
    () => patientQuery.data?.fullName ?? [patientQuery.data?.firstName, patientQuery.data?.lastName].join(" "),
    [patientQuery.data],
  );

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("prescription.title")}
        subtitle={patientName || undefined}
        actions={
          <Link to={patientId ? `/patients/${patientId}` : "/patients"}>
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      <Card className="overflow-hidden">
        {prescriptionsQuery.isLoading ? <LoadingState /> : null}

        {prescriptionsQuery.isError ? <EmptyState title={t("common.error")} /> : null}

        {!prescriptionsQuery.isLoading && !prescriptionsQuery.isError && prescriptions.length === 0 ? (
          <EmptyState icon={<IconPill className="size-8 text-slate-300" />} title={t("prescription.noPrescriptions")} />
        ) : null}

        {!prescriptionsQuery.isLoading && !prescriptionsQuery.isError && prescriptions.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-4 py-3 text-start font-semibold">{t("prescription.doctor")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("prescription.issuedAt")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("prescription.items")}</th>
                  <th className="px-4 py-3 text-end font-semibold">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {prescriptions.map((prescription) => (
                  <tr key={prescription.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                    <td className="px-4 py-3 text-slate-600">{prescription.doctorName ?? "—"}</td>
                    <td className="px-4 py-3 text-slate-600">{formatDateTime(prescription.issuedAtUtc, locale)}</td>
                    <td className="px-4 py-3 text-slate-600">{prescription.itemCount ?? 0}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-end gap-2">
                        <Link
                          to={`/prescriptions/${prescription.id}`}
                          className="text-sm font-medium text-primary-600 hover:underline"
                        >
                          {t("prescription.viewPrescription")}
                        </Link>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}

        {prescriptionsQuery.data && (prescriptionsQuery.data.totalCount ?? 0) > 0 ? (
          <div className="border-t border-slate-200">
            <Pagination
              page={page}
              pageSize={PAGE_SIZE}
              totalCount={prescriptionsQuery.data.totalCount ?? 0}
              onChange={setPage}
            />
          </div>
        ) : null}
      </Card>
    </div>
  );
}

export default PatientPrescriptionsPage;