import { useState } from "react";
import { Link } from "react-router-dom";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { Pagination } from "../../components/ui/Pagination";
import { IconFileText } from "../../components/icons";
import { useOwnInvoices } from "../../features/invoices/hooks";
import { formatCurrency, formatDateTime, invoiceStatusKey, invoiceStatusTone } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";

const PAGE_SIZE = 10;

export function MyInvoicesPage() {
  const { t, locale } = useI18n();
  const [page, setPage] = useState(1);

  const invoicesQuery = useOwnInvoices({ page, pageSize: PAGE_SIZE });
  const invoices = invoicesQuery.data?.items ?? [];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("invoice.myTitle")}
        actions={
          <Link to="/me">
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      <Card className="overflow-hidden">
        {invoicesQuery.isLoading ? <LoadingState /> : null}

        {invoicesQuery.isError ? <EmptyState title={t("common.error")} /> : null}

        {!invoicesQuery.isLoading && !invoicesQuery.isError && invoices.length === 0 ? (
          <EmptyState icon={<IconFileText className="size-8 text-slate-300" />} title={t("invoice.noInvoices")} />
        ) : null}

        {!invoicesQuery.isLoading && !invoicesQuery.isError && invoices.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-4 py-3 text-start font-semibold">{t("invoice.number")}</th>
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

        {invoicesQuery.data && (invoicesQuery.data.totalCount ?? 0) > 0 ? (
          <div className="border-t border-slate-200">
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={invoicesQuery.data.totalCount ?? 0} onChange={setPage} />
          </div>
        ) : null}
      </Card>
    </div>
  );
}

export default MyInvoicesPage;