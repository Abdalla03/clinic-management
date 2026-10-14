import { api, unwrap } from "./client";
import type { components } from "./schema";

export type AppointmentSummary = components["schemas"]["AppointmentSummary"];
export type AppointmentDetails = components["schemas"]["AppointmentDetails"];
export type AppointmentSummaryPagedResult = components["schemas"]["AppointmentSummaryPagedResult"];
export type AppointmentStatus = components["schemas"]["AppointmentStatus"];
export type BookAppointmentRequest = components["schemas"]["BookAppointmentRequest"];
export type BookMyAppointmentRequest = components["schemas"]["BookMyAppointmentRequest"];
export type RescheduleAppointmentRequest = components["schemas"]["RescheduleAppointmentRequest"];
export type CancelAppointmentRequest = components["schemas"]["CancelAppointmentRequest"];

export interface AppointmentSearchParams {
  doctorId?: string;
  patientId?: string;
  status?: AppointmentStatus;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

export function searchAppointments(
  params: AppointmentSearchParams = {},
): Promise<AppointmentSummaryPagedResult> {
  return api.GET("/api/appointments", { params: { query: params } }).then(unwrap);
}

export function getOwnAppointments(
  params: { page?: number; pageSize?: number } = {},
): Promise<AppointmentSummaryPagedResult> {
  return api.GET("/api/appointments/me", { params: { query: params } }).then(unwrap);
}

export function getAppointment(id: string): Promise<AppointmentDetails> {
  return api.GET("/api/appointments/{appointmentId}", { params: { path: { appointmentId: id } } }).then(unwrap);
}

export function bookAppointment(body: BookAppointmentRequest): Promise<AppointmentDetails> {
  return api.POST("/api/appointments", { body }).then(unwrap);
}

/** Books an appointment for the signed-in patient's own record. */
export function bookMyAppointment(body: BookMyAppointmentRequest): Promise<AppointmentDetails> {
  return api.POST("/api/appointments/me", { body }).then(unwrap);
}

export function rescheduleAppointment(
  id: string,
  body: RescheduleAppointmentRequest,
): Promise<AppointmentDetails> {
  return api
    .PUT("/api/appointments/{appointmentId}", { params: { path: { appointmentId: id } }, body })
    .then(unwrap);
}

export function confirmAppointment(id: string): Promise<AppointmentDetails> {
  return api
    .POST("/api/appointments/{appointmentId}/confirm", { params: { path: { appointmentId: id } } })
    .then(unwrap);
}

export function completeAppointment(id: string): Promise<AppointmentDetails> {
  return api
    .POST("/api/appointments/{appointmentId}/complete", { params: { path: { appointmentId: id } } })
    .then(unwrap);
}

export function markAppointmentNoShow(id: string): Promise<AppointmentDetails> {
  return api
    .POST("/api/appointments/{appointmentId}/no-show", { params: { path: { appointmentId: id } } })
    .then(unwrap);
}

export function cancelAppointment(id: string, body: CancelAppointmentRequest = {}): Promise<AppointmentDetails> {
  return api
    .POST("/api/appointments/{appointmentId}/cancel", { params: { path: { appointmentId: id } }, body })
    .then(unwrap);
}

/** Cancels one of the signed-in patient's own appointments. */
export function cancelMyAppointment(
  id: string,
  body: CancelAppointmentRequest = {},
): Promise<AppointmentDetails> {
  return api
    .POST("/api/appointments/me/{appointmentId}/cancel", {
      params: { path: { appointmentId: id } },
      body,
    })
    .then(unwrap);
}