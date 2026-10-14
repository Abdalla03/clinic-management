import { useState } from "react";
import { Button } from "../../components/ui/Button";
import { Card, CardBody, CardHeader } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { Field, Input, Select } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { IconCalendar } from "../../components/icons";
import { useAddSchedule, useDeactivateSchedule, useDoctorSchedule, useDoctors } from "../../features/doctors/hooks";
import { useApiErrorMessage } from "../../features/errors/useApiErrorMessage";
import { dayOfWeekKey, formatTime } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import { useToast } from "../../lib/toast/useToast";

const DAYS = [0, 1, 2, 3, 4, 5, 6];

function toTimeOnly(value: string): string {
  return value ? `${value}:00` : "";
}

export function DoctorSchedulePage() {
  const { t, locale } = useI18n();
  const toast = useToast();
  const apiMessage = useApiErrorMessage();

  const [doctorId, setDoctorId] = useState("");
  const [day, setDay] = useState("0");
  const [startsAt, setStartsAt] = useState("");
  const [endsAt, setEndsAt] = useState("");
  const [slotMinutes, setSlotMinutes] = useState("30");
  const [formError, setFormError] = useState("");

  const doctorsQuery = useDoctors();
  const scheduleQuery = useDoctorSchedule(doctorId || undefined);
  const addMutation = useAddSchedule(doctorId || undefined);
  const deactivateMutation = useDeactivateSchedule(doctorId || undefined);

  const doctors = doctorsQuery.data ?? [];

  const handleAdd = () => {
    if (!doctorId) {
      setFormError(t("appointment.chooseDoctorError"));
      return;
    }
    if (!startsAt || !endsAt) {
      setFormError(t("appointment.chooseSlotError"));
      return;
    }
    if (endsAt <= startsAt) {
      setFormError(t("schedule.endAfterStartError"));
      return;
    }
    const parsedSlot = Number(slotMinutes);
    if (!Number.isFinite(parsedSlot) || parsedSlot < 5) {
      setFormError(t("schedule.slotError"));
      return;
    }
    setFormError("");
    addMutation.mutate(
      {
        dayOfWeek: Number(day) as 0 | 1 | 2 | 3 | 4 | 5 | 6,
        startsAt: toTimeOnly(startsAt),
        endsAt: toTimeOnly(endsAt),
        slotMinutes: parsedSlot,
      },
      {
        onSuccess: () => {
          toast.success({ title: t("schedule.addSuccess") });
          setStartsAt("");
          setEndsAt("");
          setSlotMinutes("30");
        },
        onError: (error) => {
          const message = apiMessage(error);
          toast.error({ title: message.title, message: message.message });
        },
      },
    );
  };

  const handleDeactivate = (scheduleId: string) => {
    deactivateMutation.mutate(scheduleId, {
      onSuccess: () => {
        toast.success({ title: t("schedule.deactivateSuccess") });
      },
      onError: (error) => {
        const message = apiMessage(error);
        toast.error({ title: message.title, message: message.message });
      },
    });
  };

  const schedule = scheduleQuery.data ?? [];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t("schedule.title")} subtitle={t("schedule.subtitle")} />

      <Card>
        <CardHeader title={t("schedule.selectDoctor")} />
        <CardBody>
          <Field label={t("schedule.selectDoctor")} htmlFor="schedule-doctor">
            <Select
              id="schedule-doctor"
              value={doctorId}
              onChange={(event) => setDoctorId(event.target.value)}
            >
              <option value="">{t("appointment.chooseDoctorError")}</option>
              {doctors.map((doctor) => (
                <option key={doctor.id} value={doctor.id}>
                  {doctor.name}
                </option>
              ))}
            </Select>
          </Field>
        </CardBody>
      </Card>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card className="overflow-hidden">
          <CardHeader title={t("schedule.title")} />
          {scheduleQuery.isLoading ? <LoadingState /> : null}

          {scheduleQuery.isError ? <EmptyState title={t("common.error")} /> : null}

          {!scheduleQuery.isLoading && !scheduleQuery.isError && schedule.length === 0 ? (
            <EmptyState icon={<IconCalendar className="size-8 text-slate-300" />} title={t("schedule.noSchedule")} />
          ) : null}

          {!scheduleQuery.isLoading && !scheduleQuery.isError && schedule.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                    <th className="px-4 py-3 text-start font-semibold">{t("schedule.day")}</th>
                    <th className="px-4 py-3 text-start font-semibold">{t("schedule.startsAt")}</th>
                    <th className="px-4 py-3 text-start font-semibold">{t("schedule.endsAt")}</th>
                    <th className="px-4 py-3 text-start font-semibold">{t("schedule.slotMinutes")}</th>
                    <th className="px-4 py-3 text-end font-semibold">{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {schedule.map((entry) => {
                    const dayKey = dayOfWeekKey(entry.dayOfWeek);
                    return (
                      <tr key={entry.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                        <td className="px-4 py-3 text-slate-900">{dayKey ? t(dayKey) : "—"}</td>
                        <td className="px-4 py-3 text-slate-700">{formatTime(entry.startsAt, locale)}</td>
                        <td className="px-4 py-3 text-slate-700">{formatTime(entry.endsAt, locale)}</td>
                        <td className="px-4 py-3 text-slate-700">{entry.slotMinutes}</td>
                        <td className="px-4 py-3">
                          <div className="flex justify-end">
                            <Button
                              variant="secondary"
                              size="sm"
                              loading={deactivateMutation.isPending}
                              onClick={() => handleDeactivate(entry.id as string)}
                            >
                              {t("schedule.deactivate")}
                            </Button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ) : null}
        </Card>

        <Card>
          <CardHeader title={t("schedule.add")} />
          <CardBody className="flex flex-col gap-4">
            <p className="text-sm text-slate-500">{t("schedule.addHint")}</p>

            <Field label={t("schedule.day")} htmlFor="schedule-day">
              <Select id="schedule-day" value={day} onChange={(event) => setDay(event.target.value)}>
                {DAYS.map((value) => {
                  const dayKey = dayOfWeekKey(value);
                  return (
                    <option key={value} value={value}>
                      {dayKey ? t(dayKey) : value}
                    </option>
                  );
                })}
              </Select>
            </Field>

            <div className="grid grid-cols-2 gap-3">
              <Field label={t("schedule.startsAt")} htmlFor="schedule-starts">
                <Input
                  id="schedule-starts"
                  type="time"
                  value={startsAt}
                  onChange={(event) => setStartsAt(event.target.value)}
                />
              </Field>
              <Field label={t("schedule.endsAt")} htmlFor="schedule-ends">
                <Input
                  id="schedule-ends"
                  type="time"
                  value={endsAt}
                  onChange={(event) => setEndsAt(event.target.value)}
                />
              </Field>
            </div>

            <Field label={t("schedule.slotMinutes")} htmlFor="schedule-slot">
              <Input
                id="schedule-slot"
                type="number"
                min={5}
                step={5}
                value={slotMinutes}
                onChange={(event) => setSlotMinutes(event.target.value)}
              />
            </Field>

            {formError ? <p className="text-xs font-medium text-red-600">{formError}</p> : null}

            <div className="flex justify-end">
              <Button loading={addMutation.isPending} onClick={handleAdd}>
                {t("schedule.add")}
              </Button>
            </div>
          </CardBody>
        </Card>
      </div>
    </div>
  );
}

export default DoctorSchedulePage;