import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { Route, Routes } from "react-router-dom";
import { renderWithProviders } from "../../test/test-utils";
import { PatientFormPage } from "./PatientFormPage";

const { createPatientMock } = vi.hoisted(() => ({ createPatientMock: vi.fn() }));

vi.mock("../../lib/api/patients", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../lib/api/patients")>();
  return { ...actual, createPatient: createPatientMock };
});

function PatientFormApp() {
  return (
    <Routes>
      <Route path="/patients/new" element={<PatientFormPage />} />
      <Route path="/patients" element={<div>Patients list page</div>} />
    </Routes>
  );
}

describe("PatientFormPage (create)", () => {
  beforeEach(() => {
    createPatientMock.mockReset();
  });

  it("does not submit while required fields are empty", async () => {
    renderWithProviders(<PatientFormApp />, { route: "/patients/new" });
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() => {
      expect(createPatientMock).not.toHaveBeenCalled();
    });
    expect(screen.getByRole("button", { name: "Save" })).toBeInTheDocument();
  });

  it("creates a patient and returns to the list", async () => {
    createPatientMock.mockResolvedValue({
      id: "p1",
      firstName: "Yasmin",
      lastName: "Ali",
      fullName: "Yasmin Ali",
      medicalRecordNumber: "MRN-001",
    });
    renderWithProviders(<PatientFormApp />, { route: "/patients/new" });
    const user = userEvent.setup();

    await user.type(screen.getByLabelText(/first name/i), "Yasmin");
    await user.type(screen.getByLabelText(/last name/i), "Ali");
    await user.type(screen.getByLabelText(/medical record number/i), "MRN-001");
    await user.selectOptions(screen.getByLabelText(/gender/i), "2");
    await user.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() => {
      expect(createPatientMock).toHaveBeenCalledWith(
        expect.objectContaining({
          firstName: "Yasmin",
          lastName: "Ali",
          medicalRecordNumber: "MRN-001",
          gender: 2,
        }),
      );
    });
    expect(await screen.findByText("Patients list page")).toBeInTheDocument();
  });
});