import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../features/auth/useAuth";
import { useI18n } from "../lib/i18n/useI18n";
import type { Role } from "../lib/roles";
import { ForbiddenPage } from "../pages/ForbiddenPage";

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { user, loading } = useAuth();
  const { t } = useI18n();
  const location = useLocation();

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="animate-pulse text-sm text-slate-400">{t("common.loading")}</div>
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }

  return children;
}

export function RoleGuard({ roles, children }: { roles: readonly Role[]; children: ReactNode }) {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading || !user) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }

  const allowed = roles.some((role) => user.roles?.includes(role));
  if (!allowed) {
    return <ForbiddenPage />;
  }

  return children;
}