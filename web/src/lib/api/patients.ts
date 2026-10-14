import { api, unwrap } from "./client";
import type { components } from "./schema";

export type PatientSummary = components["schemas"]["PatientSummary"];
export type PatientDetails = components["schemas"]["PatientDetails"];
export type PatientSummaryPagedResult = components["schemas"]["PatientSummaryPagedResult"];
export type MedicalHistoryEntryResponse = components["schemas"]["MedicalHistoryEntryResponse"];
export type MedicalHistoryEntryResponsePagedResult = components["schemas"]["MedicalHistoryEntryResponsePagedResult"];
export type CreatePatientRequest = components["schemas"]["CreatePatientRequest"];
export type UpdatePatientRequest = components["schemas"]["UpdatePatientRequest"];
export type AddMedicalHistoryRequest = components["schemas"]["AddMedicalHistoryRequest"];

export interface PatientSearchParams {
  query?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

export interface PageParams {
  page?: number;
  pageSize?: number;
}

export function searchPatients(params: PatientSearchParams = {}): Promise<PatientSummaryPagedResult> {
  return api
    .GET("/api/patients", { params: { query: params } })
    .then(unwrap);
}

export function getPatient(id: string): Promise<PatientDetails> {
  return api.GET("/api/patients/{patientId}", { params: { path: { patientId: id } } }).then(unwrap);
}

export function createPatient(body: CreatePatientRequest): Promise<PatientDetails> {
  return api.POST("/api/patients", { body }).then(unwrap);
}

export function updatePatient(id: string, body: UpdatePatientRequest): Promise<PatientDetails> {
  return api.PUT("/api/patients/{patientId}", { params: { path: { patientId: id } }, body }).then(unwrap);
}

export function getMedicalHistory(id: string, params: PageParams = {}): Promise<MedicalHistoryEntryResponsePagedResult> {
  return api
    .GET("/api/patients/{patientId}/medical-history", {
      params: { path: { patientId: id }, query: params },
    })
    .then(unwrap);
}

export function addMedicalHistory(id: string, body: AddMedicalHistoryRequest): Promise<MedicalHistoryEntryResponse> {
  return api
    .POST("/api/patients/{patientId}/medical-history", { params: { path: { patientId: id } }, body })
    .then(unwrap);
}

export function getOwnPatient(): Promise<PatientDetails> {
  return api.GET("/api/patients/me").then(unwrap);
}

export function getOwnMedicalHistory(params: PageParams = {}): Promise<MedicalHistoryEntryResponsePagedResult> {
  return api.GET("/api/patients/me/medical-history", { params: { query: params } }).then(unwrap);
}