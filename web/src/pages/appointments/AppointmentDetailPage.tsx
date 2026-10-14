import { useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { Card, CardBody, CardHeader } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { Field, Input, Textarea } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { IconCalendar } from "../../components/icons";
import {
  useAppointment,
  useCancelAppointment,
  useCompleteAppointment,
  useConfirmAppointment,
  useMarkNoShow,
  useRescheduleAppointment,
} from "../../features/appointments/hooks";
import { useAvailability } from "../../features/doctors/hooks";
import { useApiErrorMessage } from "../../features/errors/useApiErrorMessage";
import {
  appointmentStatusKey,
  appointmentStatusTone,
  durationMinutes as computeDurationMinutes,
  formatDateTime,
  formatTimeOnly,
} from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import type { TranslationKey } from "../../lib/i18n/translations";
import { useToast } from "../../lib/toast/useToast";
import { cn } from "../../lib/cn";

const StatusBooked = 1;
const StatusConfirmed = 2;
const TODAY_ISO = new Date().toISOString().slice(0, 10);

export function AppointmentDetailPage() {
  const { appointmentId } = useParams<{ appointmentId: string }>();
  const { t, locale } = useI18n();
  const toast = useToast();
  const apiMessage = useApiErrorMessage();

  const [rescheduleOpen, setRescheduleOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [date, setDate] = useState("");
  const [slot, setSlot] = useState("");
  const [rescheduleError, setRescheduleError] = useState("");
  const [cancelReason, setCancelReason] = useState("");

  const appointmentQuery = useAppointment(appointmentId);
  const appointment = appointmentQuery.data;

  const range = useMemo(() => {
    if (!date) {
      return undefined;
    }
    const from = new Date(`${date}T00:00:00`);
    const to = new Date(from.getTime() + 86_400_000);
    return { fromUtc: from.toISOString(), toUtc: to.toISOString() };
  }, [date]);

  const availabilityQuery = useAvailability(appointment?.doctorId ?? undefined, range);

  const confirmMutation = useConfirmAppointment();
  const completeMutation = useCompleteAppointment();
  const noShowMutation = useMarkNoShow();
  const rescheduleMutation = useRescheduleAppointment();
  const cancelMutation = useCancelAppointment();

  const runMutation = (mutation: ReturnType<typeof useConfirmAppointment>, successKey: TranslationKey) => {
    if (!appointmentId) {
      return;
    }
    mutation.mutate(appointmentId, {
      onSuccess: () => {
        toast.success({ title: t(successKey) });
        setRescheduleOpen(false);
        setCancelOpen(false);
      },
      onError: (error) => {
        const message = apiMessage(error);
        toast.error({ title: message.title, message: message.message });
      },
    });
  };

  const handleReschedule = () => {
    if (!appointment || !slot) {
      setRescheduleError(t("appointment.chooseSlotError"));
      return;
    }
    rescheduleMutation.mutate(
      {
        id: appointment.id as string,
        body: {
          startsAtUtc: slot,
          durationMinutes: computeDurationMinutes(appointment.startsAtUtc, appointment.endsAtUtc),
        },
      },
      {
        onSuccess: () => {
          toast.success({ title: t("appointment.rescheduleSuccess") });
          setRescheduleOpen(false);
          setCancelOpen(false);
          setSlot("");
        },
        onError: (error) => {
          const message = apiMessage(error);
          toast.error({ title: message.title, message: message.message });
        },
      },
    );
  };

  const handleCancel = () => {
    if (!appointmentId) {
      return;
    }
    cancelMutation.mutate(
      { id: appointmentId, body: { reason: cancelReason.trim() || undefined } },
      {
        onSuccess: () => {
          toast.success({ title: t("appointment.cancelSuccess") });
          setRescheduleOpen(false);
          setCancelOpen(false);
        },
        onError: (error) => {
          const message = apiMessage(error);
          toast.error({ title: message.title, message: message.message });
        },
      },
    );
  };

  const status = appointment?.status;
  const statusKey = appointmentStatusKey(status);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("appointment.details")}
        actions={
          <Link to="/appointments">
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      {appointmentQuery.isLoading ? <LoadingState /> : null}

      {appointmentQuery.isError ? <EmptyState title={t("common.error")} /> : null}

      {!appointmentQuery.isLoading && !appointmentQuery.isError && !appointment ? (
        <EmptyState title={t("patient.patientNotFound")} />
      ) : null}

      {appointment ? (
        <div className="flex flex-col gap-6">
          <Card>
            <CardHeader
              title={appointment.patientName ?? "—"}
              actions={
                statusKey ? (
                  <Badge tone="slate">
                    <span className={cn("rounded-full px-2 py-0.5 text-xs font-semibold", appointmentStatusTone(status))}>
                      {t(statusKey)}
                    </span>
                  </Badge>
                ) : undefined
              }
            />
            <CardBody>
              <dl className="grid gap-4 text-sm sm:grid-cols-2">
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("appointment.patient")}</dt>
                  <dd className="mt-0.5 text-slate-900">{appointment.patientName ?? "—"}</dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("appointment.doctor")}</dt>
                  <dd className="mt-0.5 text-slate-900">{appointment.doctorName ?? "—"}</dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("appointment.time")}</dt>
                  <dd className="mt-0.5 text-slate-900">
                    {formatDateTime(appointment.startsAtUtc, locale)}
                    <span className="text-slate-500"> → {formatTimeOnly(appointment.endsAtUtc, locale)}</span>
                  </dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("appointment.duration")}</dt>
                  <dd className="mt-0.5 text-slate-900">
                    {computeDurationMinutes(appointment.startsAtUtc, appointment.endsAtUtc)} min
                  </dd>
                </div>
                <div className="sm:col-span-2">
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("appointment.reason")}</dt>
                  <dd className="mt-0.5 text-slate-900">{appointment.reasonForVisit ?? "—"}</dd>
                </div>
                {appointment.confirmedAtUtc ? (
                  <div>
                    <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("appointment.confirmedAt")}</dt>
                    <dd className="mt-0.5 text-slate-900">{formatDateTime(appointment.confirmedAtUtc, locale)}</dd>
                  </div>
                ) : null}
                {appointment.completedAtUtc ? (
                  <div>
                    <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("appointment.completedAt")}</dt>
                    <dd className="mt-0.5 text-slate-900">{formatDateTime(appointment.completedAtUtc, locale)}</dd>
                  </div>
                ) : null}
                {appointment.noShowAtUtc ? (
                  <div>
                    <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("appointment.noShowAt")}</dt>
                    <dd className="mt-0.5 text-slate-900">{formatDateTime(appointment.noShowAtUtc, locale)}</dd>
                  </div>
                ) : null}
                {appointment.cancelledAtUtc ? (
                  <div className={appointment.cancellationReason ? "sm:col-span-1" : "sm:col-span-2"}>
                    <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("appointment.cancelledAt")}</dt>
                    <dd className="mt-0.5 text-slate-900">
                      {formatDateTime(appointment.cancelledAtUtc, locale)}
                      {appointment.cancellationReason ? (
                        <span className="block text-slate-500">{appointment.cancellationReason}</span>
                      ) : null}
                    </dd>
                  </div>
                ) : null}
              </dl>
            </CardBody>
          </Card>

          {status === StatusBooked || status === StatusConfirmed ? (
            <Card>
              <CardHeader title={t("appointment.title")} />
              <CardBody className="flex flex-col gap-4">
                <div className="flex flex-wrap gap-2">
                  {status === StatusBooked ? (
                    <Button loading={confirmMutation.isPending} onClick={() => runMutation(confirmMutation, "appointment.confirmSuccess")}>
                      {t("appointment.confirm")}
                    </Button>
                  ) : null}
                  {status === StatusConfirmed ? (
                    <Button loading={completeMutation.isPending} onClick={() => runMutation(completeMutation, "appointment.completeSuccess")}>
                      {t("appointment.complete")}
                    </Button>
                  ) : null}
                  <Button
                    variant="secondary"
                    loading={noShowMutation.isPending}
                    onClick={() => runMutation(noShowMutation, "appointment.noShowSuccess")}
                  >
                    {t("appointment.noShow")}
                  </Button>
                  <Button variant="secondary" onClick={() => setRescheduleOpen((open) => !open)}>
                    {t("appointment.reschedule")}
                  </Button>
                  <Button variant="danger" onClick={() => setCancelOpen((open) => !open)}>
                    {t("appointment.cancel")}
                  </Button>
                </div>

                {rescheduleOpen ? (
                  <div className="rounded-xl border border-slate-200 p-4">
                    <p className="mb-3 text-sm font-medium text-slate-700">{t("appointment.rescheduleHint")}</p>
                    <div className="mb-3">
                      <Field label={t("appointment.selectDate")} htmlFor="reschedule-date">
                        <Input
                          id="reschedule-date"
                          type="date"
                          value={date}
                          min={TODAY_ISO}
                          onChange={(event) => {
                            setDate(event.target.value);
                            setSlot("");
                            setRescheduleError("");
                          }}
                        />
                      </Field>
                    </div>
                    <div>
                      <p className="mb-2 text-sm font-medium text-slate-700">{t("appointment.slots")}</p>
                      {availabilityQuery.isLoading ? <LoadingState /> : null}
                      {!availabilityQuery.isLoading &&
                      !availabilityQuery.isError &&
                      (availabilityQuery.data?.length ?? 0) === 0 ? (
                        <EmptyState icon={<IconCalendar className="size-6 text-slate-300" />} title={t("appointment.noSlots")} />
                      ) : null}
                      {(availabilityQuery.data ?? []).length > 0 ? (
                        <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-3">
                          {(availabilityQuery.data ?? []).map((availableSlot) => {
                            const selected = availableSlot.startsAtUtc === slot;
                            return (
                              <button
                                key={availableSlot.startsAtUtc}
                                type="button"
                                onClick={() => {
                                  setSlot(availableSlot.startsAtUtc ?? "");
                                  setRescheduleError("");
                                }}
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
                      {rescheduleError ? <p className="mt-2 text-xs font-medium text-red-600">{rescheduleError}</p> : null}
                    </div>
                    <div className="mt-4 flex justify-end gap-2">
                      <Button variant="secondary" onClick={() => setRescheduleOpen(false)}>
                        {t("common.cancel")}
                      </Button>
                      <Button loading={rescheduleMutation.isPending} onClick={handleReschedule}>
                        {t("appointment.reschedule")}
                      </Button>
                    </div>
                  </div>
                ) : null}

                {cancelOpen ? (
                  <div className="rounded-xl border border-red-200 bg-red-50/50 p-4">
                    <p className="mb-3 text-sm font-medium text-slate-700">{t("appointment.cancelHint")}</p>
                    <Field label={t("appointment.cancelReason")} htmlFor="cancel-reason">
                      <Textarea
                        id="cancel-reason"
                        value={cancelReason}
                        onChange={(event) => setCancelReason(event.target.value)}
                        placeholder={t("appointment.cancelReason")}
                      />
                    </Field>
                    <div className="mt-4 flex justify-end gap-2">
                      <Button variant="secondary" onClick={() => setCancelOpen(false)}>
                        {t("common.back")}
                      </Button>
                      <Button variant="danger" loading={cancelMutation.isPending} onClick={handleCancel}>
                        {t("appointment.cancel")}
                      </Button>
                    </div>
                  </div>
                ) : null}
              </CardBody>
            </Card>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

export default AppointmentDetailPage;