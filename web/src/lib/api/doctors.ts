import { api, unwrap } from "./client";
import type { components } from "./schema";

export type AvailabilitySlot = components["schemas"]["AvailabilitySlot"];
export type DoctorSummary = components["schemas"]["DoctorSummary"];
export type DoctorScheduleResponse = components["schemas"]["DoctorScheduleResponse"];
export type UpsertDoctorScheduleRequest = components["schemas"]["UpsertDoctorScheduleRequest"];

export interface AvailabilityParams {
  fromUtc: string;
  toUtc: string;
}

export function listDoctors(): Promise<DoctorSummary[]> {
  return api.GET("/api/doctors").then(unwrap);
}

export function getAvailability(
  doctorId: string,
  params: AvailabilityParams,
): Promise<AvailabilitySlot[]> {
  return api
    .GET("/api/doctors/{doctorId}/availability", { params: { path: { doctorId }, query: params } })
    .then(unwrap);
}

export function getSchedule(doctorId: string): Promise<DoctorScheduleResponse[]> {
  return api.GET("/api/doctors/{doctorId}/schedule", { params: { path: { doctorId } } }).then(unwrap);
}

export function addSchedule(
  doctorId: string,
  body: UpsertDoctorScheduleRequest,
): Promise<DoctorScheduleResponse> {
  return api
    .POST("/api/doctors/{doctorId}/schedule", { params: { path: { doctorId } }, body })
    .then(unwrap);
}

export function deactivateSchedule(doctorId: string, scheduleId: string): Promise<void> {
  return api
    .DELETE("/api/doctors/{doctorId}/schedule/{scheduleId}", {
      params: { path: { doctorId, scheduleId } },
    })
    .then(unwrap);
}