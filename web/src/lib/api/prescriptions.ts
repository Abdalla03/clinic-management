import { api, unwrap } from "./client";
import type { components } from "./schema";

export type PrescriptionSummary = components["schemas"]["PrescriptionSummary"];
export type PrescriptionDetails = components["schemas"]["PrescriptionDetails"];
export type PrescriptionSummaryPagedResult = components["schemas"]["PrescriptionSummaryPagedResult"];
export type IssuePrescriptionRequest = components["schemas"]["IssuePrescriptionRequest"];
export type PrescriptionItemRequest = components["schemas"]["PrescriptionItemRequest"];

export interface PrescriptionSearchParams {
  patientId?: string;
  doctorId?: string;
  visitId?: string;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

export interface PageParams {
  page?: number;
  pageSize?: number;
}

export function searchPrescriptions(
  params: PrescriptionSearchParams = {},
): Promise<PrescriptionSummaryPagedResult> {
  return api.GET("/api/prescriptions", { params: { query: params } }).then(unwrap);
}

export function getOwnPrescriptions(params: PageParams = {}): Promise<PrescriptionSummaryPagedResult> {
  return api.GET("/api/prescriptions/mine", { params: { query: params } }).then(unwrap);
}

export function getPrescription(id: string): Promise<PrescriptionDetails> {
  return api.GET("/api/prescriptions/{prescriptionId}", { params: { path: { prescriptionId: id } } }).then(unwrap);
}

export function issuePrescription(body: IssuePrescriptionRequest): Promise<PrescriptionDetails> {
  return api.POST("/api/prescriptions", { body }).then(unwrap);
}

export function getPatientPrescriptions(
  patientId: string,
  params: PageParams = {},
): Promise<PrescriptionSummaryPagedResult> {
  return api
    .GET("/api/patients/{patientId}/prescriptions", { params: { path: { patientId }, query: params } })
    .then(unwrap);
}