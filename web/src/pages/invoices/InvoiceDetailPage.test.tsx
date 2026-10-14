import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { Route, Routes } from "react-router-dom";
import { renderWithProviders } from "../../test/test-utils";
import { InvoiceDetailPage } from "./InvoiceDetailPage";

const { getInvoiceMock } = vi.hoisted(() => ({ getInvoiceMock: vi.fn() }));
const { recordPaymentMock } = vi.hoisted(() => ({ recordPaymentMock: vi.fn() }));
const { issueInvoiceMock } = vi.hoisted(() => ({ issueInvoiceMock: vi.fn() }));
const { voidInvoiceMock } = vi.hoisted(() => ({ voidInvoiceMock: vi.fn() }));
const { hasRoleMock } = vi.hoisted(() => ({ hasRoleMock: vi.fn() }));

vi.mock("../../lib/api/invoices", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../lib/api/invoices")>();
  return {
    ...actual,
    getInvoice: getInvoiceMock,
    recordPayment: recordPaymentMock,
    issueInvoice: issueInvoiceMock,
    voidInvoice: voidInvoiceMock,
  };
});

vi.mock("../../features/auth/useAuth", () => ({
  useAuth: () => ({ hasRole: hasRoleMock }),
}));

const ISSUED_INVOICE = {
  id: "inv-1",
  invoiceNumber: "INV-1001",
  patientId: "p1",
  patientName: "Ahmed Hassan",
  visitId: null,
  status: 2,
  currency: "EGP",
  totalAmount: 2000,
  amountPaid: 1500,
  outstandingAmount: 500,
  issuedAtUtc: "2026-10-01T09:00:00.000Z",
  createdAtUtc: "2026-09-30T10:00:00.000Z",
  voidedAtUtc: null,
  voidReason: null,
  createdByUserId: "u1",
  updatedAtUtc: "2026-10-01T09:00:00.000Z",
  lines: [{ id: "l1", description: "Consultation", unitPrice: 2000, quantity: 1, lineTotal: 2000 }],
  payments: [],
};

function InvoiceApp() {
  return (
    <Routes>
      <Route path="/invoices/:invoiceId" element={<InvoiceDetailPage />} />
      <Route path="/invoices" element={<div>Invoices list page</div>} />
    </Routes>
  );
}

function openPaymentForm(): HTMLElement {
  const amount = screen.getByLabelText(/amount/i);
  return amount;
}

describe("InvoiceDetailPage payments", () => {
  beforeEach(() => {
    hasRoleMock.mockImplementation((role: string) => role === "Secretary");
    getInvoiceMock.mockReset().mockResolvedValue(ISSUED_INVOICE);
    recordPaymentMock.mockReset().mockResolvedValue(ISSUED_INVOICE);
    issueInvoiceMock.mockReset().mockResolvedValue(ISSUED_INVOICE);
    voidInvoiceMock.mockReset().mockResolvedValue(ISSUED_INVOICE);
  });

  it("rejects a payment larger than the outstanding balance", async () => {
    renderWithProviders(<InvoiceApp />, { route: "/invoices/inv-1" });
    const user = userEvent.setup();

    await screen.findByText("INV-1001");
    await user.click(screen.getByRole("button", { name: "Record payment" }));

    const amount = openPaymentForm();
    await user.clear(amount);
    await user.type(amount, "900");

    const submitButtons = screen.getAllByRole("button", { name: "Record payment" });
    await user.click(submitButtons.at(-1)!);

    expect(await screen.findByText("Amount exceeds the outstanding balance.")).toBeInTheDocument();
    expect(recordPaymentMock).not.toHaveBeenCalled();
  });

  it("records a valid payment and confirms it", async () => {
    renderWithProviders(<InvoiceApp />, { route: "/invoices/inv-1" });
    const user = userEvent.setup();

    await screen.findByText("INV-1001");
    await user.click(screen.getByRole("button", { name: "Record payment" }));

    const amount = openPaymentForm();
    await user.clear(amount);
    await user.type(amount, "200");
    await user.selectOptions(screen.getByRole("combobox"), "2");

    const submitButtons = screen.getAllByRole("button", { name: "Record payment" });
    await user.click(submitButtons.at(-1)!);

    await vi.waitFor(() => {
      expect(recordPaymentMock).toHaveBeenCalledWith("inv-1", {
        amount: 200,
        method: 2,
        reference: undefined,
      });
    });
    expect(await screen.findByText("Payment recorded.")).toBeInTheDocument();
  });
});