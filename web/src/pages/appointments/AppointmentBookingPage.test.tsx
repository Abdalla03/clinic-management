import { fireEvent, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { Route, Routes } from "react-router-dom";
import { renderWithProviders } from "../../test/test-utils";
import { AppointmentBookingPage } from "./AppointmentBookingPage";

const { searchPatientsMock } = vi.hoisted(() => ({ searchPatientsMock: vi.fn() }));
const { listDoctorsMock } = vi.hoisted(() => ({ listDoctorsMock: vi.fn() }));
const { getAvailabilityMock } = vi.hoisted(() => ({ getAvailabilityMock: vi.fn() }));
const { bookAppointmentMock } = vi.hoisted(() => ({ bookAppointmentMock: vi.fn() }));

vi.mock("../../lib/api/patients", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../lib/api/patients")>();
  return { ...actual, searchPatients: searchPatientsMock };
});

vi.mock("../../lib/api/doctors", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../lib/api/doctors")>();
  return { ...actual, listDoctors: listDoctorsMock, getAvailability: getAvailabilityMock };
});

vi.mock("../../lib/api/appointments", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../lib/api/appointments")>();
  return { ...actual, bookAppointment: bookAppointmentMock };
});

function BookingApp() {
  return (
    <Routes>
      <Route path="/appointments/new" element={<AppointmentBookingPage />} />
      <Route path="/appointments/:appointmentId" element={<div>Appointment detail page</div>} />
    </Routes>
  );
}

const SLOT = "2026-10-15T09:00:00.000Z";

describe("AppointmentBookingPage", () => {
  beforeEach(() => {
    searchPatientsMock.mockReset().mockResolvedValue({
      items: [{ id: "p1", firstName: "Ahmed", lastName: "Hassan", medicalRecordNumber: "MRN-1" }],
      page: 1,
      pageSize: 8,
      totalCount: 1,
    });
    listDoctorsMock.mockReset().mockResolvedValue([{ id: "d1", name: "Dr. Sara" }]);
    getAvailabilityMock.mockReset().mockResolvedValue([{ startsAtUtc: SLOT }]);
    bookAppointmentMock.mockReset();
  });

  it("requires a patient, doctor and slot", async () => {
    renderWithProviders(<BookingApp />, { route: "/appointments/new" });
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Book appointment" }));

    expect(await screen.findByText("Choose an available time.")).toBeInTheDocument();
    expect(screen.getAllByText("Choose a doctor.")).toHaveLength(2);
    expect(screen.getByText("Choose a patient.")).toBeInTheDocument();
    expect(bookAppointmentMock).not.toHaveBeenCalled();
  });

  it("books an appointment for the selected patient, doctor and slot", async () => {
    bookAppointmentMock.mockResolvedValue({ id: "a1", patientName: "Ahmed Hassan" });
    renderWithProviders(<BookingApp />, { route: "/appointments/new" });
    const user = userEvent.setup();

    await user.click(await screen.findByText("Ahmed Hassan"));

    await user.selectOptions(screen.getByLabelText(/doctor/i), "d1");

    const dateInput = screen.getByLabelText(/date/i);
    fireEvent.change(dateInput, { target: { value: "2026-10-15" } });

    const slotButton = await screen.findByRole("button", { name: /^\d{1,2}:\d{2}/ });
    await user.click(slotButton);

    await user.click(screen.getByRole("button", { name: "Book appointment" }));

    await vi.waitFor(() => {
      expect(bookAppointmentMock).toHaveBeenCalledWith({
        patientId: "p1",
        doctorId: "d1",
        startsAtUtc: SLOT,
        durationMinutes: 30,
        reasonForVisit: undefined,
      });
    });
    expect(await screen.findByText("Appointment detail page")).toBeInTheDocument();
  });
});