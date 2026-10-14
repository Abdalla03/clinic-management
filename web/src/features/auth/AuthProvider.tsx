import { useQuery, useQueryClient } from "@tanstack/react-query";
import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { getCurrentUser, logout, type CurrentUserResponse } from "../../lib/api/auth";
import { tokenStore } from "../../lib/api/tokens";
import { hasRole, RolePatient, RoleSecretary, RoleDoctor, type Role } from "../../lib/roles";

export interface AuthContextValue {
  user: ExistingUser | null;
  loading: boolean;
  isAuthenticated: boolean;
  roles: string[];
  refreshUser: () => void;
  signOut: () => Promise<void>;
  hasRole: (role: Role) => boolean;
  isClinicalStaff: boolean;
  isPatient: boolean;
}

export type ExistingUser = CurrentUserResponse;

export const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [hasTokens, setHasTokens] = useState(() => tokenStore.get() !== null);

  useEffect(() => {
    const sync = () => setHasTokens(tokenStore.get() !== null);
    window.addEventListener("auth:tokens-changed", sync);
    return () => window.removeEventListener("auth:tokens-changed", sync);
  }, []);

  const meQuery = useQuery({
    queryKey: ["auth", "me"],
    queryFn: getCurrentUser,
    enabled: hasTokens,
    staleTime: 60_000,
    retry: false,
  });

  const user = hasTokens ? (meQuery.data ?? null) : null;
  const loading = hasTokens && meQuery.isPending;
  const roles = useMemo(() => user?.roles ?? [], [user]);

  const refreshUser = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: ["auth", "me"] });
  }, [queryClient]);

  const signOut = useCallback(async () => {
    try {
      await logout();
    } finally {
      void queryClient.clear();
      setHasTokens(false);
    }
  }, [queryClient]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      loading,
      isAuthenticated: user !== null,
      roles,
      refreshUser,
      signOut,
      hasRole: (role: Role) => hasRole(roles, role),
      isClinicalStaff: roles.some((role) => role === RoleDoctor || role === RoleSecretary),
      isPatient: hasRole(roles, RolePatient),
    }),
    [user, loading, roles, refreshUser, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}