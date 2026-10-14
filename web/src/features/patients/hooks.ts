import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  addMedicalHistory,
  createPatient,
  getMedicalHistory,
  getOwnMedicalHistory,
  getOwnPatient,
  getPatient,
  searchPatients,
  updatePatient,
  type AddMedicalHistoryRequest,
  type CreatePatientRequest,
  type PageParams,
  type PatientSearchParams,
  type UpdatePatientRequest,
} from "../../lib/api/patients";

export function usePatientsSearch(params: PatientSearchParams) {
  return useQuery({
    queryKey: ["patients", "search", params],
    queryFn: () => searchPatients(params),
    placeholderData: keepPreviousData,
  });
}

export function usePatient(id: string | undefined) {
  return useQuery({
    queryKey: ["patients", "detail", id],
    queryFn: () => getPatient(id as string),
    enabled: Boolean(id),
  });
}

export function useCreatePatient() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreatePatientRequest) => createPatient(body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["patients"] });
    },
  });
}

export function useUpdatePatient() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdatePatientRequest }) => updatePatient(id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["patients"] });
    },
  });
}

export function useMedicalHistory(patientId: string | undefined, params: PageParams) {
  return useQuery({
    queryKey: ["patients", "medical-history", patientId, params],
    queryFn: () => getMedicalHistory(patientId as string, params),
    enabled: Boolean(patientId),
    placeholderData: keepPreviousData,
  });
}

export function useAddMedicalHistory(patientId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: AddMedicalHistoryRequest) => addMedicalHistory(patientId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["patients", "medical-history", patientId] });
    },
  });
}

export function useOwnPatient() {
  return useQuery({
    queryKey: ["patients", "me"],
    queryFn: getOwnPatient,
    retry: false,
  });
}

export function useOwnMedicalHistory(params: PageParams) {
  return useQuery({
    queryKey: ["patients", "me", "medical-history", params],
    queryFn: () => getOwnMedicalHistory(params),
    placeholderData: keepPreviousData,
  });
}