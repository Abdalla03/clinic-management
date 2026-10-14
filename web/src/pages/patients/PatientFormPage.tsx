import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useMemo } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate, useParams } from "react-router-dom";
import { z } from "zod";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { Card, CardBody } from "../../components/ui/Card";
import { Field, Input, Select, Textarea } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { useCreatePatient, usePatient, useUpdatePatient } from "../../features/patients/hooks";
import { useApiErrorMessage } from "../../features/errors/useApiErrorMessage";
import type { CreatePatientRequest, UpdatePatientRequest } from "../../lib/api/patients";
import { useI18n } from "../../lib/i18n/useI18n";
import type { TranslationKey } from "../../lib/i18n/translations";
import { useToast } from "../../lib/toast/useToast";
import { ApiRequestError, problemFieldErrors } from "../../lib/problem";

const TODAY_ISO = new Date().toISOString().slice(0, 10);

const genderOptions = [
  { value: "1", labelKey: "gender.male" as TranslationKey },
  { value: "2", labelKey: "gender.female" as TranslationKey },
];

const bloodTypeOptions: Array<{ value: string; labelKey: TranslationKey }> = [
  { value: "1", labelKey: "bloodType.aPositive" },
  { value: "2", labelKey: "bloodType.aNegative" },
  { value: "3", labelKey: "bloodType.bPositive" },
  { value: "4", labelKey: "bloodType.bNegative" },
  { value: "5", labelKey: "bloodType.abPositive" },
  { value: "6", labelKey: "bloodType.abNegative" },
  { value: "7", labelKey: "bloodType.oPositive" },
  { value: "8", labelKey: "bloodType.oNegative" },
];

interface FormValues {
  medicalRecordNumber: string;
  firstName: string;
  lastName: string;
  nationalId?: string;
  phoneNumber?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  dateOfBirth?: string;
  gender?: string;
  bloodType?: string;
  address?: string;
  notes?: string;
}

function emptyStringToUndefined(value: string | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

function toCreateRequest(values: FormValues): CreatePatientRequest {
  return {
    medicalRecordNumber: emptyStringToUndefined(values.medicalRecordNumber),
    firstName: values.firstName,
    lastName: values.lastName,
    nationalId: emptyStringToUndefined(values.nationalId),
    phoneNumber: emptyStringToUndefined(values.phoneNumber),
    emergencyContactName: emptyStringToUndefined(values.emergencyContactName),
    emergencyContactPhone: emptyStringToUndefined(values.emergencyContactPhone),
    dateOfBirth: emptyStringToUndefined(values.dateOfBirth),
    gender: values.gender ? (Number(values.gender) as 1 | 2) : undefined,
    bloodType: values.bloodType ? (Number(values.bloodType) as 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8) : undefined,
    address: emptyStringToUndefined(values.address),
    notes: emptyStringToUndefined(values.notes),
  };
}

function toUpdateRequest(values: FormValues): UpdatePatientRequest {
  const request = toCreateRequest(values);
  const { medicalRecordNumber: _ignored, ...rest } = request;
  return rest;
}

export function PatientFormPage() {
  const { patientId } = useParams<{ patientId: string }>();
  const isEdit = Boolean(patientId);
  const { t, locale } = useI18n();
  const toast = useToast();
  const apiMessage = useApiErrorMessage();
  const navigate = useNavigate();

  const patientQuery = usePatient(isEdit ? patientId : undefined);
  const createMutation = useCreatePatient();
  const updateMutation = useUpdatePatient();

  const schema = useMemo(
    () =>
      z.object({
        medicalRecordNumber: z.string().trim().min(1, t("common.required")),
        firstName: z.string().trim().min(1, t("common.required")),
        lastName: z.string().trim().min(1, t("common.required")),
        nationalId: z.string().optional(),
        phoneNumber: z.string().optional(),
        emergencyContactName: z.string().optional(),
        emergencyContactPhone: z.string().optional(),
        dateOfBirth: z.string().optional(),
        gender: z.string().optional(),
        bloodType: z.string().optional(),
        address: z.string().optional(),
        notes: z.string().optional(),
      }),
    [t],
  );

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      medicalRecordNumber: "",
      firstName: "",
      lastName: "",
      nationalId: "",
      phoneNumber: "",
      emergencyContactName: "",
      emergencyContactPhone: "",
      dateOfBirth: "",
      gender: "",
      bloodType: "",
      address: "",
      notes: "",
    },
  });

  useEffect(() => {
    if (!isEdit || !patientQuery.data) {
      return;
    }
    const data = patientQuery.data;
    form.reset({
      medicalRecordNumber: data.medicalRecordNumber ?? "",
      firstName: data.firstName ?? "",
      lastName: data.lastName ?? "",
      nationalId: data.nationalId ?? "",
      phoneNumber: data.phoneNumber ?? "",
      emergencyContactName: data.emergencyContactName ?? "",
      emergencyContactPhone: data.emergencyContactPhone ?? "",
      dateOfBirth: data.dateOfBirth ?? "",
      gender: data.gender !== undefined && data.gender !== null ? String(data.gender) : "",
      bloodType: data.bloodType !== undefined && data.bloodType !== null ? String(data.bloodType) : "",
      address: data.address ?? "",
      notes: data.notes ?? "",
    });
  }, [patientQuery.data, isEdit, form]);

  const onSubmit = (values: FormValues) => {
    if (isEdit && patientId) {
      updateMutation.mutate(
        { id: patientId, body: toUpdateRequest(values) },
        {
          onSuccess: (patient) => {
            toast.success({
              title: t("patient.updatedSuccess", { name: patient.fullName ?? values.firstName }),
            });
            navigate("/patients", { replace: true });
          },
          onError: (error) => {
            const message = apiMessage(error);
            toast.error({ title: message.title, message: message.message });
            const fieldErrors =
              error instanceof ApiRequestError ? problemFieldErrors(error.problem) : undefined;
            if (fieldErrors) {
              for (const [field, message2] of Object.entries(fieldErrors)) {
                form.setError(field as keyof FormValues, { message: message2 });
              }
            }
          },
        },
      );
      return;
    }

    createMutation.mutate(toCreateRequest(values), {
      onSuccess: (patient) => {
        toast.success({
          title: t("patient.createdSuccess", { name: patient.fullName ?? values.firstName }),
        });
        navigate("/patients", { replace: true });
      },
      onError: (error) => {
        const message = apiMessage(error);
        toast.error({ title: message.title, message: message.message });
        const fieldErrors =
          error instanceof ApiRequestError ? problemFieldErrors(error.problem) : undefined;
        if (fieldErrors) {
          for (const [field, message2] of Object.entries(fieldErrors)) {
            form.setError(field as keyof FormValues, { message: message2 });
          }
        }
      },
    });
  };

  const busy = createMutation.isPending || updateMutation.isPending;

  if (isEdit && patientQuery.isLoading) {
    return <LoadingState />;
  }

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <PageHeader
        title={isEdit ? t("patient.editPatient") : t("patient.createPatient")}
        actions={
          <Link to="/patients">
            <Button variant="secondary">{t("common.back")}</Button>
          </Link>
        }
      />

      {isEdit && patientQuery.data ? (
        <div className="flex items-center gap-3">
          <Badge tone="green">{t("patient.medicalRecordNumber")}: {patientQuery.data.medicalRecordNumber}</Badge>
          {patientQuery.data.isActive ? (
            <Badge tone="green">{t("common.active")}</Badge>
          ) : (
            <Badge tone="red">{t("common.inactive")}</Badge>
          )}
        </div>
      ) : null}

      {isEdit && patientQuery.isError ? (
        <Card>
          <CardBody>
            <p className="text-sm text-red-600">{t("patient.patientNotFound")}</p>
          </CardBody>
        </Card>
      ) : null}

      <Card>
        <CardBody>
          <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-5">
            {isEdit ? (
              <>
                <Field label={t("patient.medicalRecordNumber")} htmlFor="mrn">
                  <Input id="mrn" value={patientQuery.data?.medicalRecordNumber ?? ""} disabled />
                </Field>
                <hr className="border-slate-100" />
              </>
            ) : (
              <Field label={t("patient.medicalRecordNumber")} htmlFor="mrn" required>
                <Input
                  id="mrn"
                  invalid={Boolean(form.formState.errors.medicalRecordNumber)}
                  {...form.register("medicalRecordNumber")}
                />
              </Field>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t("patient.firstName")} htmlFor="firstName" required>
                <Input id="firstName" invalid={Boolean(form.formState.errors.firstName)} {...form.register("firstName")} />
              </Field>
              <Field label={t("patient.lastName")} htmlFor="lastName" required>
                <Input id="lastName" invalid={Boolean(form.formState.errors.lastName)} {...form.register("lastName")} />
              </Field>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t("patient.gender")} htmlFor="gender">
                <Select id="gender" {...form.register("gender")}>
                  <option value="">—</option>
                  {genderOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {t(option.labelKey)}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label={t("patient.bloodType")} htmlFor="bloodType">
                <Select id="bloodType" {...form.register("bloodType")}>
                  <option value="">—</option>
                  {bloodTypeOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {t(option.labelKey)}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t("patient.dateOfBirth")} htmlFor="dateOfBirth">
                <Input
                  id="dateOfBirth"
                  type="date"
                  max={TODAY_ISO}
                  {...form.register("dateOfBirth")}
                />
              </Field>
              <Field label={t("patient.nationalId")} htmlFor="nationalId">
                <Input id="nationalId" {...form.register("nationalId")} />
              </Field>
            </div>

            <Field label={t("patient.phoneNumber")} htmlFor="phoneNumber">
              <Input id="phoneNumber" type="tel" {...form.register("phoneNumber")} />
            </Field>

            <hr className="border-slate-100" />

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t("patient.emergencyContactName")} htmlFor="emergencyContactName">
                <Input id="emergencyContactName" {...form.register("emergencyContactName")} />
              </Field>
              <Field label={t("patient.emergencyContactPhone")} htmlFor="emergencyContactPhone">
                <Input id="emergencyContactPhone" type="tel" {...form.register("emergencyContactPhone")} />
              </Field>
            </div>

            <Field label={t("patient.address")} htmlFor="address">
              <Input id="address" {...form.register("address")} />
            </Field>

            <Field label={t("patient.notes")} htmlFor="notes">
              <Textarea id="notes" {...form.register("notes")} />
            </Field>

            <div className="flex justify-end gap-3" dir={locale === "ar" ? "rtl" : "ltr"}>
              <Link to="/patients">
                <Button variant="secondary" type="button" disabled={busy}>
                  {t("common.cancel")}
                </Button>
              </Link>
              <Button type="submit" loading={busy}>
                {busy ? t("common.loading") : t("common.save")}
              </Button>
            </div>
          </form>
        </CardBody>
      </Card>
    </div>
  );
}

export default PatientFormPage;