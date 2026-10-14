import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  bookAppointment,
  bookMyAppointment,
  cancelAppointment,
  cancelMyAppointment,
  completeAppointment,
  confirmAppointment,
  getAppointment,
  getOwnAppointments,
  markAppointmentNoShow,
  rescheduleAppointment,
  searchAppointments,
  type AppointmentDetails,
  type AppointmentSearchParams,
  type AppointmentSummary,
  type AppointmentSummaryPagedResult,
  type BookAppointmentRequest,
  type BookMyAppointmentRequest,
  type CancelAppointmentRequest,
  type RescheduleAppointmentRequest,
} from "../../lib/api/appointments";

export function useAppointmentsSearch(params: AppointmentSearchParams) {
  return useQuery({
    queryKey: ["appointments", "search", params],
    queryFn: () => searchAppointments(params),
    placeholderData: keepPreviousData,
  });
}

export function useOwnAppointments(
  params: { page?: number; pageSize?: number },
  enabled = true,
) {
  return useQuery({
    queryKey: ["appointments", "me", params],
    queryFn: () => getOwnAppointments(params),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useAppointment(id: string | undefined) {
  return useQuery({
    queryKey: ["appointments", "detail", id],
    queryFn: () => getAppointment(id as string),
    enabled: Boolean(id),
  });
}

function useAppointmentMutation(
  mutationFn: (id: string) => Promise<unknown>,
): ReturnType<typeof useMutation<unknown, unknown, string>> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

export function useBookAppointment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: BookAppointmentRequest) => bookAppointment(body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

export function useBookMyAppointment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: BookMyAppointmentRequest) => bookMyAppointment(body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

export function useRescheduleAppointment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: RescheduleAppointmentRequest }) =>
      rescheduleAppointment(id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

export function useConfirmAppointment() {
  return useAppointmentMutation((id) => confirmAppointment(id));
}

export function useCompleteAppointment() {
  return useAppointmentMutation((id) => completeAppointment(id));
}

export function useMarkNoShow() {
  return useAppointmentMutation((id) => markAppointmentNoShow(id));
}

export function useCancelAppointment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body?: CancelAppointmentRequest }) =>
      cancelAppointment(id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

export function useCancelMyAppointment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body?: CancelAppointmentRequest }) =>
      cancelMyAppointment(id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["appointments"] });
    },
  });
}

export type { AppointmentDetails, AppointmentSummary, AppointmentSummaryPagedResult };