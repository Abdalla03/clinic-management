import { useState } from "react";
import { Badge } from "../../components/ui/Badge";
import { Card, CardBody, CardHeader } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { Pagination } from "../../components/ui/Pagination";
import { useOwnMedicalHistory, useOwnPatient } from "../../features/patients/hooks";
import type { PatientDetails } from "../../lib/api/patients";
import { bloodTypeLabelKey, formatDate, formatDateTime, genderLabelKey } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import type { TranslationKey } from "../../lib/i18n/translations";

const PAGE_SIZE = 10;

const details: Array<{ field: keyof PatientDetails; label: TranslationKey }> = [
  { field: "medicalRecordNumber", label: "patient.medicalRecordNumber" },
  { field: "fullName", label: "patient.fullName" },
  { field: "gender", label: "patient.gender" },
  { field: "bloodType", label: "patient.bloodType" },
  { field: "dateOfBirth", label: "patient.dateOfBirth" },
  { field: "nationalId", label: "patient.nationalId" },
  { field: "phoneNumber", label: "patient.phoneNumber" },
  { field: "address", label: "patient.address" },
  { field: "emergencyContactName", label: "patient.emergencyContactName" },
  { field: "emergencyContactPhone", label: "patient.emergencyContactPhone" },
];

export function MyHealthPage() {
  const { t, locale } = useI18n();
  const [page, setPage] = useState(1);

  const recordQuery = useOwnPatient();
  const historyQuery = useOwnMedicalHistory({ page, pageSize: PAGE_SIZE });

  if (recordQuery.isLoading) {
    return <LoadingState />;
  }

  const record = recordQuery.data;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t("patient.myRecord")} />

      {recordQuery.isError ? (
        <Card>
          <CardBody>
            <p className="text-sm text-red-600">{t("patient.patientNotFound")}</p>
          </CardBody>
        </Card>
      ) : null}

      {record ? (
        <div className="grid gap-6 lg:grid-cols-3">
          <Card>
            <CardHeader
              title={record.fullName ?? `${record.firstName ?? ""} ${record.lastName ?? ""}`.trim()}
              actions={
                record.isActive ? (
                  <Badge tone="green">{t("common.active")}</Badge>
                ) : (
                  <Badge tone="red">{t("common.inactive")}</Badge>
                )
              }
            />
            <CardBody>
              <dl className="grid grid-cols-1 gap-x-6 gap-y-4 sm:grid-cols-2">
                {details.map((row) => {
                  const value = record[row.field];
                  let display: string;
                  if (row.field === "gender") {
                    const key = genderLabelKey(value as PatientDetails["gender"]);
                    display = key ? t(key) : "—";
                  } else if (row.field === "bloodType") {
                    const key = bloodTypeLabelKey(value as PatientDetails["bloodType"]);
                    display = key ? t(key) : "—";
                  } else if (row.field === "dateOfBirth") {
                    display = formatDate(value as string | undefined, locale);
                  } else {
                    display = (value as string | null | undefined) ?? "—";
                  }
                  return (
                    <div key={row.field} className="flex flex-col gap-0.5">
                      <dt className="text-xs font-medium uppercase tracking-wide text-slate-400">{t(row.label)}</dt>
                      <dd className="text-sm font-medium text-slate-900">{display}</dd>
                    </div>
                  );
                })}
              </dl>

              {record.notes ? (
                <div className="mt-4 rounded-lg bg-slate-50 px-3 py-2">
                  <p className="text-xs font-medium uppercase tracking-wide text-slate-400">{t("patient.notes")}</p>
                  <p className="mt-0.5 text-sm text-slate-700">{record.notes}</p>
                </div>
              ) : null}
            </CardBody>
          </Card>

          <Card className="lg:col-span-2">
            <CardHeader title={t("patient.myMedicalHistory")} />
            {historyQuery.isLoading ? <LoadingState /> : null}
            {historyQuery.isError ? <EmptyState title={t("common.error")} /> : null}
            {!historyQuery.isLoading && !historyQuery.isError && (historyQuery.data?.items?.length ?? 0) === 0 ? (
              <EmptyState title={t("patient.noHistory")} />
            ) : null}

            {!historyQuery.isLoading && !historyQuery.isError && historyQuery.data?.items ? (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                      <th className="px-4 py-3 text-start font-semibold">{t("patient.conditionName")}</th>
                      <th className="px-4 py-3 text-start font-semibold">{t("patient.diagnosedOn")}</th>
                      <th className="px-4 py-3 text-start font-semibold">{t("patient.recordedAt")}</th>
                      <th className="px-4 py-3 text-start font-semibold">{t("common.status")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {historyQuery.data.items.map((entry) => (
                      <tr key={entry.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                        <td className="px-4 py-3">
                          <div className="font-medium text-slate-900">{entry.conditionName}</div>
                          {entry.notes ? <div className="mt-0.5 text-xs text-slate-500">{entry.notes}</div> : null}
                        </td>
                        <td className="px-4 py-3 text-slate-600">{formatDate(entry.diagnosedOn, locale)}</td>
                        <td className="px-4 py-3 text-slate-600">{formatDateTime(entry.recordedAtUtc, locale)}</td>
                        <td className="px-4 py-3">
                          {entry.isActive ? (
                            <Badge tone="green">{t("common.active")}</Badge>
                          ) : (
                            <Badge tone="slate">{t("common.inactive")}</Badge>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : null}

            {historyQuery.data && (historyQuery.data.totalCount ?? 0) > 0 ? (
              <div className="border-t border-slate-200">
                <Pagination
                  page={page}
                  pageSize={PAGE_SIZE}
                  totalCount={historyQuery.data.totalCount ?? 0}
                  onChange={setPage}
                />
              </div>
            ) : null}
          </Card>
        </div>
      ) : null}
    </div>
  );
}

export default MyHealthPage;