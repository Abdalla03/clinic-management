import { api, unwrap } from "./client";
import type { components } from "./schema";

export type InvoiceSummary = components["schemas"]["InvoiceSummary"];
export type InvoiceDetails = components["schemas"]["InvoiceDetails"];
export type InvoiceSummaryPagedResult = components["schemas"]["InvoiceSummaryPagedResult"];
export type CreateInvoiceRequest = components["schemas"]["CreateInvoiceRequest"];
export type InvoiceLineRequest = components["schemas"]["InvoiceLineRequest"];
export type RecordPaymentRequest = components["schemas"]["RecordPaymentRequest"];
export type VoidInvoiceRequest = components["schemas"]["VoidInvoiceRequest"];
export type InvoiceStatus = components["schemas"]["InvoiceStatus"];
export type PaymentMethod = components["schemas"]["PaymentMethod"];

export interface InvoiceSearchParams {
  patientId?: string;
  visitId?: string;
  status?: InvoiceStatus;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

export interface PageParams {
  page?: number;
  pageSize?: number;
}

export function searchInvoices(params: InvoiceSearchParams = {}): Promise<InvoiceSummaryPagedResult> {
  return api.GET("/api/invoices", { params: { query: params } }).then(unwrap);
}

export function getOwnInvoices(params: PageParams = {}): Promise<InvoiceSummaryPagedResult> {
  return api.GET("/api/invoices/mine", { params: { query: params } }).then(unwrap);
}

export function getInvoice(id: string): Promise<InvoiceDetails> {
  return api.GET("/api/invoices/{invoiceId}", { params: { path: { invoiceId: id } } }).then(unwrap);
}

export function createInvoice(body: CreateInvoiceRequest): Promise<InvoiceDetails> {
  return api.POST("/api/invoices", { body }).then(unwrap);
}

export function issueInvoice(id: string): Promise<InvoiceDetails> {
  return api.POST("/api/invoices/{invoiceId}/issue", { params: { path: { invoiceId: id } } }).then(unwrap);
}

export function recordPayment(id: string, body: RecordPaymentRequest): Promise<InvoiceDetails> {
  return api.POST("/api/invoices/{invoiceId}/payments", { params: { path: { invoiceId: id } }, body }).then(unwrap);
}

export function voidInvoice(id: string, body: VoidInvoiceRequest): Promise<InvoiceDetails> {
  return api.POST("/api/invoices/{invoiceId}/void", { params: { path: { invoiceId: id } }, body }).then(unwrap);
}