import { api, unwrap } from "./client";
import type { components } from "./schema";

export type VisitSummary = components["schemas"]["VisitSummary"];
export type VisitDetails = components["schemas"]["VisitDetails"];
export type VisitSummaryPagedResult = components["schemas"]["VisitSummaryPagedResult"];
export type RecordVisitRequest = components["schemas"]["RecordVisitRequest"];
export type UpdateVisitRequest = components["schemas"]["UpdateVisitRequest"];

export interface VisitSearchParams {
  patientId?: string;
  doctorId?: string;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

export interface PageParams {
  page?: number;
  pageSize?: number;
}

export function searchVisits(params: VisitSearchParams = {}): Promise<VisitSummaryPagedResult> {
  return api.GET("/api/visits", { params: { query: params } }).then(unwrap);
}

export function getOwnVisits(params: PageParams = {}): Promise<VisitSummaryPagedResult> {
  return api.GET("/api/visits/mine", { params: { query: params } }).then(unwrap);
}

export function getVisit(id: string): Promise<VisitDetails> {
  return api.GET("/api/visits/{visitId}", { params: { path: { visitId: id } } }).then(unwrap);
}

export function recordVisit(body: RecordVisitRequest): Promise<VisitDetails> {
  return api.POST("/api/visits", { body }).then(unwrap);
}

export function updateVisit(id: string, body: UpdateVisitRequest): Promise<VisitDetails> {
  return api.PUT("/api/visits/{visitId}", { params: { path: { visitId: id } }, body }).then(unwrap);
}

export function getPatientVisits(patientId: string, params: PageParams = {}): Promise<VisitSummaryPagedResult> {
  return api
    .GET("/api/patients/{patientId}/visits", { params: { path: { patientId }, query: params } })
    .then(unwrap);
}