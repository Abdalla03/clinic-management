import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { Select } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { IconCalendar, IconChevronLeft, IconChevronRight, IconPlus } from "../../components/icons";
import { useAppointmentsSearch } from "../../features/appointments/hooks";
import { useDoctors } from "../../features/doctors/hooks";
import { appointmentStatusKey, appointmentStatusTone, formatTimeOnly } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import { RoleSecretary } from "../../lib/roles";
import { useAuth } from "../../features/auth/useAuth";

const DAY_MS = 86_400_000;
const WEEK_MS = 7 * DAY_MS;
const TODAY_STRING = new Date().toDateString();

function startOfWeek(date: Date): Date {
  const d = new Date(date);
  const diff = (d.getDay() + 6) % 7;
  d.setDate(d.getDate() - diff);
  d.setHours(0, 0, 0, 0);
  return d;
}

export function AppointmentsListPage() {
  const { t, locale } = useI18n();
  const { hasRole } = useAuth();
  const canCreate = hasRole(RoleSecretary);

  const [weekStart, setWeekStart] = useState(() => startOfWeek(new Date()));
  const [doctorId, setDoctorId] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");

  const doctorsQuery = useDoctors();

  const fromUtc = weekStart.toISOString();
  const toUtc = new Date(weekStart.getTime() + WEEK_MS).toISOString();

  const params = useMemo(
    () => ({
      doctorId: doctorId || undefined,
      status: statusFilter === "all" ? undefined : (Number(statusFilter) as 1 | 2 | 3 | 4 | 5),
      fromUtc,
      toUtc,
      pageSize: 200,
    }),
    [doctorId, statusFilter, fromUtc, toUtc],
  );

  const { data, isLoading, isError, refetch } = useAppointmentsSearch(params);
  const appointments = data?.items ?? [];

  const days = useMemo(() => {
    return Array.from({ length: 7 }, (_, index) =>
      new Date(weekStart.getTime() + index * DAY_MS),
    );
  }, [weekStart]);

  const grouped = useMemo(() => {
    const weekAppointments = data?.items ?? [];
    const map = new Map<string, typeof weekAppointments>();
    for (const day of days) {
      map.set(day.toDateString(), []);
    }
    for (const appointment of weekAppointments) {
      const start = new Date(appointment.startsAtUtc as string);
      const key = start.toDateString();
      const list = map.get(key);
      if (list) {
        list.push(appointment);
      }
    }
    for (const list of map.values()) {
      list.sort((a, b) => (a.startsAtUtc ?? "").localeCompare(b.startsAtUtc ?? ""));
    }
    return map;
  }, [days, data]);

  const navigateWeek = (direction: number) => {
    setWeekStart((current) => new Date(current.getTime() + direction * WEEK_MS));
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("appointment.title")}
        subtitle={t("appointment.subtitle")}
        actions={
          canCreate ? (
            <Link to="/appointments/new">
              <Button>
                <IconPlus />
                {t("appointment.new")}
              </Button>
            </Link>
          ) : undefined
        }
      />

      <Card className="overflow-hidden">
        <div className="flex flex-wrap items-center gap-3 border-b border-slate-200 p-4">
          <div className="flex items-center gap-1.5">
            <Button variant="secondary" size="sm" onClick={() => navigateWeek(-1)} aria-label={t("appointment.prevWeek")}>
              <IconChevronLeft />
            </Button>
            <Button variant="secondary" size="sm" onClick={() => navigateWeek(1)} aria-label={t("appointment.nextWeek")}>
              <IconChevronRight />
            </Button>
            <Button variant="ghost" size="sm" onClick={() => setWeekStart(startOfWeek(new Date()))}>
              {t("appointment.goToToday")}
            </Button>
          </div>
          <span className="text-sm font-semibold text-slate-700">
            {new Intl.DateTimeFormat(locale, { month: "short", day: "numeric" }).format(days[0])} –{" "}
            {new Intl.DateTimeFormat(locale, { month: "short", day: "numeric" }).format(days[6])}
          </span>
          <div className="ms-auto flex flex-wrap items-center gap-3">
            <Select
              value={doctorId}
              onChange={(event) => {
                setDoctorId(event.target.value);
              }}
              aria-label={t("appointment.selectDoctor")}
              className="sm:w-56"
            >
              <option value="">{t("appointment.doctor")} — {t("common.all")}</option>
              {(doctorsQuery.data ?? []).map((doctor) => (
                <option key={doctor.id} value={doctor.id}>
                  {doctor.name}
                </option>
              ))}
            </Select>
            <Select
              value={statusFilter}
              onChange={(event) => {
                setStatusFilter(event.target.value);
              }}
              aria-label={t("appointment.status")}
              className="sm:w-44"
            >
              <option value="all">{t("common.status")} — {t("common.all")}</option>
              <option value="1">{t("appointment.status.booked")}</option>
              <option value="2">{t("appointment.status.confirmed")}</option>
              <option value="3">{t("appointment.status.done")}</option>
              <option value="4">{t("appointment.status.cancelled")}</option>
              <option value="5">{t("appointment.status.noShow")}</option>
            </Select>
          </div>
        </div>

        {isLoading ? <LoadingState /> : null}

        {isError ? (
          <EmptyState
            title={t("common.error")}
            hint={
              <Button variant="secondary" size="sm" onClick={() => void refetch()}>
                {t("common.retry")}
              </Button>
            }
          />
        ) : null}

        {!isLoading && !isError && appointments.length === 0 ? (
          <EmptyState icon={<IconCalendar className="size-8 text-slate-300" />} title={t("appointment.noAppointments")} />
        ) : null}

        {!isLoading && !isError && appointments.length > 0 ? (
          <div className="grid grid-cols-1 gap-0 sm:grid-cols-2 lg:grid-cols-7">
            {days.map((day) => {
              const list = grouped.get(day.toDateString()) ?? [];
              const isToday = day.toDateString() === TODAY_STRING;
              return (
                <div
                  key={day.toDateString()}
                  className={`min-h-40 border-slate-100 p-2 sm:border-s ${
                    isToday ? "bg-primary-50/50" : ""
                  }`}
                >
                  <p className="mb-2 text-center text-xs font-semibold text-slate-500">
                    {new Intl.DateTimeFormat(locale, { weekday: "short", day: "numeric" }).format(day)}
                  </p>
                  <div className="flex flex-col gap-1.5">
                    {list.map((appointment) => {
                      const statusKey = appointmentStatusKey(appointment.status);
                      return (
                        <Link
                          key={appointment.id}
                          to={`/appointments/${appointment.id}`}
                          className="block rounded-lg border border-slate-200 bg-white p-2 shadow-sm transition-colors hover:border-primary-300 hover:bg-primary-50/50"
                        >
                          <p className="text-xs font-bold text-slate-800">
                            {formatTimeOnly(appointment.startsAtUtc, locale)}
                          </p>
                          <p className="truncate text-xs font-medium text-slate-700">{appointment.patientName}</p>
                          <p className="truncate text-xs text-slate-500">{appointment.doctorName}</p>
                          {statusKey ? (
                            <span
                              className={`mt-1.5 inline-block rounded-full px-2 py-0.5 text-[10px] font-semibold ${appointmentStatusTone(
                                appointment.status,
                              )}`}
                            >
                              {t(statusKey)}
                            </span>
                          ) : null}
                        </Link>
                      );
                    })}
                    {list.length === 0 ? <p className="text-xs text-slate-300">&nbsp;</p> : null}
                  </div>
                </div>
              );
            })}
          </div>
        ) : null}
      </Card>
    </div>
  );
}

export default AppointmentsListPage;