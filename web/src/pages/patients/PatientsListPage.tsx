import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../features/auth/useAuth";
import { usePatientsSearch } from "../../features/patients/hooks";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { Input, Select } from "../../components/ui/Field";
import { LoadingState } from "../../components/ui/Spinner";
import { PageHeader } from "../../components/ui/PageHeader";
import { Pagination } from "../../components/ui/Pagination";
import { IconPlus, IconUsers } from "../../components/icons";
import { bloodTypeLabelKey, formatDate, genderLabelKey } from "../../lib/format";
import { useI18n } from "../../lib/i18n/useI18n";
import { useDebouncedValue } from "../../lib/useDebouncedValue";
import { RoleSecretary } from "../../lib/roles";

const PAGE_SIZE = 10;
type StatusFilter = "all" | "active" | "inactive";

export function PatientsListPage() {
  const { t, locale } = useI18n();
  const { hasRole } = useAuth();
  const canCreate = hasRole(RoleSecretary);

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebouncedValue(search);

  const params = useMemo(
    () => ({
      query: debouncedSearch.trim() || undefined,
      isActive: statusFilter === "all" ? undefined : statusFilter === "active",
      page,
      pageSize: PAGE_SIZE,
    }),
    [debouncedSearch, statusFilter, page],
  );

  const { data, isLoading, isError, isFetching, refetch } = usePatientsSearch(params);
  const patients = data?.items ?? [];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t("patient.title")}
        subtitle={t("patient.subtitle")}
        actions={
          canCreate ? (
            <Link to="/patients/new">
              <Button>
                <IconPlus />
                {t("patient.newPatient")}
              </Button>
            </Link>
          ) : undefined
        }
      />

      <Card className="overflow-hidden">
        <div className="flex flex-col gap-3 border-b border-slate-200 p-4 sm:flex-row sm:items-center">
          <div className="flex-1">
            <Input
              type="search"
              placeholder={t("patient.searchPlaceholder")}
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
              aria-label={t("common.search")}
            />
          </div>
          <Select
            value={statusFilter}
            onChange={(event) => {
              setStatusFilter(event.target.value as StatusFilter);
              setPage(1);
            }}
            aria-label={t("common.status")}
            className="sm:w-48"
          >
            <option value="all">{t("patient.allStatuses")}</option>
            <option value="active">{t("patient.onlyActive")}</option>
            <option value="inactive">{t("patient.onlyInactive")}</option>
          </Select>
        </div>

        {isLoading ? <LoadingState /> : null}

        {isError ? (
          <EmptyState
            title={t("common.error")}
            hint={
              <Button variant="secondary" size="sm" onClick={() => void refetch()}>
                {t("common.retry")}
              </Button>
            }
          />
        ) : null}

        {!isLoading && !isError && patients.length === 0 ? (
          <EmptyState icon={<IconUsers className="size-8 text-slate-300" />} title={t("common.noRecords")} />
        ) : null}

        {!isLoading && !isError && patients.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-start text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-4 py-3 text-start font-semibold">{t("patient.medicalRecordNumber")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("patient.fullName")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("patient.gender")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("patient.bloodType")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("patient.dateOfBirth")}</th>
                  <th className="px-4 py-3 text-start font-semibold">{t("common.status")}</th>
                  <th className="px-4 py-3 text-end font-semibold">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {patients.map((patient) => {
                  const genderKey = genderLabelKey(patient.gender);
                  const bloodKey = bloodTypeLabelKey(patient.bloodType);
                  return (
                    <tr key={patient.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                      <td className="px-4 py-3 font-mono text-xs text-slate-600">{patient.medicalRecordNumber ?? "—"}</td>
                      <td className="px-4 py-3">
                        <div className="font-medium text-slate-900">
                          {patient.firstName} {patient.lastName}
                        </div>
                      </td>
                      <td className="px-4 py-3 text-slate-600">{genderKey ? t(genderKey) : "—"}</td>
                      <td className="px-4 py-3 text-slate-600">{bloodKey ? t(bloodKey) : "—"}</td>
                      <td className="px-4 py-3 text-slate-600">{formatDate(patient.dateOfBirth, locale)}</td>
                      <td className="px-4 py-3">
                        {patient.isActive ? (
                          <Badge tone="green">{t("common.active")}</Badge>
                        ) : (
                          <Badge tone="red">{t("common.inactive")}</Badge>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center justify-end gap-2" dir={locale === "ar" ? "rtl" : "ltr"}>
                          <Link
                            to={`/patients/${patient.id}/edit`}
                            className="text-sm font-medium text-primary-600 hover:underline"
                          >
                            {t("common.edit")}
                          </Link>
                          <Link
                            to={`/patients/${patient.id}/medical-history`}
                            className="text-sm font-medium text-primary-600 hover:underline"
                          >
                            {t("patient.medicalHistoryTitle")}
                          </Link>
                          <Link
                            to={`/patients/${patient.id}/visits`}
                            className="text-sm font-medium text-primary-600 hover:underline"
                          >
                            {t("visit.title")}
                          </Link>
                          <Link
                            to={`/patients/${patient.id}/prescriptions`}
                            className="text-sm font-medium text-primary-600 hover:underline"
                          >
                            {t("prescription.title")}
                          </Link>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : null}

        {data && (data.totalCount ?? 0) > 0 ? (
          <div className="border-t border-slate-200">
            <Pagination
              page={page}
              pageSize={PAGE_SIZE}
              totalCount={data.totalCount ?? 0}
              onChange={setPage}
            />
          </div>
        ) : null}

        {isFetching && !isLoading ? (
          <p className="border-t border-slate-100 px-5 py-2 text-xs text-slate-400">{t("common.loading")}</p>
        ) : null}
      </Card>
    </div>
  );
}

export default PatientsListPage;