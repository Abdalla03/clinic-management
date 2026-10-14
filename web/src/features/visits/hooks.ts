import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getOwnVisits,
  getPatientVisits,
  getVisit,
  recordVisit,
  searchVisits,
  updateVisit,
  type PageParams,
  type RecordVisitRequest,
  type UpdateVisitRequest,
  type VisitDetails,
  type VisitSearchParams,
  type VisitSummary,
  type VisitSummaryPagedResult,
} from "../../lib/api/visits";

export function useVisitsSearch(params: VisitSearchParams) {
  return useQuery({
    queryKey: ["visits", "search", params],
    queryFn: () => searchVisits(params),
    placeholderData: keepPreviousData,
  });
}

export function useOwnVisits(params: PageParams) {
  return useQuery({
    queryKey: ["visits", "me", params],
    queryFn: () => getOwnVisits(params),
    placeholderData: keepPreviousData,
  });
}

export function useVisit(id: string | undefined) {
  return useQuery({
    queryKey: ["visits", "detail", id],
    queryFn: () => getVisit(id as string),
    enabled: Boolean(id),
  });
}

export function usePatientVisits(patientId: string | undefined, params: PageParams) {
  return useQuery({
    queryKey: ["visits", "patient", patientId, params],
    queryFn: () => getPatientVisits(patientId as string, params),
    enabled: Boolean(patientId),
    placeholderData: keepPreviousData,
  });
}

export function useRecordVisit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RecordVisitRequest) => recordVisit(body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["visits"] });
    },
  });
}

export function useUpdateVisit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateVisitRequest }) => updateVisit(id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["visits"] });
    },
  });
}

export type { VisitDetails, VisitSummary, VisitSummaryPagedResult };