import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { Route, Routes } from "react-router-dom";
import { renderWithProviders } from "../test/test-utils";
import { LoginPage } from "./LoginPage";

const { loginMock } = vi.hoisted(() => ({ loginMock: vi.fn() }));
const { useAuthMock } = vi.hoisted(() => ({ useAuthMock: vi.fn() }));

vi.mock("../lib/api/auth", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../lib/api/auth")>();
  return { ...actual, login: loginMock };
});

vi.mock("../features/auth/useAuth", () => ({
  useAuth: useAuthMock,
}));

function LoginApp() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/" element={<div>Home dashboard</div>} />
    </Routes>
  );
}

describe("LoginPage", () => {
  beforeEach(() => {
    useAuthMock.mockReturnValue({ isAuthenticated: false });
    loginMock.mockReset();
  });

  it("requires both credentials before submitting", async () => {
    renderWithProviders(<LoginApp />, { route: "/login" });
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findAllByText("Required")).toHaveLength(2);
    expect(loginMock).not.toHaveBeenCalled();
  });

  it("signs in with valid credentials and navigates to the dashboard", async () => {
    loginMock.mockResolvedValue({
      mfaRequired: false,
      tokens: {
        accessToken: "access-token",
        accessTokenExpiresAtUtc: "2026-10-14T09:00:00.000Z",
        refreshToken: "refresh-token",
        refreshTokenExpiresAtUtc: "2026-11-14T09:00:00.000Z",
      },
    });
    renderWithProviders(<LoginApp />, { route: "/login" });
    const user = userEvent.setup();

    await user.type(screen.getByLabelText(/email or username/i), "secretary@clinic.local");
    await user.type(screen.getByLabelText(/password/i), "Secretary!Clinic2026");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByText("Home dashboard")).toBeInTheDocument();
    expect(loginMock).toHaveBeenCalledWith("secretary@clinic.local", "Secretary!Clinic2026");
  });
});