import { useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Button } from "../../components/ui/Button";
import { Card, CardBody, CardHeader } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { Field, Input, Select, Textarea } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { IconCalendar, IconUsers } from "../../components/icons";
import { useBookAppointment, useBookMyAppointment } from "../../features/appointments/hooks";
import { useAuth } from "../../features/auth/useAuth";
import { useAvailability, useDoctors } from "../../features/doctors/hooks";
import { usePatientsSearch } from "../../features/patients/hooks";
import { useApiErrorMessage } from "../../features/errors/useApiErrorMessage";
import { formatDateTime, formatTimeOnly } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import { useDebouncedValue } from "../../lib/useDebouncedValue";
import { useToast } from "../../lib/toast/useToast";
import { cn } from "../../lib/cn";

const DEFAULT_DURATION_MINUTES = 30;
const TODAY_ISO = new Date().toISOString().slice(0, 10);

export function AppointmentBookingPage() {
  const { t, locale } = useI18n();
  const toast = useToast();
  const apiMessage = useApiErrorMessage();
  const navigate = useNavigate();
  const { isPatient } = useAuth();

  const [patientQuery, setPatientQuery] = useState("");
  const [patientId, setPatientId] = useState("");
  const [doctorId, setDoctorId] = useState("");
  const [date, setDate] = useState("");
  const [slot, setSlot] = useState("");
  const [reason, setReason] = useState("");
  const [errors, setErrors] = useState<{ patient?: string; doctor?: string; date?: string; slot?: string }>({});

  const debouncedPatientQuery = useDebouncedValue(patientQuery);
  const patientSearchQuery = usePatientsSearch(
    {
      query: debouncedPatientQuery.trim() || undefined,
      pageSize: 8,
    },
    !isPatient,
  );
  const doctorsQuery = useDoctors();

  const range = useMemo(() => {
    if (!date) {
      return undefined;
    }
    const from = new Date(`${date}T00:00:00`);
    const to = new Date(from.getTime() + 86_400_000);
    return { fromUtc: from.toISOString(), toUtc: to.toISOString() };
  }, [date]);

  const availabilityQuery = useAvailability(doctorId || undefined, range);
  const bookMutation = useBookAppointment();
  const bookMyMutation = useBookMyAppointment();

  const patients = patientSearchQuery.data?.items ?? [];
  const selectedPatient = patients.find((p) => p.id === patientId) ?? undefined;

  const handleBooked = (appointment: { id?: string; patientName?: string | null }) => {
    toast.success({ title: t("appointment.bookedSuccess", { patient: appointment.patientName ?? "" }) });
    navigate(isPatient ? "/appointments/me" : `/appointments/${appointment.id}`, { replace: true });
  };

  const handleError = (error: unknown) => {
    const message = apiMessage(error);
    toast.error({ title: message.title, message: message.message });
  };

  const handleSubmit = () => {
    const nextErrors: typeof errors = {};
    if (!isPatient && !patientId) {
      nextErrors.patient = t("appointment.choosePatientError");
    }
    if (!doctorId) {
      nextErrors.doctor = t("appointment.chooseDoctorError");
    }
    if (!date) {
      nextErrors.date = t("appointment.chooseSlotError");
    }
    if (!slot) {
      nextErrors.slot = t("appointment.chooseSlotError");
    }
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    if (isPatient) {
      bookMyMutation.mutate(
        {
          doctorId,
          startsAtUtc: slot,
          durationMinutes: DEFAULT_DURATION_MINUTES,
          reasonForVisit: reason.trim() || undefined,
        },
        { onSuccess: handleBooked, onError: handleError },
      );
      return;
    }

    bookMutation.mutate(
      {
        patientId,
        doctorId,
        startsAtUtc: slot,
        durationMinutes: DEFAULT_DURATION_MINUTES,
        reasonForVisit: reason.trim() || undefined,
      },
      { onSuccess: handleBooked, onError: handleError },
    );
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("appointment.new")}
        subtitle={t("appointment.book")}
        actions={
          <Link to={isPatient ? "/appointments/me" : "/appointments"}>
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      <div className="grid gap-6 lg:grid-cols-2">
        <div className="flex flex-col gap-6">
          {!isPatient ? (
            <Card>
              <CardHeader title={t("appointment.selectPatient")} />
              <CardBody className="flex flex-col gap-3">
                <Field label={t("appointment.selectPatient")} htmlFor="patient-search">
                  <Input
                    id="patient-search"
                    type="search"
                    value={patientQuery}
                    onChange={(event) => {
                      setPatientQuery(event.target.value);
                      setPatientId("");
                    }}
                    placeholder={t("appointment.patientSearchHint")}
                  />
                </Field>

                {patientSearchQuery.isError ? <p className="text-sm font-medium text-red-600">{t("common.error")}</p> : null}

                {!patientSearchQuery.isLoading && !patientSearchQuery.isError && patients.length === 0 ? (
                  <EmptyState icon={<IconUsers className="size-7 text-slate-300" />} title={t("appointment.patientNotFoundHint")} />
                ) : null}

                {patients.length > 0 ? (
                  <div className="flex max-h-64 flex-col gap-1.5 overflow-y-auto">
                    {patients.map((patient) => {
                      const selected = patient.id === patientId;
                      return (
                        <button
                          key={patient.id}
                          type="button"
                          onClick={() => setPatientId(patient.id ?? "")}
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
                ) : null}

                {errors.patient ? <p className="text-xs font-medium text-red-600">{errors.patient}</p> : null}
              </CardBody>
            </Card>
          ) : null}

          <Card>
            <CardHeader title={t("appointment.selectDoctor")} />
            <CardBody>
              <Field label={t("appointment.selectDoctor")} htmlFor="doctor">
                <Select
                  id="doctor"
                  value={doctorId}
                  onChange={(event) => {
                    setDoctorId(event.target.value);
                    setSlot("");
                  }}
                  invalid={Boolean(errors.doctor)}
                >
                  <option value="">{t("appointment.chooseDoctorError")}</option>
                  {(doctorsQuery.data ?? []).map((doctor) => (
                    <option key={doctor.id} value={doctor.id}>
                      {doctor.name}
                    </option>
                  ))}
                </Select>
              </Field>
              {errors.doctor ? <p className="mt-1.5 text-xs font-medium text-red-600">{errors.doctor}</p> : null}
            </CardBody>
          </Card>
        </div>

        <Card>
          <CardHeader title={t("appointment.selectDate")} />
          <CardBody className="flex flex-col gap-4">
            <Field label={t("appointment.selectDate")} htmlFor="appointment-date">
              <Input
                id="appointment-date"
                type="date"
                value={date}
                min={TODAY_ISO}
                onChange={(event) => {
                  setDate(event.target.value);
                  setSlot("");
                }}
                invalid={Boolean(errors.date)}
              />
            </Field>

            <div>
              <p className="mb-2 text-sm font-medium text-slate-700">{t("appointment.slots")}</p>

              {availabilityQuery.isLoading ? <LoadingState /> : null}

              {availabilityQuery.isError ? (
                <EmptyState title={t("common.error")} />
              ) : null}

              {!availabilityQuery.isLoading && !availabilityQuery.isError && (availabilityQuery.data?.length ?? 0) === 0 ? (
                <EmptyState icon={<IconCalendar className="size-7 text-slate-300" />} title={t("appointment.noSlots")} />
              ) : null}

              {(availabilityQuery.data ?? []).length > 0 ? (
                <div className="grid max-h-72 grid-cols-2 gap-1.5 overflow-y-auto sm:grid-cols-3">
                  {(availabilityQuery.data ?? []).map((availableSlot) => {
                    const selected = availableSlot.startsAtUtc === slot;
                    return (
                      <button
                        key={availableSlot.startsAtUtc}
                        type="button"
                        onClick={() => setSlot(availableSlot.startsAtUtc ?? "")}
                        className={cn(
                          "rounded-lg border px-2 py-2 text-sm font-medium transition-colors",
                          selected
                            ? "border-primary-500 bg-primary-50 text-primary-700"
                            : "border-slate-200 text-slate-700 hover:border-primary-300 hover:bg-slate-50",
                        )}
                      >
                        {formatTimeOnly(availableSlot.startsAtUtc, locale)}
                      </button>
                    );
                  })}
                </div>
              ) : null}

              {errors.slot ? <p className="mt-2 text-xs font-medium text-red-600">{errors.slot}</p> : null}
            </div>

            <Field label={t("appointment.reason")} htmlFor="reason">
              <Textarea
                id="reason"
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                placeholder={t("appointment.reason")}
              />
            </Field>

            {slot ? (
              <p className="text-sm text-slate-600">
                <span className="font-semibold text-slate-800">{formatDateTime(slot, locale)}</span>
                {selectedPatient
                  ? ` — ${selectedPatient.firstName} ${selectedPatient.lastName}`
                  : ""}
              </p>
            ) : null}

            <div className="flex justify-end gap-2">
              <Link to={isPatient ? "/appointments/me" : "/appointments"}>
                <Button variant="secondary">{t("common.cancel")}</Button>
              </Link>
              <Button
                loading={bookMutation.isPending || bookMyMutation.isPending}
                onClick={handleSubmit}
              >
                {t("appointment.book")}
              </Button>
            </div>
          </CardBody>
        </Card>
      </div>
    </div>
  );
}

export default AppointmentBookingPage;