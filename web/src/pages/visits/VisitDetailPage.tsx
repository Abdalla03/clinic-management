import { Link, useParams } from "react-router-dom";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { Card, CardBody, CardHeader } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { useVisit } from "../../features/visits/hooks";
import { useAuth } from "../../features/auth/useAuth";
import { formatDateTime } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import { RoleDoctor } from "../../lib/roles";

export function VisitDetailPage() {
  const { visitId } = useParams<{ visitId: string }>();
  const { t, locale } = useI18n();
  const { hasRole } = useAuth();
  const isDoctor = hasRole(RoleDoctor);

  const visitQuery = useVisit(visitId);
  const visit = visitQuery.data;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("visit.details")}
        actions={
          <Link to="/visits">
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      {visitQuery.isLoading ? <LoadingState /> : null}

      {visitQuery.isError ? <EmptyState title={t("common.error")} /> : null}

      {!visitQuery.isLoading && !visitQuery.isError && !visit ? (
        <EmptyState title={t("common.noRecords")} />
      ) : null}

      {visit ? (
        <div className="flex flex-col gap-6">
          <Card>
            <CardHeader
              title={visit.patientName ?? "—"}
              actions={
                <div className="flex flex-wrap gap-2">
                  {isDoctor ? (
                    <>
                      <Link to={`/visits/${visit.id}/edit`}>
                        <Button variant="secondary" size="sm">
                          {t("visit.edit")}
                        </Button>
                      </Link>
                      <Link to={`/prescriptions/new?patientId=${visit.patientId ?? ""}`}>
                        <Button size="sm">{t("prescription.issue")}</Button>
                      </Link>
                    </>
                  ) : null}
                </div>
              }
            />
            <CardBody>
              <dl className="grid gap-4 text-sm sm:grid-cols-2">
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("visit.patient")}</dt>
                  <dd className="mt-0.5 text-slate-900">{visit.patientName ?? "—"}</dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("visit.doctor")}</dt>
                  <dd className="mt-0.5 text-slate-900">{visit.doctorName ?? "—"}</dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("visit.dateTime")}</dt>
                  <dd className="mt-0.5 text-slate-900">{formatDateTime(visit.visitAtUtc, locale)}</dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("visit.createdAt")}</dt>
                  <dd className="mt-0.5 text-slate-900">{formatDateTime(visit.createdAtUtc, locale)}</dd>
                </div>
                {visit.appointmentId ? (
                  <div>
                    <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("visit.appointment")}</dt>
                    <dd className="mt-0.5 text-slate-900">
                      <Badge tone="green">{t("appointment.title")}</Badge>
                    </dd>
                  </div>
                ) : null}
                <div className="sm:col-span-2">
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("visit.chiefComplaint")}</dt>
                  <dd className="mt-0.5 text-slate-900">{visit.chiefComplaint ?? "—"}</dd>
                </div>
                <div className="sm:col-span-2">
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("visit.diagnosis")}</dt>
                  <dd className="mt-0.5 text-slate-900">{visit.diagnosis ?? "—"}</dd>
                </div>
                <div className="sm:col-span-2">
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("visit.notes")}</dt>
                  <dd className="mt-0.5 text-slate-900">{visit.notes ?? "—"}</dd>
                </div>
              </dl>
            </CardBody>
          </Card>
        </div>
      ) : null}
    </div>
  );
}

export default VisitDetailPage;