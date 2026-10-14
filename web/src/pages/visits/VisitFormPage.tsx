import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { z } from "zod";
import { Button } from "../../components/ui/Button";
import { Card, CardBody } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { Field, Input, Select, Textarea } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { useRecordVisit, useUpdateVisit, useVisit } from "../../features/visits/hooks";
import { usePatient, usePatientsSearch } from "../../features/patients/hooks";
import { useDoctors } from "../../features/doctors/hooks";
import { useApiErrorMessage } from "../../features/errors/useApiErrorMessage";
import { formatDateTime } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import { useDebouncedValue } from "../../lib/useDebouncedValue";
import { useToast } from "../../lib/toast/useToast";
import { cn } from "../../lib/cn";

const DEFAULT_NOW = (() => {
  const date = new Date();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  const hours = String(date.getHours()).padStart(2, "0");
  const minutes = String(date.getMinutes()).padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}T${hours}:${minutes}`;
})();

const textSchema = z.object({
  chiefComplaint: z.string().optional(),
  notes: z.string().optional(),
  diagnosis: z.string().optional(),
});

type TextValues = z.infer<typeof textSchema>;

export function VisitFormPage() {
  const { visitId } = useParams<{ visitId: string }>();
  const isEdit = Boolean(visitId);
  const [searchParams] = useSearchParams();
  const { t, locale } = useI18n();
  const toast = useToast();
  const apiMessage = useApiErrorMessage();
  const navigate = useNavigate();

  const [patientId, setPatientId] = useState(() => searchParams.get("patientId") ?? "");
  const [doctorId, setDoctorId] = useState("");
  const [visitAt, setVisitAt] = useState(DEFAULT_NOW);
  const [errors, setErrors] = useState<{ patientId?: string; doctorId?: string; visitAt?: string }>({});
  const [patientQuery, setPatientQuery] = useState("");

  const visitQuery = useVisit(visitId);
  const patientQueryData = usePatient(isEdit ? undefined : (patientId || undefined));
  const recordMutation = useRecordVisit();
  const updateMutation = useUpdateVisit();

  const patientName = useMemo(() => {
    if (isEdit) {
      return visitQuery.data?.patientName ?? undefined;
    }
    const data = patientQueryData.data;
    if (!data) {
      return undefined;
    }
    return data.fullName ?? ([data.firstName, data.lastName].filter(Boolean).join(" ") || undefined);
  }, [isEdit, visitQuery.data, patientQueryData.data]);

  const form = useForm<TextValues>({
    resolver: zodResolver(textSchema),
    defaultValues: { chiefComplaint: "", notes: "", diagnosis: "" },
  });

  useEffect(() => {
    if (!visitId || !visitQuery.data) {
      return;
    }
    const data = visitQuery.data;
    form.reset({
      chiefComplaint: data.chiefComplaint ?? "",
      notes: data.notes ?? "",
      diagnosis: data.diagnosis ?? "",
    });
  }, [visitId, visitQuery.data, form]);

  const debouncedPatientQuery = useDebouncedValue(patientQuery);
  const patientSearchQuery = usePatientsSearch({
    query: debouncedPatientQuery.trim() || undefined,
    pageSize: 6,
  });
  const patients = patientSearchQuery.data?.items ?? [];

  const doctorsQuery = useDoctors();

  const handleSubmit = () => {
    if (!isEdit) {
      const nextErrors: typeof errors = {};
      if (!patientId) {
        nextErrors.patientId = t("visit.choosePatientError");
      }
      if (!doctorId) {
        nextErrors.doctorId = t("visit.chooseDoctorError");
      }
      if (!visitAt) {
        nextErrors.visitAt = t("visit.chooseDateError");
      }
      setErrors(nextErrors);
      if (Object.keys(nextErrors).length > 0) {
        return;
      }
    }

    const text = form.getValues();

    if (isEdit && visitId) {
      updateMutation.mutate(
        {
          id: visitId,
          body: {
            chiefComplaint: text.chiefComplaint?.trim() || undefined,
            notes: text.notes?.trim() || undefined,
            diagnosis: text.diagnosis?.trim() || undefined,
          },
        },
        {
          onSuccess: () => {
            toast.success({ title: t("visit.updatedSuccess") });
            navigate(`/visits/${visitId}`, { replace: true });
          },
          onError: (error) => {
            const message = apiMessage(error);
            toast.error({ title: message.title, message: message.message });
          },
        },
      );
      return;
    }

    recordMutation.mutate(
      {
        patientId,
        doctorId,
        visitAtUtc: new Date(visitAt).toISOString(),
        chiefComplaint: text.chiefComplaint?.trim() || undefined,
        notes: text.notes?.trim() || undefined,
        diagnosis: text.diagnosis?.trim() || undefined,
      },
      {
        onSuccess: (visit) => {
          toast.success({ title: t("visit.recordedSuccess") });
          navigate(`/visits/${visit.id}`, { replace: true });
        },
        onError: (error) => {
          const message = apiMessage(error);
          toast.error({ title: message.title, message: message.message });
        },
      },
    );
  };

  const busy = recordMutation.isPending || updateMutation.isPending;

  if (isEdit && visitQuery.isLoading) {
    return <LoadingState />;
  }

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <PageHeader
        title={isEdit ? t("visit.edit") : t("visit.record")}
        actions={
          <Link to={isEdit && visitId ? `/visits/${visitId}` : "/visits"}>
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      <Card>
        <CardBody className="flex flex-col gap-5">
          {isEdit ? (
            <>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label={t("visit.patient")}>
                  <Input value={patientName ?? "—"} disabled />
                </Field>
                <Field label={t("visit.doctor")}>
                  <Input value={visitQuery.data?.doctorName ?? "—"} disabled />
                </Field>
              </div>
              <Field label={t("visit.dateTime")}>
                <Input value={formatDateTime(visitQuery.data?.visitAtUtc, locale)} disabled />
              </Field>
              <hr className="border-slate-100" />
            </>
          ) : (
            <>
              <div>
                <Field label={t("visit.patient")} htmlFor="visit-patient-search" required>
                  <Input
                    id="visit-patient-search"
                    type="search"
                    value={patientQuery}
                    onChange={(event) => {
                      setPatientQuery(event.target.value);
                      setPatientId("");
                    }}
                    placeholder={t("appointment.patientSearchHint")}
                    invalid={Boolean(errors.patientId)}
                  />
                </Field>
                {patients.length > 0 ? (
                  <div className="mt-2 flex max-h-52 flex-col gap-1.5 overflow-y-auto">
                    {patients.map((patient) => {
                      const selected = patient.id === patientId;
                      return (
                        <button
                          key={patient.id}
                          type="button"
                          onClick={() => {
                            setPatientId(patient.id ?? "");
                            setErrors((current) => ({ ...current, patientId: undefined }));
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
                  <p className="mt-2 text-xs text-slate-500">{t("appointment.patientNotFoundHint")}</p>
                ) : null}
                {errors.patientId ? (
                  <p className="mt-1.5 text-xs font-medium text-red-600">{errors.patientId}</p>
                ) : null}
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <Field label={t("visit.doctor")} htmlFor="visit-doctor" required>
                  <Select
                    id="visit-doctor"
                    value={doctorId}
                    onChange={(event) => {
                      setDoctorId(event.target.value);
                      setErrors((current) => ({ ...current, doctorId: undefined }));
                    }}
                    invalid={Boolean(errors.doctorId)}
                  >
                    <option value="">{t("visit.chooseDoctorError")}</option>
                    {(doctorsQuery.data ?? []).map((doctor) => (
                      <option key={doctor.id} value={doctor.id}>
                        {doctor.name}
                      </option>
                    ))}
                  </Select>
                </Field>
                <Field label={t("visit.dateTime")} htmlFor="visit-at" required>
                  <Input
                    id="visit-at"
                    type="datetime-local"
                    value={visitAt}
                    onChange={(event) => {
                      setVisitAt(event.target.value);
                      setErrors((current) => ({ ...current, visitAt: undefined }));
                    }}
                    invalid={Boolean(errors.visitAt)}
                  />
                </Field>
              </div>
              {errors.doctorId || errors.visitAt ? (
                <div className="flex flex-col gap-1">
                  {errors.doctorId ? <p className="text-xs font-medium text-red-600">{errors.doctorId}</p> : null}
                  {errors.visitAt ? <p className="text-xs font-medium text-red-600">{errors.visitAt}</p> : null}
                </div>
              ) : null}
              <hr className="border-slate-100" />
            </>
          )}

          <Field label={t("visit.chiefComplaint")} htmlFor="chiefComplaint">
            <Textarea
              id="chiefComplaint"
              placeholder={t("visit.chiefComplaint")}
              {...form.register("chiefComplaint")}
            />
          </Field>

          <Field label={t("visit.diagnosis")} htmlFor="diagnosis">
            <Textarea id="diagnosis" placeholder={t("visit.diagnosis")} {...form.register("diagnosis")} />
          </Field>

          <Field label={t("visit.notes")} htmlFor="notes">
            <Textarea id="notes" placeholder={t("visit.notes")} {...form.register("notes")} />
          </Field>

          <div className="flex justify-end gap-3">
            <Link to={isEdit && visitId ? `/visits/${visitId}` : "/visits"}>
              <Button variant="secondary" type="button" disabled={busy}>
                {t("common.cancel")}
              </Button>
            </Link>
            <Button loading={busy} onClick={handleSubmit}>
              {t("common.save")}
            </Button>
          </div>
        </CardBody>
      </Card>

      {isEdit && visitQuery.isError ? <EmptyState title={t("common.error")} /> : null}
    </div>
  );
}

export default VisitFormPage;