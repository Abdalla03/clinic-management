import { Link, useParams } from "react-router-dom";
import { Button } from "../../components/ui/Button";
import { Card, CardBody, CardHeader } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { usePrescription } from "../../features/prescriptions/hooks";
import { useAuth } from "../../features/auth/useAuth";
import { allergyOutcomeKey, allergyOutcomeTone, formatDateTime, formatTime } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import { RoleDoctor } from "../../lib/roles";

export function PrescriptionDetailPage() {
  const { prescriptionId } = useParams<{ prescriptionId: string }>();
  const { t, locale } = useI18n();
  const { hasRole } = useAuth();
  const isDoctor = hasRole(RoleDoctor);

  const prescriptionQuery = usePrescription(prescriptionId);
  const prescription = prescriptionQuery.data;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("prescription.details")}
        actions={
          <Link to="/prescriptions">
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      {prescriptionQuery.isLoading ? <LoadingState /> : null}

      {prescriptionQuery.isError ? <EmptyState title={t("common.error")} /> : null}

      {!prescriptionQuery.isLoading && !prescriptionQuery.isError && !prescription ? (
        <EmptyState title={t("common.noRecords")} />
      ) : null}

      {prescription ? (
        <div className="flex flex-col gap-6">
          <Card>
            <CardHeader
              title={prescription.patientName ?? "—"}
              subtitle={formatDateTime(prescription.issuedAtUtc, locale)}
              actions={
                isDoctor ? (
                  <Link to={`/prescriptions/new?patientId=${prescription.patientId ?? ""}`}>
                    <Button size="sm">{t("prescription.issue")}</Button>
                  </Link>
                ) : undefined
              }
            />
            <CardBody>
              <dl className="grid gap-4 text-sm sm:grid-cols-2">
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("prescription.patient")}</dt>
                  <dd className="mt-0.5 text-slate-900">{prescription.patientName ?? "—"}</dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("prescription.doctor")}</dt>
                  <dd className="mt-0.5 text-slate-900">{prescription.doctorName ?? "—"}</dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("prescription.issuedAt")}</dt>
                  <dd className="mt-0.5 text-slate-900">{formatDateTime(prescription.issuedAtUtc, locale)}</dd>
                </div>
                {prescription.visitId ? (
                  <div>
                    <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("prescription.visit")}</dt>
                    <dd className="mt-0.5 text-slate-900">
                      <Link
                        to={`/visits/${prescription.visitId}`}
                        className="text-primary-600 hover:underline"
                      >
                        {t("prescription.viewVisit")}
                      </Link>
                    </dd>
                  </div>
                ) : null}
                {prescription.notes ? (
                  <div className="sm:col-span-2">
                    <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("prescription.notes")}</dt>
                    <dd className="mt-0.5 text-slate-900">{prescription.notes}</dd>
                  </div>
                ) : null}
              </dl>
            </CardBody>
          </Card>

          <Card className="overflow-hidden">
            <CardHeader title={t("prescription.items")} />
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                    <th className="px-4 py-3 text-start font-semibold">{t("prescription.drugName")}</th>
                    <th className="px-4 py-3 text-start font-semibold">{t("prescription.dose")}</th>
                    <th className="px-4 py-3 text-start font-semibold">{t("prescription.route")}</th>
                    <th className="px-4 py-3 text-start font-semibold">{t("prescription.timesPerDay")}</th>
                    <th className="px-4 py-3 text-start font-semibold">{t("prescription.durationDays")}</th>
                    <th className="px-4 py-3 text-start font-semibold">{t("prescription.doseTimes")}</th>
                    <th className="px-4 py-3 text-end font-semibold">{t("prescription.allergyCheck")}</th>
                  </tr>
                </thead>
                <tbody>
                  {(prescription.items ?? []).map((item) => {
                    const allergyKey = allergyOutcomeKey(item.allergyCheckOutcome);
                    return (
                      <tr key={item.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                        <td className="px-4 py-3">
                          <div className="font-medium text-slate-900">{item.drugName}</div>
                          {item.instructions ? (
                            <div className="mt-0.5 text-xs text-slate-500">{item.instructions}</div>
                          ) : null}
                        </td>
                        <td className="px-4 py-3 text-slate-600">{item.dose ?? "—"}</td>
                        <td className="px-4 py-3 text-slate-600">{item.route ?? "—"}</td>
                        <td className="px-4 py-3 text-slate-600">{item.timesPerDay ?? "—"}</td>
                        <td className="px-4 py-3 text-slate-600">{item.durationDays ?? "—"}</td>
                        <td className="px-4 py-3 text-slate-600">
                          {(item.doseTimes ?? []).length > 0
                            ? item.doseTimes!.map((time) => formatTime(time, locale)).join(", ")
                            : "—"}
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex items-center justify-end">
                            {allergyKey ? (
                              <span
                                className={`inline-block rounded-full px-2 py-0.5 text-[10px] font-semibold ${allergyOutcomeTone(
                                  item.allergyCheckOutcome,
                                )}`}
                              >
                                {t(allergyKey)}
                              </span>
                            ) : (
                              <span className="text-slate-400">—</span>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </Card>
        </div>
      ) : null}
    </div>
  );
}

export default PrescriptionDetailPage;