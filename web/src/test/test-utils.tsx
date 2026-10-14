import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import { AuthContext, type AuthContextValue } from "../features/auth/AuthProvider";
import { I18nProvider } from "../lib/i18n/I18nProvider";
import { RoleDoctor, RolePatient, RoleSecretary, type Role } from "../lib/roles";
import { ThemeProvider } from "../lib/theme/ThemeProvider";
import { ToastProvider } from "../lib/toast/ToastProvider";

export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: 0, refetchOnWindowFocus: false },
      mutations: { retry: false },
    },
  });
}

export function createTestAuthValue(roles: readonly string[] = [RoleSecretary]): AuthContextValue {
  return {
    user: null,
    loading: false,
    isAuthenticated: true,
    roles: [...roles],
    refreshUser: () => undefined,
    signOut: async () => undefined,
    hasRole: (role: Role) => roles.includes(role),
    isClinicalStaff: roles.includes(RoleDoctor) || roles.includes(RoleSecretary),
    isPatient: roles.includes(RolePatient),
  };
}

interface RenderWithProvidersOptions {
  route: string;
  queryClient?: QueryClient;
  authRoles?: readonly string[];
}

export function renderWithProviders(ui: ReactElement, options: Partial<RenderWithProvidersOptions> = {}) {
  const { route = "/", queryClient = createTestQueryClient(), authRoles = [RoleSecretary] } = options;
  const authValue = createTestAuthValue(authRoles);
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <AuthContext.Provider value={authValue}>
          <I18nProvider>
            <ToastProvider>
              <MemoryRouter initialEntries={[route]}>{children}</MemoryRouter>
            </ToastProvider>
          </I18nProvider>
        </AuthContext.Provider>
      </ThemeProvider>
    </QueryClientProvider>
  );
  return { queryClient, ...render(ui, { wrapper: Wrapper }) };
}
