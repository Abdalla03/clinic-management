import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createBrowserRouter, Outlet, RouterProvider } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { ProtectedRoute, RoleGuard } from "./components/guards";
import { AuthSessionListener } from "./features/auth/AuthSessionListener";
import { AuthProvider } from "./features/auth/AuthProvider";
import { I18nProvider } from "./lib/i18n/I18nProvider";
import { ThemeProvider } from "./lib/theme/ThemeProvider";
import { ToastProvider } from "./lib/toast/ToastProvider";
import { ClinicalStaffRoles, RoleDoctor, RolePatient, RoleSecretary } from "./lib/roles";
import { AppointmentBookingPage } from "./pages/appointments/AppointmentBookingPage";
import { AppointmentDetailPage } from "./pages/appointments/AppointmentDetailPage";
import { AppointmentsListPage } from "./pages/appointments/AppointmentsListPage";
import { MyAppointmentsPage } from "./pages/appointments/MyAppointmentsPage";
import { DoctorSchedulePage } from "./pages/doctors/DoctorSchedulePage";
import { ForbiddenPage } from "./pages/ForbiddenPage";
import { HomePage } from "./pages/HomePage";
import { LoginPage } from "./pages/LoginPage";
import { MfaSettingsPage } from "./pages/account/MfaSettingsPage";
import { MfaVerifyPage } from "./pages/MfaVerifyPage";
import { CreateInvoicePage } from "./pages/invoices/CreateInvoicePage";
import { InvoiceDetailPage } from "./pages/invoices/InvoiceDetailPage";
import { InvoicesListPage } from "./pages/invoices/InvoicesListPage";
import { MyInvoicesPage } from "./pages/invoices/MyInvoicesPage";
import { MyHealthPage } from "./pages/patients/MyHealthPage";
import { NotFoundPage } from "./pages/NotFoundPage";
import { PatientFormPage } from "./pages/patients/PatientFormPage";
import { PatientMedicalHistoryPage } from "./pages/patients/PatientMedicalHistoryPage";
import { PatientsListPage } from "./pages/patients/PatientsListPage";
import { IssuePrescriptionPage } from "./pages/prescriptions/IssuePrescriptionPage";
import { MyPrescriptionsPage } from "./pages/prescriptions/MyPrescriptionsPage";
import { PatientPrescriptionsPage } from "./pages/prescriptions/PatientPrescriptionsPage";
import { PrescriptionDetailPage } from "./pages/prescriptions/PrescriptionDetailPage";
import { PrescriptionsListPage } from "./pages/prescriptions/PrescriptionsListPage";
import { ReportsPage } from "./pages/reports/ReportsPage";
import { MyVisitsPage } from "./pages/visits/MyVisitsPage";
import { PatientVisitsPage } from "./pages/visits/PatientVisitsPage";
import { VisitDetailPage } from "./pages/visits/VisitDetailPage";
import { VisitFormPage } from "./pages/visits/VisitFormPage";
import { VisitsListPage } from "./pages/visits/VisitsListPage";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false },
  },
});

function Root() {
  return (
    <>
      <AuthSessionListener />
      <Outlet />
    </>
  );
}

const router = createBrowserRouter([
  {
    element: <Root />,
    children: [
      { path: "/login", element: <LoginPage /> },
      { path: "/mfa-verify", element: <MfaVerifyPage /> },
      {
        element: (
          <ProtectedRoute>
            <AppShell />
          </ProtectedRoute>
        ),
        children: [
          { index: true, element: <HomePage /> },
          { path: "account/security", element: <MfaSettingsPage /> },
          {
            path: "patients",
            element: (
              <RoleGuard roles={ClinicalStaffRoles}>
                <PatientsListPage />
              </RoleGuard>
            ),
          },
          {
            path: "patients/new",
            element: (
              <RoleGuard roles={ClinicalStaffRoles}>
                <PatientFormPage />
              </RoleGuard>
            ),
          },
          {
            path: "patients/:patientId/edit",
            element: (
              <RoleGuard roles={ClinicalStaffRoles}>
                <PatientFormPage />
              </RoleGuard>
            ),
          },
          {
            path: "patients/:patientId/medical-history",
            element: (
              <RoleGuard roles={ClinicalStaffRoles}>
                <PatientMedicalHistoryPage />
              </RoleGuard>
            ),
          },
          {
            path: "patients/:patientId/visits",
            element: (
              <RoleGuard roles={ClinicalStaffRoles}>
                <PatientVisitsPage />
              </RoleGuard>
            ),
          },
          {
            path: "patients/:patientId/prescriptions",
            element: (
              <RoleGuard roles={ClinicalStaffRoles}>
                <PatientPrescriptionsPage />
              </RoleGuard>
            ),
          },
          {
            path: "appointments",
            element: (
              <RoleGuard roles={ClinicalStaffRoles}>
                <AppointmentsListPage />
              </RoleGuard>
            ),
          },
          {
            path: "appointments/new",
            element: (
              <RoleGuard roles={[RoleDoctor, RoleSecretary, RolePatient]}>
                <AppointmentBookingPage />
              </RoleGuard>
            ),
          },
          {
            path: "appointments/me",
            element: (
              <RoleGuard roles={[RolePatient]}>
                <MyAppointmentsPage />
              </RoleGuard>
            ),
          },
          {
            path: "appointments/:appointmentId",
            element: (
              <RoleGuard roles={[RoleDoctor, RoleSecretary, RolePatient]}>
                <AppointmentDetailPage />
              </RoleGuard>
            ),
          },
          {
            path: "doctors/schedule",
            element: (
              <RoleGuard roles={ClinicalStaffRoles}>
                <DoctorSchedulePage />
              </RoleGuard>
            ),
          },
          {
            path: "visits",
            element: (
              <RoleGuard roles={ClinicalStaffRoles}>
                <VisitsListPage />
              </RoleGuard>
            ),
          },
          {
            path: "visits/me",
            element: (
              <RoleGuard roles={[RolePatient]}>
                <MyVisitsPage />
              </RoleGuard>
            ),
          },
          {
            path: "visits/new",
            element: (
              <RoleGuard roles={[RoleDoctor]}>
                <VisitFormPage />
              </RoleGuard>
            ),
          },
          {
            path: "visits/:visitId",
            element: (
              <RoleGuard roles={[RoleDoctor, RoleSecretary, RolePatient]}>
                <VisitDetailPage />
              </RoleGuard>
            ),
          },
          {
            path: "visits/:visitId/edit",
            element: (
              <RoleGuard roles={[RoleDoctor]}>
                <VisitFormPage />
              </RoleGuard>
            ),
          },
          {
            path: "prescriptions",
            element: (
              <RoleGuard roles={ClinicalStaffRoles}>
                <PrescriptionsListPage />
              </RoleGuard>
            ),
          },
          {
            path: "prescriptions/me",
            element: (
              <RoleGuard roles={[RolePatient]}>
                <MyPrescriptionsPage />
              </RoleGuard>
            ),
          },
          {
            path: "prescriptions/new",
            element: (
              <RoleGuard roles={[RoleDoctor]}>
                <IssuePrescriptionPage />
              </RoleGuard>
            ),
          },
          {
            path: "prescriptions/:prescriptionId",
            element: (
              <RoleGuard roles={[RoleDoctor, RoleSecretary, RolePatient]}>
                <PrescriptionDetailPage />
              </RoleGuard>
            ),
          },
          {
            path: "invoices",
            element: (
              <RoleGuard roles={ClinicalStaffRoles}>
                <InvoicesListPage />
              </RoleGuard>
            ),
          },
          {
            path: "invoices/me",
            element: (
              <RoleGuard roles={[RolePatient]}>
                <MyInvoicesPage />
              </RoleGuard>
            ),
          },
          {
            path: "invoices/new",
            element: (
              <RoleGuard roles={[RoleSecretary]}>
                <CreateInvoicePage />
              </RoleGuard>
            ),
          },
          {
            path: "invoices/:invoiceId",
            element: (
              <RoleGuard roles={[RoleDoctor, RoleSecretary, RolePatient]}>
                <InvoiceDetailPage />
              </RoleGuard>
            ),
          },
          {
            path: "reports",
            element: (
              <RoleGuard roles={[RoleDoctor, RoleSecretary]}>
                <ReportsPage />
              </RoleGuard>
            ),
          },
          {
            path: "me",
            element: (
              <RoleGuard roles={[RolePatient]}>
                <MyHealthPage />
              </RoleGuard>
            ),
          },
          { path: "forbidden", element: <ForbiddenPage /> },
          { path: "*", element: <NotFoundPage /> },
        ],
      },
    ],
  },
]);

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <I18nProvider>
          <ToastProvider>
            <AuthProvider>
              <RouterProvider router={router} />
            </AuthProvider>
          </ToastProvider>
        </I18nProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}