import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createInvoice,
  getInvoice,
  getOwnInvoices,
  issueInvoice,
  recordPayment,
  searchInvoices,
  voidInvoice,
  type CreateInvoiceRequest,
  type InvoiceDetails,
  type InvoiceSearchParams,
  type InvoiceSummary,
  type InvoiceSummaryPagedResult,
  type PageParams,
  type RecordPaymentRequest,
  type VoidInvoiceRequest,
} from "../../lib/api/invoices";

export function useInvoicesSearch(params: InvoiceSearchParams) {
  return useQuery({
    queryKey: ["invoices", "search", params],
    queryFn: () => searchInvoices(params),
    placeholderData: keepPreviousData,
  });
}

export function useOwnInvoices(params: PageParams) {
  return useQuery({
    queryKey: ["invoices", "me", params],
    queryFn: () => getOwnInvoices(params),
    placeholderData: keepPreviousData,
  });
}

export function useInvoice(id: string | undefined) {
  return useQuery({
    queryKey: ["invoices", "detail", id],
    queryFn: () => getInvoice(id as string),
    enabled: Boolean(id),
  });
}

function invalidateInvoices(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ["invoices"] });
}

export function useCreateInvoice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateInvoiceRequest) => createInvoice(body),
    onSuccess: () => invalidateInvoices(queryClient),
  });
}

export function useIssueInvoice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => issueInvoice(id),
    onSuccess: () => invalidateInvoices(queryClient),
  });
}

export function useRecordInvoicePayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: RecordPaymentRequest }) => recordPayment(id, body),
    onSuccess: () => invalidateInvoices(queryClient),
  });
}

export function useVoidInvoice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: VoidInvoiceRequest }) => voidInvoice(id, body),
    onSuccess: () => invalidateInvoices(queryClient),
  });
}

export type { InvoiceDetails, InvoiceSummary, InvoiceSummaryPagedResult };