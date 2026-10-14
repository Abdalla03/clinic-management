import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Button } from "../../components/ui/Button";
import { Card, CardBody, CardHeader } from "../../components/ui/Card";
import { Field, Input, Textarea } from "../../components/ui/Field";
import { PageHeader } from "../../components/ui/PageHeader";
import { IconPlus, IconTrash } from "../../components/icons";
import { useIssuePrescription } from "../../features/prescriptions/hooks";
import { usePatientsSearch } from "../../features/patients/hooks";
import { useApiErrorMessage } from "../../features/errors/useApiErrorMessage";
import type { PrescriptionItemRequest } from "../../lib/api/prescriptions";
import { useI18n } from "../../lib/i18n/useI18n";
import { useDebouncedValue } from "../../lib/useDebouncedValue";
import { useToast } from "../../lib/toast/useToast";
import { cn } from "../../lib/cn";

interface ItemRow {
  drugName: string;
  dose: string;
  route: string;
  timesPerDay: string;
  durationDays: string;
  instructions: string;
  doseTimes: string;
}

const emptyItem: ItemRow = {
  drugName: "",
  dose: "",
  route: "",
  timesPerDay: "1",
  durationDays: "7",
  instructions: "",
  doseTimes: "",
};

function toRequestItem(item: ItemRow, index: number, errors: Array<Record<string, string> | undefined>): PrescriptionItemRequest {
  errors[index] = {};
  const rowErrors = errors[index];
  if (!item.drugName.trim()) {
    rowErrors.drugName = "prescription.medicineRequired";
  }
  if (!item.dose.trim()) {
    rowErrors.dose = "prescription.doseRequired";
  }
  const timesPerDay = item.timesPerDay.trim() ? Number(item.timesPerDay) : 1;
  if (!Number.isInteger(timesPerDay) || timesPerDay < 1 || timesPerDay > 12) {
    rowErrors.timesPerDay = "prescription.timesPerDayRange";
  }
  const durationDays = item.durationDays.trim() ? Number(item.durationDays) : 7;
  if (!Number.isInteger(durationDays) || durationDays < 1 || durationDays > 365) {
    rowErrors.durationDays = "prescription.durationRange";
  }
  const doseTimes = item.doseTimes
    .split(",")
    .map((time) => time.trim())
    .filter(Boolean);
  if (doseTimes.length > 0 && doseTimes.length !== timesPerDay) {
    rowErrors.doseTimes = "prescription.doseTimesError";
  }

  return {
    drugName: item.drugName.trim(),
    dose: item.dose.trim(),
    route: item.route.trim() || undefined,
    timesPerDay,
    durationDays,
    instructions: item.instructions.trim() || undefined,
    doseTimes: doseTimes.length > 0 ? doseTimes : undefined,
  };
}

function isRowError(row: Record<string, string> | undefined): boolean {
  return Object.keys(row ?? {}).length > 0;
}

export function IssuePrescriptionPage() {
  const [searchParams] = useSearchParams();
  const preselectedPatientId = searchParams.get("patientId") ?? "";
  const preselectedVisitId = searchParams.get("visitId") ?? "";
  const { t } = useI18n();
  const toast = useToast();
  const apiMessage = useApiErrorMessage();
  const navigate = useNavigate();

  const [patientId, setPatientId] = useState(preselectedPatientId);
  const [visitId, setVisitId] = useState(preselectedVisitId);
  const [notes, setNotes] = useState("");
  const [rows, setRows] = useState<ItemRow[]>(() => [emptyItem]);
  const [rowErrors, setRowErrors] = useState<Array<Record<string, string> | undefined>>([undefined]);
  const [patientError, setPatientError] = useState("");
  const [patientQuery, setPatientQuery] = useState("");

  const debouncedPatientQuery = useDebouncedValue(patientQuery);
  const patientSearchQuery = usePatientsSearch({
    query: debouncedPatientQuery.trim() || undefined,
    pageSize: 6,
  });
  const patients = patientSearchQuery.data?.items ?? [];
  const issueMutation = useIssuePrescription();

  const updateRow = (index: number, patch: Partial<ItemRow>) => {
    setRows((current) => current.map((row, i) => (i === index ? { ...row, ...patch } : row)));
    setRowErrors((current) => {
      const next = [...current];
      next[index] = undefined;
      return next;
    });
  };

  const addRow = () => {
    setRows((current) => [...current, emptyItem]);
    setRowErrors((current) => [...current, undefined]);
  };

  const removeRow = (index: number) => {
    setRows((current) => current.filter((_, i) => i !== index));
    setRowErrors((current) => current.filter((_, i) => i !== index));
  };

  const handleSubmit = () => {
    if (!patientId) {
      setPatientError(t("prescription.choosePatientError"));
      return;
    }
    setPatientError("");

    const errors: Array<Record<string, string> | undefined> = rows.map(() => undefined);
    const items = rows.map((row, index) => toRequestItem(row, index, errors));
    setRowErrors(errors);

    if (errors.some(isRowError)) {
      toast.error({ title: t("prescription.itemErrors") });
      return;
    }

    issueMutation.mutate(
      {
        patientId,
        visitId: visitId || undefined,
        notes: notes.trim() || undefined,
        items,
      },
      {
        onSuccess: (prescription) => {
          toast.success({ title: t("prescription.issuedSuccess") });
          const hasAllergyMatch = (prescription.items ?? []).some(
            (item) => item.allergyCheckOutcome === 3,
          );
          if (hasAllergyMatch) {
            toast.info({ title: t("prescription.allergyMatchedWarning") });
          }
          navigate(`/prescriptions/${prescription.id}`, { replace: true });
        },
        onError: (error) => {
          const message = apiMessage(error);
          toast.error({ title: message.title, message: message.message });
        },
      },
    );
  };

  const busy = issueMutation.isPending;

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <PageHeader
        title={t("prescription.issue")}
        actions={
          <Link to="/prescriptions">
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      <Card>
        <CardHeader title={t("prescription.new")} />
        <CardBody className="flex flex-col gap-5">
          <Field label={t("prescription.patient")} htmlFor="prescription-patient-search" required>
            <Input
              id="prescription-patient-search"
              type="search"
              value={patientQuery}
              onChange={(event) => {
                setPatientQuery(event.target.value);
                setPatientId("");
              }}
              placeholder={t("appointment.patientSearchHint")}
              invalid={Boolean(patientError)}
            />
          </Field>
          {patients.length > 0 ? (
            <div className="-mt-3 flex max-h-52 flex-col gap-1.5 overflow-y-auto">
              {patients.map((patient) => {
                const selected = patient.id === patientId;
                return (
                  <button
                    key={patient.id}
                    type="button"
                    onClick={() => {
                      setPatientId(patient.id ?? "");
                      setPatientError("");
                    }}
                    className={cn(
                      "flex items-start justify-between gap-3 rounded-lg border p-3 text-start transition-colors",
                      selected
                        ? "border-primary-500 bg-primary-50"
                        : "border-slate-200 bg-white hover:border-primary-300 hover:bg-slate-50",
                    )}
                  >
                    <span className="text-sm font-medium text-slate-900">
                      {patient.firstName} {patient.lastName}
                    </span>
                    <span className="font-mono text-xs text-slate-500">{patient.medicalRecordNumber}</span>
                  </button>
                );
              })}
            </div>
          ) : patientSearchQuery.isFetched && debouncedPatientQuery ? (
            <p className="-mt-3 text-xs text-slate-500">{t("appointment.patientNotFoundHint")}</p>
          ) : null}
          {patientError ? <p className="-mt-3 text-xs font-medium text-red-600">{patientError}</p> : null}

          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t("prescription.visit")} htmlFor="prescription-visit">
              <Input
                id="prescription-visit"
                value={visitId || ""}
                onChange={(event) => setVisitId(event.target.value)}
                placeholder="00000000-0000-0000-0000-000000000000"
              />
            </Field>
            <Field label={t("prescription.notes")} htmlFor="prescription-notes">
              <Textarea
                id="prescription-notes"
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
                placeholder={t("prescription.notes")}
              />
            </Field>
          </div>

          <hr className="border-slate-100" />

          <div className="flex items-center justify-between">
            <span className="text-sm font-semibold text-slate-900">{t("prescription.items")}</span>
            <Button variant="secondary" size="sm" onClick={addRow} disabled={rows.length >= 50}>
              <IconPlus />
              {t("prescription.addItem")}
            </Button>
          </div>

          {rows.map((row, index) => {
            const errors = rowErrors[index] ?? {};
            return (
              <div key={index} className="flex flex-col gap-3 rounded-xl border border-slate-200 p-4">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                    {index + 1}
                  </span>
                  {rows.length > 1 ? (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => removeRow(index)}
                      aria-label={t("prescription.removeItem")}
                    >
                      <IconTrash />
                    </Button>
                  ) : null}
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label={t("prescription.drugName")} htmlFor={`drug-${index}`} required>
                    <Input
                      id={`drug-${index}`}
                      value={row.drugName}
                      onChange={(event) => updateRow(index, { drugName: event.target.value })}
                      invalid={Boolean(errors.drugName)}
                    />
                  </Field>
                  <Field label={t("prescription.dose")} htmlFor={`dose-${index}`} required>
                    <Input
                      id={`dose-${index}`}
                      value={row.dose}
                      onChange={(event) => updateRow(index, { dose: event.target.value })}
                      invalid={Boolean(errors.dose)}
                    />
                  </Field>
                </div>
                <div className="grid gap-3 sm:grid-cols-3">
                  <Field label={t("prescription.route")} htmlFor={`route-${index}`}>
                    <Input
                      id={`route-${index}`}
                      value={row.route}
                      onChange={(event) => updateRow(index, { route: event.target.value })}
                    />
                  </Field>
                  <Field label={t("prescription.timesPerDay")} htmlFor={`times-${index}`}>
                    <Input
                      id={`times-${index}`}
                      type="number"
                      min={1}
                      max={12}
                      value={row.timesPerDay}
                      onChange={(event) => updateRow(index, { timesPerDay: event.target.value })}
                      invalid={Boolean(errors.timesPerDay)}
                    />
                  </Field>
                  <Field label={t("prescription.durationDays")} htmlFor={`duration-${index}`}>
                    <Input
                      id={`duration-${index}`}
                      type="number"
                      min={1}
                      max={365}
                      value={row.durationDays}
                      onChange={(event) => updateRow(index, { durationDays: event.target.value })}
                      invalid={Boolean(errors.durationDays)}
                    />
                  </Field>
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field
                    label={t("prescription.doseTimes")}
                    htmlFor={`doseTimes-${index}`}
                    hint={t("prescription.doseTimesHint")}
                  >
                    <Input
                      id={`doseTimes-${index}`}
                      value={row.doseTimes}
                      onChange={(event) => updateRow(index, { doseTimes: event.target.value })}
                      invalid={Boolean(errors.doseTimes)}
                    />
                  </Field>
                  <Field label={t("prescription.instructions")} htmlFor={`instructions-${index}`}>
                    <Input
                      id={`instructions-${index}`}
                      value={row.instructions}
                      onChange={(event) => updateRow(index, { instructions: event.target.value })}
                    />
                  </Field>
                </div>
                {Object.keys(errors).length > 0 ? (
                  <div className="flex flex-col gap-1">
                    {Object.entries(errors).map(([key, keyName]) => (
                      <p key={key} className="text-xs font-medium text-red-600">
                        {keyName}
                      </p>
                    ))}
                  </div>
                ) : null}
              </div>
            );
          })}

          <div className="flex justify-end gap-3">
            <Link to="/prescriptions">
              <Button variant="secondary" type="button" disabled={busy}>
                {t("common.cancel")}
              </Button>
            </Link>
            <Button loading={busy} onClick={handleSubmit}>
              {t("prescription.issue")}
            </Button>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}

export default IssuePrescriptionPage;