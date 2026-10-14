import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../lib/i18n/I18nProvider";
import { ToastProvider } from "../lib/toast/ToastProvider";

export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: 0, refetchOnWindowFocus: false },
      mutations: { retry: false },
    },
  });
}

interface RenderWithProvidersOptions {
  route: string;
  queryClient?: QueryClient;
}

export function renderWithProviders(ui: ReactElement, options: Partial<RenderWithProvidersOptions> = {}) {
  const { route = "/", queryClient = createTestQueryClient() } = options;
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <ToastProvider>
          <MemoryRouter initialEntries={[route]}>{children}</MemoryRouter>
        </ToastProvider>
      </I18nProvider>
    </QueryClientProvider>
  );
  return { queryClient, ...render(ui, { wrapper: Wrapper }) };
}