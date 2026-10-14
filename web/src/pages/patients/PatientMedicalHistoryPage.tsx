import { zodResolver } from "@hookform/resolvers/zod";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useParams } from "react-router-dom";
import { z } from "zod";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { Card, CardBody, CardHeader } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { Field, Input, Textarea } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { Pagination } from "../../components/ui/Pagination";
import { IconPlus } from "../../components/icons";
import { useAddMedicalHistory, useMedicalHistory, usePatient } from "../../features/patients/hooks";
import { useApiErrorMessage } from "../../features/errors/useApiErrorMessage";
import { formatDate, formatDateTime } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import { useToast } from "../../lib/toast/useToast";

const entrySchema = z.object({
  conditionName: z.string().trim().min(1),
  diagnosedOn: z.string().optional(),
  notes: z.string().optional(),
});

type EntryValues = z.infer<typeof entrySchema>;

const PAGE_SIZE = 10;
const TODAY_ISO = new Date().toISOString().slice(0, 10);

export function PatientMedicalHistoryPage() {
  const { patientId } = useParams<{ patientId: string }>();
  const { t, locale } = useI18n();
  const toast = useToast();
  const apiMessage = useApiErrorMessage();

  const [page, setPage] = useState(1);

  const patientQuery = usePatient(patientId);
  const historyQuery = useMedicalHistory(patientId, { page, pageSize: PAGE_SIZE });
  const addMutation = useAddMedicalHistory(patientId ?? "");

  const form = useForm<EntryValues>({
    resolver: zodResolver(entrySchema),
    defaultValues: { conditionName: "", diagnosedOn: "", notes: "" },
  });

  const entries = historyQuery.data?.items ?? [];

  const onSubmit = (values: EntryValues) => {
    if (!patientId) {
      return;
    }
    addMutation.mutate(
      {
        conditionName: values.conditionName,
        diagnosedOn: values.diagnosedOn || undefined,
        notes: values.notes || undefined,
      },
      {
        onSuccess: () => {
          form.reset();
          setPage(1);
          toast.success({ title: t("patient.conditionAdded") });
        },
        onError: (error) => {
          const message = apiMessage(error);
          toast.error({ title: message.title, message: message.message });
        },
      },
    );
  };

  const patientName = useMemo(
    () => patientQuery.data?.fullName ?? [patientQuery.data?.firstName, patientQuery.data?.lastName].join(" "),
    [patientQuery.data],
  );

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("patient.medicalHistoryTitle")}
        subtitle={patientName || undefined}
        actions={
          <Link to="/patients">
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      {patientQuery.isLoading ? <LoadingState /> : null}

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader title={t("patient.medicalHistoryTitle")} />
          {historyQuery.isLoading ? <LoadingState /> : null}

          {historyQuery.isError ? (
            <EmptyState title={t("common.error")} />
          ) : null}

          {!historyQuery.isLoading && !historyQuery.isError && entries.length === 0 ? (
            <EmptyState title={t("patient.noHistory")} />
          ) : null}

          {!historyQuery.isLoading && !historyQuery.isError && entries.length > 0 ? (
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
                  {entries.map((entry) => (
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

        <Card>
          <CardHeader title={t("patient.addHistoryEntry")} />
          <CardBody>
            <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4">
              <Field label={t("patient.conditionName")} htmlFor="conditionName" required>
                <Input
                  id="conditionName"
                  invalid={Boolean(form.formState.errors.conditionName)}
                  placeholder={t("patient.conditionName")}
                  {...form.register("conditionName")}
                />
              </Field>
              <Field label={t("patient.diagnosedOn")} htmlFor="diagnosedOn">
                <Input id="diagnosedOn" type="date" max={TODAY_ISO} {...form.register("diagnosedOn")} />
              </Field>
              <Field label={t("patient.notes")} htmlFor="history-notes">
                <Textarea id="history-notes" {...form.register("notes")} />
              </Field>
              <Button type="submit" loading={addMutation.isPending} className="w-fit">
                <IconPlus />
                {t("patient.addHistoryEntry")}
              </Button>
            </form>
          </CardBody>
        </Card>
      </div>
    </div>
  );
}

export default PatientMedicalHistoryPage;