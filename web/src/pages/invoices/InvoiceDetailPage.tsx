import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Button } from "../../components/ui/Button";
import { Card, CardBody, CardHeader } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { Field, Input, Select, Textarea } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { useInvoice, useIssueInvoice, useRecordInvoicePayment, useVoidInvoice } from "../../features/invoices/hooks";
import { useAuth } from "../../features/auth/useAuth";
import { useApiErrorMessage } from "../../features/errors/useApiErrorMessage";
import { formatCurrency, formatDateTime, invoiceStatusKey, invoiceStatusTone, paymentMethodKey } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import { RoleSecretary } from "../../lib/roles";
import { useToast } from "../../lib/toast/useToast";

function PaymentMethodSelect({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const { t } = useI18n();
  return (
    <Select value={value} onChange={(event) => onChange(event.target.value)}>
      <option value="1">{t("paymentMethod.cash")}</option>
      <option value="2">{t("paymentMethod.card")}</option>
      <option value="3">{t("paymentMethod.bankTransfer")}</option>
      <option value="4">{t("paymentMethod.insurance")}</option>
    </Select>
  );
}

export function InvoiceDetailPage() {
  const { invoiceId } = useParams<{ invoiceId: string }>();
  const { t, locale } = useI18n();
  const toast = useToast();
  const apiMessage = useApiErrorMessage();
  const { hasRole } = useAuth();
  const isSecretary = hasRole(RoleSecretary);

  const invoiceQuery = useInvoice(invoiceId);
  const invoice = invoiceQuery.data;
  const issueMutation = useIssueInvoice();
  const paymentMutation = useRecordInvoicePayment();
  const voidMutation = useVoidInvoice();

  const [showPayment, setShowPayment] = useState(false);
  const [showVoid, setShowVoid] = useState(false);
  const [amount, setAmount] = useState("");
  const [method, setMethod] = useState("1");
  const [reference, setReference] = useState("");
  const [reason, setReason] = useState("");
  const [fieldErrors, setFieldErrors] = useState<{ amount?: string; method?: string; reason?: string }>({});

  const showPaymentForm = () => {
    setAmount(String(invoice?.outstandingAmount ?? 0));
    setMethod("1");
    setReference("");
    setFieldErrors({});
    setShowPayment(true);
    setShowVoid(false);
  };

  const showVoidForm = () => {
    setReason("");
    setFieldErrors({});
    setShowVoid(true);
    setShowPayment(false);
  };

  const submitPayment = () => {
    const outstanding = invoice?.outstandingAmount ?? 0;
    const parsedAmount = Number(amount);
    const errors: typeof fieldErrors = {};
    if (!Number.isFinite(parsedAmount) || parsedAmount <= 0) {
      errors.amount = t("invoice.amountRequiredError");
    } else if (parsedAmount > outstanding) {
      errors.amount = t("invoice.overPaymentError");
    }
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }
    paymentMutation.mutate(
      {
        id: invoiceId ?? "",
        body: { amount: parsedAmount, method: Number(method) as 1 | 2 | 3 | 4, reference: reference.trim() || undefined },
      },
      {
        onSuccess: () => {
          toast.success({ title: t("invoice.paymentSuccess") });
          setShowPayment(false);
        },
        onError: (error) => {
          const message = apiMessage(error);
          toast.error({ title: message.title, message: message.message });
        },
      },
    );
  };

  const submitVoid = () => {
    const errors: typeof fieldErrors = {};
    if (!reason.trim()) {
      errors.reason = t("common.required");
    }
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }
    voidMutation.mutate(
      { id: invoiceId ?? "", body: { reason: reason.trim() } },
      {
        onSuccess: () => {
          toast.success({ title: t("invoice.voidSuccess") });
          setShowVoid(false);
        },
        onError: (error) => {
          const message = apiMessage(error);
          toast.error({ title: message.title, message: message.message });
        },
      },
    );
  };

  const submitIssue = () => {
    issueMutation.mutate(invoiceId ?? "", {
      onSuccess: () => {
        toast.success({ title: t("invoice.issuedSuccess") });
      },
      onError: (error) => {
        const message = apiMessage(error);
        toast.error({ title: message.title, message: message.message });
      },
    });
  };

  if (invoiceQuery.isLoading) {
    return <LoadingState />;
  }

  const statusKey = invoice ? invoiceStatusKey(invoice.status) : undefined;
  const status = invoice?.status ?? 0;

  return (
    <div className="flex flex-col gap-6">
      <div className="no-print">
        <PageHeader
          title={t("invoice.details")}
          actions={
            <div className="flex flex-wrap gap-2">
              {invoice ? (
                <Button variant="secondary" onClick={() => window.print()}>
                  {t("invoice.print")}
                </Button>
              ) : null}
              <Link to="/invoices">
                <Button variant="secondary">{t("common.back")}</Button>
              </Link>
            </div>
          }
        />
      </div>

      {invoiceQuery.isError ? <EmptyState title={t("common.error")} /> : null}

      {!invoiceQuery.isLoading && !invoiceQuery.isError && !invoice ? (
        <EmptyState title={t("common.noRecords")} />
      ) : null}

      {invoice ? (
        <>
          <Card>
            <CardHeader
              title={invoice.invoiceNumber ?? "—"}
              subtitle={invoice.patientName ?? "—"}
              actions={
                statusKey ? (
                  <span
                    className={`inline-block rounded-full px-2 py-0.5 text-[10px] font-semibold ${invoiceStatusTone(
                      invoice.status,
                    )}`}
                  >
                    {t(statusKey)}
                  </span>
                ) : null
              }
            />
            <CardBody>
              <dl className="grid gap-4 text-sm sm:grid-cols-2">
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("invoice.patient")}</dt>
                  <dd className="mt-0.5 text-slate-900">{invoice.patientName ?? "—"}</dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("invoice.visit")}</dt>
                  <dd className="mt-0.5 text-slate-900">
                    {invoice.visitId ? (
                      <Link to={`/visits/${invoice.visitId}`} className="text-primary-600 hover:underline">
                        {t("prescription.viewVisit")}
                      </Link>
                    ) : (
                      "—"
                    )}
                  </dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("invoice.currency")}</dt>
                  <dd className="mt-0.5 text-slate-900">{invoice.currency ?? "—"}</dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("invoice.createdAt")}</dt>
                  <dd className="mt-0.5 text-slate-900">{formatDateTime(invoice.createdAtUtc, locale)}</dd>
                </div>
                <div>
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("invoice.issuedAt")}</dt>
                  <dd className="mt-0.5 text-slate-900">{formatDateTime(invoice.issuedAtUtc, locale)}</dd>
                </div>
                {invoice.status === 5 ? (
                  <>
                    <div>
                      <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("invoice.voidedAt")}</dt>
                      <dd className="mt-0.5 text-slate-900">{formatDateTime(invoice.voidedAtUtc, locale)}</dd>
                    </div>
                    <div>
                      <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("invoice.voidReason")}</dt>
                      <dd className="mt-0.5 text-slate-900">{invoice.voidReason ?? "—"}</dd>
                    </div>
                  </>
                ) : null}
                <div className="sm:col-span-2">
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t("invoice.total")}</dt>
                  <dd className="mt-0.5 text-lg font-bold text-slate-900">
                    {formatCurrency(invoice.totalAmount, invoice.currency, locale)}
                  </dd>
                </div>
              </dl>
            </CardBody>
          </Card>

          <Card className="overflow-hidden">
            <CardHeader title={t("invoice.lines")} />
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                    <th className="px-4 py-3 text-start font-semibold">{t("invoice.line.description")}</th>
                    <th className="px-4 py-3 text-end font-semibold">{t("invoice.line.unitPrice")}</th>
                    <th className="px-4 py-3 text-end font-semibold">{t("invoice.line.quantity")}</th>
                    <th className="px-4 py-3 text-end font-semibold">{t("invoice.line.total")}</th>
                  </tr>
                </thead>
                <tbody>
                  {(invoice.lines ?? []).map((line) => (
                    <tr key={line.id} className="border-b border-slate-100 last:border-0">
                      <td className="px-4 py-3 text-slate-900">{line.description}</td>
                      <td className="px-4 py-3 text-end text-slate-600">
                        {formatCurrency(line.unitPrice, invoice.currency, locale)}
                      </td>
                      <td className="px-4 py-3 text-end text-slate-600">{line.quantity}</td>
                      <td className="px-4 py-3 text-end font-medium text-slate-900">
                        {formatCurrency(line.lineTotal, invoice.currency, locale)}
                      </td>
                    </tr>
                  ))}
                  {(invoice.lines ?? []).length > 0 ? (
                    <tr className="bg-slate-50">
                      <td colSpan={3} className="px-4 py-3 text-end text-sm font-semibold text-slate-700">
                        {t("invoice.total")}: {formatCurrency(invoice.totalAmount, invoice.currency, locale)}
                      </td>
                      <td className="px-4 py-3 text-end font-bold text-slate-900">
                        {formatCurrency(invoice.totalAmount, invoice.currency, locale)}
                      </td>
                    </tr>
                  ) : (
                    <tr>
                      <td colSpan={4} className="px-4 py-8 text-center text-sm text-slate-400">
                        {t("common.noRecords")}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </Card>

          <Card className="overflow-hidden">
            <CardHeader title={t("invoice.payments")} />
            <div className="overflow-x-auto">
              {(invoice.payments ?? []).length === 0 ? (
                <EmptyState title={t("invoice.pending")} />
              ) : (
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                      <th className="px-4 py-3 text-start font-semibold">{t("invoice.paymentAmount")}</th>
                      <th className="px-4 py-3 text-start font-semibold">{t("invoice.paymentMethod")}</th>
                      <th className="px-4 py-3 text-start font-semibold">{t("payment.paidAt")}</th>
                      <th className="px-4 py-3 text-start font-semibold">{t("payment.reference")}</th>
                      <th className="px-4 py-3 text-start font-semibold">{t("payment.receivedBy")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(invoice.payments ?? []).map((payment) => {
                      const methodKey = paymentMethodKey(payment.method);
                      return (
                        <tr key={payment.id} className="border-b border-slate-100 last:border-0">
                          <td className="px-4 py-3 font-medium text-slate-900">
                            {formatCurrency(payment.amount, invoice.currency, locale)}
                          </td>
                          <td className="px-4 py-3 text-slate-600">{methodKey ? t(methodKey) : "—"}</td>
                          <td className="px-4 py-3 text-slate-600">{formatDateTime(payment.paidAtUtc, locale)}</td>
                          <td className="px-4 py-3 text-slate-600">{payment.reference ?? "—"}</td>
                          <td className="px-4 py-3 font-mono text-xs text-slate-500">{payment.receivedByUserId ?? "—"}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              )}
            </div>
          </Card>

          {isSecretary && status !== 5 && status !== 4 ? (
            <Card>
              <CardBody className="flex flex-col gap-4">
                <div className="flex flex-wrap items-end gap-3">
                  {status === 1 ? (
                    <Button loading={issueMutation.isPending} onClick={submitIssue}>
                      {t("invoice.issue")}
                    </Button>
                  ) : null}
                  {status === 2 || status === 3 ? (
                    <Button variant="secondary" onClick={showPaymentForm}>
                      {t("invoice.recordingPayment")}
                    </Button>
                  ) : null}
                  <Button variant="danger" onClick={showVoidForm}>
                    {t("invoice.voiding")}
                  </Button>
                </div>

                {showPayment ? (
                  <div className="flex flex-col gap-4 rounded-xl border border-slate-200 p-4">
                    <div className="grid gap-4 sm:grid-cols-3">
                      <Field
                        label={t("invoice.paymentAmount")}
                        htmlFor="payment-amount"
                        hint={t("invoice.paymentAmountHint")}
                        required
                      >
                        <Input
                          id="payment-amount"
                          type="number"
                          min={0}
                          step="0.01"
                          value={amount}
                          onChange={(event) => {
                            setAmount(event.target.value);
                            setFieldErrors((current) => ({ ...current, amount: undefined }));
                          }}
                          invalid={Boolean(fieldErrors.amount)}
                        />
                      </Field>
                      <Field label={t("invoice.paymentMethod")} htmlFor="payment-method" required>
                        <PaymentMethodSelect
                          value={method}
                          onChange={(value) => {
                            setMethod(value);
                            setFieldErrors((current) => ({ ...current, method: undefined }));
                          }}
                        />
                      </Field>
                      <Field label={t("invoice.paymentReference")} htmlFor="payment-reference" hint={t("invoice.paymentReferenceHint")}>
                        <Input
                          id="payment-reference"
                          value={reference}
                          maxLength={128}
                          onChange={(event) => setReference(event.target.value)}
                        />
                      </Field>
                    </div>
                    {fieldErrors.amount ? <p className="text-xs font-medium text-red-600">{fieldErrors.amount}</p> : null}
                    <div className="flex justify-end gap-2">
                      <Button variant="secondary" size="sm" onClick={() => setShowPayment(false)}>
                        {t("common.cancel")}
                      </Button>
                      <Button size="sm" loading={paymentMutation.isPending} onClick={submitPayment}>
                        {t("invoice.recordingPayment")}
                      </Button>
                    </div>
                  </div>
                ) : null}

                {showVoid ? (
                  <div className="flex flex-col gap-4 rounded-xl border border-red-200 bg-red-50/40 p-4">
                    <Field label={t("invoice.voidReason")} htmlFor="void-reason" required>
                      <Textarea
                        id="void-reason"
                        value={reason}
                        maxLength={512}
                        onChange={(event) => {
                          setReason(event.target.value);
                          setFieldErrors((current) => ({ ...current, reason: undefined }));
                        }}
                        invalid={Boolean(fieldErrors.reason)}
                        placeholder={t("invoice.voidReason")}
                      />
                    </Field>
                    {fieldErrors.reason ? <p className="text-xs font-medium text-red-600">{fieldErrors.reason}</p> : null}
                    <div className="flex justify-end gap-2">
                      <Button variant="secondary" size="sm" onClick={() => setShowVoid(false)}>
                        {t("common.cancel")}
                      </Button>
                      <Button variant="danger" size="sm" loading={voidMutation.isPending} onClick={submitVoid}>
                        {t("invoice.voiding")}
                      </Button>
                    </div>
                  </div>
                ) : null}
              </CardBody>
            </Card>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

export default InvoiceDetailPage;