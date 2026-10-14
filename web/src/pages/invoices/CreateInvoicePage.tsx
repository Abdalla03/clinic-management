import { useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Button } from "../../components/ui/Button";
import { Card, CardBody, CardHeader } from "../../components/ui/Card";
import { Field, Input, Select } from "../../components/ui/Field";
import { PageHeader } from "../../components/ui/PageHeader";
import { IconPlus, IconTrash } from "../../components/icons";
import { useCreateInvoice } from "../../features/invoices/hooks";
import { usePatientsSearch } from "../../features/patients/hooks";
import { usePatientVisits } from "../../features/visits/hooks";
import { useApiErrorMessage } from "../../features/errors/useApiErrorMessage";
import type { InvoiceLineRequest } from "../../lib/api/invoices";
import { formatCurrency, formatDateTime } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import type { TranslationKey } from "../../lib/i18n/translations";
import { useDebouncedValue } from "../../lib/useDebouncedValue";
import { useToast } from "../../lib/toast/useToast";
import { cn } from "../../lib/cn";

interface LineRow {
  description: string;
  unitPrice: string;
  quantity: string;
}

const emptyLine: LineRow = { description: "", unitPrice: "", quantity: "1" };

const CURRENCY_PATTERN = /^[A-Za-z]{3}$/;

function lineTotal(row: LineRow): number {
  const price = Number(row.unitPrice);
  const quantity = Number(row.quantity);
  if (!Number.isFinite(price) || !Number.isFinite(quantity)) {
    return 0;
  }
  return price * quantity;
}

function validateLines(
  rows: LineRow[],
  errors: Array<Record<string, string> | undefined>,
  translate: (key: TranslationKey, params?: Record<string, string | number>) => string,
): boolean {
  let hasError = false;
  rows.forEach((row, index) => {
    const entry: Record<string, string> = {};
    if (!row.description.trim()) {
      entry.description = translate("invoice.descriptionRequired");
    }
    const price = Number(row.unitPrice);
    const quantityStr = row.quantity.trim();
    const quantity = quantityStr ? Number(quantityStr) : NaN;
    if (!Number.isFinite(price) || price < 0 || price > 1_000_000) {
      entry.unitPrice = translate("invoice.unitPriceRange");
    }
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > 1000) {
      entry.quantity = translate("invoice.quantityRange");
    }
    errors[index] = entry;
    if (Object.keys(entry).length > 0) {
      hasError = true;
    }
  });
  return hasError;
}

export function CreateInvoicePage() {
  const [searchParams] = useSearchParams();
  const preselectedPatientId = searchParams.get("patientId") ?? "";
  const { t, locale } = useI18n();
  const toast = useToast();
  const apiMessage = useApiErrorMessage();
  const navigate = useNavigate();

  const [patientId, setPatientId] = useState(preselectedPatientId);
  const [visitId, setVisitId] = useState("");
  const [currency, setCurrency] = useState("EGP");
  const [rows, setRows] = useState<LineRow[]>(() => [emptyLine]);
  const [rowErrors, setRowErrors] = useState<Array<Record<string, string> | undefined>>([undefined]);
  const [patientError, setPatientError] = useState("");
  const [currencyError, setCurrencyError] = useState("");
  const [patientQuery, setPatientQuery] = useState("");

  const debouncedPatientQuery = useDebouncedValue(patientQuery);
  const patientSearchQuery = usePatientsSearch({
    query: debouncedPatientQuery.trim() || undefined,
    pageSize: 6,
  });
  const patients = patientSearchQuery.data?.items ?? [];

  const visitsQuery = usePatientVisits(patientId || undefined, { page: 1, pageSize: 100 });
  const visits = visitsQuery.data?.items ?? [];

  const createMutation = useCreateInvoice();

  const totalAmount = useMemo(() => rows.reduce((sum, row) => sum + lineTotal(row), 0), [rows]);

  const updateRow = (index: number, patch: Partial<LineRow>) => {
    setRows((current) => current.map((row, i) => (i === index ? { ...row, ...patch } : row)));
    setRowErrors((current) => {
      const next = [...current];
      next[index] = undefined;
      return next;
    });
  };

  const addRow = () => {
    setRows((current) => [...current, emptyLine]);
    setRowErrors((current) => [...current, undefined]);
  };

  const removeRow = (index: number) => {
    setRows((current) => current.filter((_, i) => i !== index));
    setRowErrors((current) => current.filter((_, i) => i !== index));
  };

  const handleSubmit = () => {
    let hasError = false;
    if (!patientId) {
      setPatientError(t("invoice.choosePatientError"));
      hasError = true;
    } else {
      setPatientError("");
    }
    if (!CURRENCY_PATTERN.test(currency.trim())) {
      setCurrencyError(t("invoice.currencyInvalid"));
      hasError = true;
    } else {
      setCurrencyError("");
    }

    const errors: Array<Record<string, string> | undefined> = rows.map(() => undefined);
    const linesInvalid = validateLines(rows, errors, t);
    setRowErrors(errors);
    if (linesInvalid) {
      toast.error({ title: t("invoice.lineError") });
      return;
    }
    if (hasError) {
      return;
    }

    const lines: InvoiceLineRequest[] = rows.map((row) => ({
      description: row.description.trim(),
      unitPrice: Number(row.unitPrice),
      quantity: Math.trunc(Number(row.quantity)),
    }));

    createMutation.mutate(
      {
        patientId,
        lines,
        visitId: visitId || undefined,
        currency: currency.trim().toUpperCase(),
      },
      {
        onSuccess: (invoice) => {
          toast.success({ title: t("invoice.createdSuccess") });
          navigate(`/invoices/${invoice.id}`, { replace: true });
        },
        onError: (error) => {
          const message = apiMessage(error);
          toast.error({ title: message.title, message: message.message });
        },
      },
    );
  };

  const busy = createMutation.isPending;

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <PageHeader
        title={t("invoice.new")}
        actions={
          <Link to="/invoices">
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      <Card>
        <CardHeader title={t("invoice.new")} />
        <CardBody className="flex flex-col gap-5">
          <Field label={t("invoice.patient")} htmlFor="invoice-patient-search" required>
            <Input
              id="invoice-patient-search"
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
                      setVisitId("");
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

          {patientId ? (
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t("invoice.visit")} htmlFor="invoice-visit">
                <Select
                  id="invoice-visit"
                  value={visitId}
                  onChange={(event) => setVisitId(event.target.value)}
                >
                  <option value="">—</option>
                  {visits.map((visit) => (
                    <option key={visit.id} value={visit.id}>
                      {formatDateTime(visit.visitAtUtc, locale)}
                      {visit.diagnosis ? ` · ${visit.diagnosis}` : ""}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label={t("invoice.currency")} htmlFor="invoice-currency" required>
                <Input
                  id="invoice-currency"
                  value={currency}
                  maxLength={3}
                  onChange={(event) => {
                    setCurrency(event.target.value);
                    setCurrencyError("");
                  }}
                  invalid={Boolean(currencyError)}
                />
              </Field>
            </div>
          ) : null}
          {currencyError ? <p className="-mt-3 text-xs font-medium text-red-600">{currencyError}</p> : null}

          <hr className="border-slate-100" />

          <div className="flex items-center justify-between">
            <span className="text-sm font-semibold text-slate-900">{t("invoice.lines")}</span>
            <Button variant="secondary" size="sm" onClick={addRow} disabled={rows.length >= 100}>
              <IconPlus />
              {t("invoice.addLine")}
            </Button>
          </div>

          {rows.map((row, index) => {
            const errors = rowErrors[index] ?? {};
            return (
              <div key={index} className="flex flex-col gap-3 rounded-xl border border-slate-200 p-4">
                <div className="grid gap-3 sm:grid-cols-[1fr_7rem_6rem_7rem]">
                  <Field label={t("invoice.line.description")} htmlFor={`line-desc-${index}`} required>
                    <Input
                      id={`line-desc-${index}`}
                      value={row.description}
                      onChange={(event) => updateRow(index, { description: event.target.value })}
                      invalid={Boolean(errors.description)}
                    />
                  </Field>
                  <Field label={t("invoice.line.unitPrice")} htmlFor={`line-price-${index}`} required>
                    <Input
                      id={`line-price-${index}`}
                      type="number"
                      min={0}
                      step="0.01"
                      value={row.unitPrice}
                      onChange={(event) => updateRow(index, { unitPrice: event.target.value })}
                      invalid={Boolean(errors.unitPrice)}
                    />
                  </Field>
                  <Field label={t("invoice.line.quantity")} htmlFor={`line-qty-${index}`} required>
                    <Input
                      id={`line-qty-${index}`}
                      type="number"
                      min={1}
                      step={1}
                      value={row.quantity}
                      onChange={(event) => updateRow(index, { quantity: event.target.value })}
                      invalid={Boolean(errors.quantity)}
                    />
                  </Field>
                  <Field label={t("invoice.line.total")}>
                    <div className="flex h-9 items-center justify-end rounded-lg bg-slate-50 px-3 font-mono text-sm text-slate-700">
                      {formatCurrency(lineTotal(row), currency, locale)}
                    </div>
                  </Field>
                </div>
                {Object.keys(errors).length > 0 ? (
                  <div className="flex flex-col gap-1">
                    {Object.entries(errors).map(([key, message]) => (
                      <p key={key} className="text-xs font-medium text-red-600">
                        {message}
                      </p>
                    ))}
                  </div>
                ) : null}
                {rows.length > 1 ? (
                  <div className="flex justify-end">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => removeRow(index)}
                      aria-label={t("invoice.removeLine")}
                    >
                      <IconTrash />
                    </Button>
                  </div>
                ) : null}
              </div>
            );
          })}

          <div className="flex items-center justify-end gap-3 border-t border-slate-100 pt-4">
            <span className="text-sm font-medium text-slate-600">{t("invoice.total")}</span>
            <span className="font-mono text-lg font-bold text-slate-900">
              {formatCurrency(totalAmount, currency, locale)}
            </span>
          </div>

          <div className="flex justify-end gap-3">
            <Link to="/invoices">
              <Button variant="secondary" type="button" disabled={busy}>
                {t("common.cancel")}
              </Button>
            </Link>
            <Button loading={busy} onClick={handleSubmit}>
              {t("invoice.new")}
            </Button>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}

export default CreateInvoicePage;