import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { Input, Select } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { Pagination } from "../../components/ui/Pagination";
import { IconFileText, IconPlus } from "../../components/icons";
import { useInvoicesSearch } from "../../features/invoices/hooks";
import { useAuth } from "../../features/auth/useAuth";
import { formatCurrency, formatDateTime, invoiceStatusKey, invoiceStatusTone } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import { RoleSecretary } from "../../lib/roles";

const PAGE_SIZE = 10;

export function InvoicesListPage() {
  const { t, locale } = useI18n();
  const { hasRole } = useAuth();
  const canCreate = hasRole(RoleSecretary);

  const [statusFilter, setStatusFilter] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [page, setPage] = useState(1);

  const params = useMemo(
    () => ({
      status: statusFilter ? (Number(statusFilter) as 1 | 2 | 3 | 4 | 5) : undefined,
      fromUtc: fromDate ? `${fromDate}T00:00:00.000Z` : undefined,
      toUtc: toDate ? `${toDate}T23:59:59.999Z` : undefined,
      page,
      pageSize: PAGE_SIZE,
    }),
    [statusFilter, fromDate, toDate, page],
  );

  const { data, isLoading, isError, isFetching, refetch } = useInvoicesSearch(params);
  const invoices = data?.items ?? [];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("invoice.title")}
        subtitle={t("invoice.subtitle")}
        actions={
          canCreate ? (
            <Link to="/invoices/new">
              <Button>
                <IconPlus />
                {t("invoice.new")}
              </Button>
            </Link>
          ) : undefined
        }
      />

      <Card className="overflow-hidden">
        <div className="flex flex-col gap-3 border-b border-slate-200 p-4 lg:flex-row lg:items-center">
          <Select
            value={statusFilter}
            onChange={(event) => {
              setStatusFilter(event.target.value);
              setPage(1);
            }}
            aria-label={t("invoice.statusFilter")}
            className="lg:w-48"
          >
            <option value="">{t("invoice.statusFilter")}</option>
            <option value="1">{t("invoice.status.draft")}</option>
            <option value="2">{t("invoice.status.issued")}</option>
            <option value="3">{t("invoice.status.partiallyPaid")}</option>
            <option value="4">{t("invoice.status.paid")}</option>
            <option value="5">{t("invoice.status.void")}</option>
          </Select>
          <div className="flex flex-1 flex-wrap items-center gap-3">
            <div className="flex items-center gap-2">
              <label className="text-xs font-medium text-slate-500">{t("common.from")}</label>
              <Input
                type="date"
                value={fromDate}
                onChange={(event) => {
                  setFromDate(event.target.value);
                  setPage(1);
                }}
                aria-label={t("common.from")}
              />
            </div>
            <div className="flex items-center gap-2">
              <label className="text-xs font-medium text-slate-500">{t("common.to")}</label>
              <Input
                type="date"
                value={toDate}
                onChange={(event) => {
                  setToDate(event.target.value);
                  setPage(1);
                }}
                aria-label={t("common.to")}
              />
            </div>
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

        {!isLoading && !isError && invoices.length === 0 ? (
          <EmptyState icon={<IconFileText className="size-8 text-slate-300" />} title={t("invoice.noInvoices")} />
        ) : null}

        {!isLoading && !isError && invoices.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-4 py-3 text-start font-semibold">{t("invoice.number")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("invoice.patient")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("invoice.status")}</th>
                  <th className="px-4 py-3 text-end font-semibold">{t("invoice.total")}</th>
                  <th className="px-4 py-3 text-end font-semibold">{t("invoice.outstanding")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("invoice.issuedAt")}</th>
                  <th className="px-4 py-3 text-end font-semibold">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {invoices.map((invoice) => {
                  const statusKey = invoiceStatusKey(invoice.status);
                  return (
                    <tr key={invoice.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                      <td className="px-4 py-3">
                        <Link
                          to={`/invoices/${invoice.id}`}
                          className="font-mono text-xs font-medium text-slate-900 hover:text-primary-600"
                        >
                          {invoice.invoiceNumber ?? "—"}
                        </Link>
                      </td>
                      <td className="px-4 py-3 text-slate-600">{invoice.patientName ?? "—"}</td>
                      <td className="px-4 py-3">
                        {statusKey ? (
                          <span
                            className={`inline-block rounded-full px-2 py-0.5 text-[10px] font-semibold ${invoiceStatusTone(
                              invoice.status,
                            )}`}
                          >
                            {t(statusKey)}
                          </span>
                        ) : (
                          <span className="text-slate-400">—</span>
                        )}
                      </td>
                      <td className="px-4 py-3 text-end font-medium text-slate-900">
                        {formatCurrency(invoice.totalAmount, invoice.currency, locale)}
                      </td>
                      <td className="px-4 py-3 text-end text-slate-600">
                        {formatCurrency(invoice.outstandingAmount, invoice.currency, locale)}
                      </td>
                      <td className="px-4 py-3 text-slate-600">{formatDateTime(invoice.issuedAtUtc, locale)}</td>
                      <td className="px-4 py-3">
                        <div className="flex items-center justify-end gap-2">
                          <Link
                            to={`/invoices/${invoice.id}`}
                            className="text-sm font-medium text-primary-600 hover:underline"
                          >
                            {t("invoice.viewInvoice")}
                          </Link>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : null}

        {data && (data.totalCount ?? 0) > 0 ? (
          <div className="border-t border-slate-200">
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount ?? 0} onChange={setPage} />
          </div>
        ) : null}

        {isFetching && !isLoading ? (
          <p className="border-t border-slate-100 px-5 py-2 text-xs text-slate-400">{t("common.loading")}</p>
        ) : null}
      </Card>
    </div>
  );
}

export default InvoicesListPage;