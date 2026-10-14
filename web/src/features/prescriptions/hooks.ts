import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getOwnPrescriptions,
  getPatientPrescriptions,
  getPrescription,
  issuePrescription,
  searchPrescriptions,
  type IssuePrescriptionRequest,
  type PageParams,
  type PrescriptionDetails,
  type PrescriptionSearchParams,
  type PrescriptionSummary,
  type PrescriptionSummaryPagedResult,
} from "../../lib/api/prescriptions";

export function usePrescriptionsSearch(params: PrescriptionSearchParams) {
  return useQuery({
    queryKey: ["prescriptions", "search", params],
    queryFn: () => searchPrescriptions(params),
    placeholderData: keepPreviousData,
  });
}

export function useOwnPrescriptions(params: PageParams) {
  return useQuery({
    queryKey: ["prescriptions", "me", params],
    queryFn: () => getOwnPrescriptions(params),
    placeholderData: keepPreviousData,
  });
}

export function usePrescription(id: string | undefined) {
  return useQuery({
    queryKey: ["prescriptions", "detail", id],
    queryFn: () => getPrescription(id as string),
    enabled: Boolean(id),
  });
}

export function usePatientPrescriptions(patientId: string | undefined, params: PageParams) {
  return useQuery({
    queryKey: ["prescriptions", "patient", patientId, params],
    queryFn: () => getPatientPrescriptions(patientId as string, params),
    enabled: Boolean(patientId),
    placeholderData: keepPreviousData,
  });
}

export function useIssuePrescription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: IssuePrescriptionRequest) => issuePrescription(body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["prescriptions"] });
    },
  });
}

export type { PrescriptionDetails, PrescriptionSummary, PrescriptionSummaryPagedResult };