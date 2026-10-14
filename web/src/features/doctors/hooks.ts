import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  addSchedule,
  deactivateSchedule,
  getAvailability,
  getSchedule,
  listDoctors,
  type AvailabilityParams,
  type AvailabilitySlot,
  type DoctorScheduleResponse,
  type DoctorSummary,
  type UpsertDoctorScheduleRequest,
} from "../../lib/api/doctors";

export function useDoctors() {
  return useQuery({
    queryKey: ["doctors", "list"],
    queryFn: listDoctors,
  });
}

export function useAvailability(
  doctorId: string | undefined,
  params: AvailabilityParams | undefined,
) {
  return useQuery({
    queryKey: ["doctors", "availability", doctorId, params],
    queryFn: () => getAvailability(doctorId as string, params as AvailabilityParams),
    enabled: Boolean(doctorId && params),
  });
}

export function useDoctorSchedule(doctorId: string | undefined) {
  return useQuery({
    queryKey: ["doctors", "schedule", doctorId],
    queryFn: () => getSchedule(doctorId as string),
    enabled: Boolean(doctorId),
  });
}

export function useAddSchedule(doctorId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpsertDoctorScheduleRequest) => addSchedule(doctorId as string, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["doctors", "schedule", doctorId] });
      void queryClient.invalidateQueries({ queryKey: ["doctors", "availability"] });
    },
  });
}

export function useDeactivateSchedule(doctorId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (scheduleId: string) => deactivateSchedule(doctorId as string, scheduleId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["doctors", "schedule", doctorId] });
      void queryClient.invalidateQueries({ queryKey: ["doctors", "availability"] });
    },
  });
}

export type { AvailabilitySlot, DoctorScheduleResponse, DoctorSummary, UpsertDoctorScheduleRequest };