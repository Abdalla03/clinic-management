import type { components } from "./api/schema";
import type { TranslationKey } from "./i18n/translations";

type Gender = components["schemas"]["Gender"];
type BloodType = components["schemas"]["BloodType"];

const genderKeys: Record<number, TranslationKey> = {
  1: "gender.male",
  2: "gender.female",
};

const bloodTypeKeys: Record<number, TranslationKey> = {
  1: "bloodType.aPositive",
  2: "bloodType.aNegative",
  3: "bloodType.bPositive",
  4: "bloodType.bNegative",
  5: "bloodType.abPositive",
  6: "bloodType.abNegative",
  7: "bloodType.oPositive",
  8: "bloodType.oNegative",
};

/** Resolves a numeric enum value to a translation key (or undefined when unknown). */
export function genderLabelKey(gender: Gender | null | undefined): TranslationKey | undefined {
  return gender === null || gender === undefined ? undefined : genderKeys[gender];
}

export function bloodTypeLabelKey(bloodType: BloodType | null | undefined): TranslationKey | undefined {
  return bloodType === null || bloodType === undefined ? undefined : bloodTypeKeys[bloodType];
}

export function formatDate(
  value: string | null | undefined,
  locale: string,
): string {
  if (!value) {
    return "—";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "—";
  }
  return new Intl.DateTimeFormat(locale, { year: "numeric", month: "short", day: "numeric" }).format(date);
}

export function formatDateTime(
  value: string | null | undefined,
  locale: string,
): string {
  if (!value) {
    return "—";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "—";
  }
  return new Intl.DateTimeFormat(locale, {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
}

export function formatNumber(value: number | null | undefined, locale: string): string {
  if (value === null || value === undefined) {
    return "—";
  }
  return new Intl.NumberFormat(locale).format(value);
}

const appointmentStatusKeys: Record<number, TranslationKey> = {
  1: "appointment.status.booked",
  2: "appointment.status.confirmed",
  3: "appointment.status.done",
  4: "appointment.status.cancelled",
  5: "appointment.status.noShow",
};

const dayOfWeekKeys: Record<number, TranslationKey> = {
  0: "day.sunday",
  1: "day.monday",
  2: "day.tuesday",
  3: "day.wednesday",
  4: "day.thursday",
  5: "day.friday",
  6: "day.saturday",
};

const appointmentStatusTones: Record<number, string> = {
  1: "bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200",
  2: "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200",
  3: "bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-200",
  4: "bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200",
  5: "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200",
};

export function appointmentStatusKey(status: number | null | undefined): TranslationKey | undefined {
  return status === null || status === undefined ? undefined : appointmentStatusKeys[status];
}

export function appointmentStatusTone(status: number | null | undefined): string {
  return status === null || status === undefined
    ? "bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-200"
    : (appointmentStatusTones[status] ?? "bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-200");
}

export function dayOfWeekKey(day: number | null | undefined): TranslationKey | undefined {
  return day === null || day === undefined ? undefined : dayOfWeekKeys[day];
}

/** Formats a TimeOnly value ("HH:mm:ss") in the given locale. */
export function formatTime(value: string | null | undefined, locale: string): string {
  if (!value) {
    return "—";
  }
  const [hours, minutes] = value.split(":");
  const parsedHours = Number(hours);
  const parsedMinutes = Number(minutes);
  if (!Number.isFinite(parsedHours) || !Number.isFinite(parsedMinutes)) {
    return value;
  }
  const date = new Date(2000, 0, 1, parsedHours, parsedMinutes, 0, 0);
  return new Intl.DateTimeFormat(locale, { hour: "2-digit", minute: "2-digit" }).format(date);
}

/** Formats a UTC ISO timestamp as a short time-of-day in the given locale. */
export function formatTimeOnly(value: string | null | undefined, locale: string): string {
  if (!value) {
    return "—";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "—";
  }
  return new Intl.DateTimeFormat(locale, { hour: "2-digit", minute: "2-digit" }).format(date);
}

/** Formats a UTC ISO timestamp, keeping only the weekday and date. */
export function formatDay(value: string | null | undefined, locale: string): string {
  if (!value) {
    return "—";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "—";
  }
  return new Intl.DateTimeFormat(locale, { weekday: "short", month: "short", day: "numeric" }).format(date);
}

/** Converts an ISO timestamp into "YYYY-MM-DD" in the caller's local timezone. */
export function toLocalDateInputValue(value: string | null | undefined): string {
  if (!value) {
    return "";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

/** Returns the number of whole minutes between two ISO timestamps. */
export function durationMinutes(start: string | null | undefined, end: string | null | undefined): number {
  const from = start ? Date.parse(start) : NaN;
  const to = end ? Date.parse(end) : NaN;
  if (!Number.isFinite(from) || !Number.isFinite(to)) {
    return 30;
  }
  return Math.max(5, Math.round((to - from) / 60000));
}

const allergyOutcomeKeys: Record<number, TranslationKey> = {
  1: "prescription.allergy.notChecked",
  2: "prescription.allergy.noKnownAllergy",
  3: "prescription.allergy.matched",
};

const allergyOutcomeTones: Record<number, string> = {
  1: "bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-200",
  2: "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200",
  3: "bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200",
};

export function allergyOutcomeKey(outcome: number | null | undefined): TranslationKey | undefined {
  return outcome === null || outcome === undefined ? undefined : allergyOutcomeKeys[outcome];
}

export function allergyOutcomeTone(outcome: number | null | undefined): string {
  return outcome === null || outcome === undefined
    ? "bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-200"
    : (allergyOutcomeTones[outcome] ?? "bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-200");
}

/** Converts an ISO timestamp into an HTML datetime-local input value in the caller's timezone. */
export function toLocalDateTimeInputValue(value: string | null | undefined): string {
  if (!value) {
    return "";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  const hours = String(date.getHours()).padStart(2, "0");
  const minutes = String(date.getMinutes()).padStart(2, "0");
  return `${year}-${month}-${day}T${hours}:${minutes}`;
}

const invoiceStatusKeys: Record<number, TranslationKey> = {
  1: "invoice.status.draft",
  2: "invoice.status.issued",
  3: "invoice.status.partiallyPaid",
  4: "invoice.status.paid",
  5: "invoice.status.void",
};

const invoiceStatusTones: Record<number, string> = {
  1: "bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-200",
  2: "bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200",
  3: "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200",
  4: "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200",
  5: "bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200",
};

const paymentMethodKeys: Record<number, TranslationKey> = {
  1: "paymentMethod.cash",
  2: "paymentMethod.card",
  3: "paymentMethod.bankTransfer",
  4: "paymentMethod.insurance",
};

export function invoiceStatusKey(status: number | null | undefined): TranslationKey | undefined {
  return status === null || status === undefined ? undefined : invoiceStatusKeys[status];
}

export function invoiceStatusTone(status: number | null | undefined): string {
  return status === null || status === undefined
    ? "bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-200"
    : (invoiceStatusTones[status] ?? "bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-200");
}

export function paymentMethodKey(method: number | null | undefined): TranslationKey | undefined {
  return method === null || method === undefined ? undefined : paymentMethodKeys[method];
}

export function formatCurrency(
  value: number | null | undefined,
  currency: string | null | undefined,
  locale: string,
): string {
  if (value === null || value === undefined) {
    return "—";
  }
  const code = (currency ?? "EGP").trim();
  try {
    return new Intl.NumberFormat(locale, { style: "currency", currency: code }).format(value);
  } catch {
    return `${value.toFixed(2)} ${code}`;
  }
}