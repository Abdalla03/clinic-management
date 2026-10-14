import { api, unwrap } from "./client";
import type { components } from "./schema";
import { tokenStore, type TokenSet } from "./tokens";

type TokenResponse = components["schemas"]["TokenResponse"];
export type LoginResult = components["schemas"]["LoginResult"];
export type CurrentUserResponse = components["schemas"]["CurrentUserResponse"];
export type MfaEnrollment = components["schemas"]["MfaEnrollment"];

export function tokenSetFromResponse(tokens: TokenResponse): TokenSet {
  return {
    accessToken: tokens.accessToken ?? "",
    accessTokenExpiresAtUtc: tokens.accessTokenExpiresAtUtc ?? "",
    refreshToken: tokens.refreshToken ?? "",
    refreshTokenExpiresAtUtc: tokens.refreshTokenExpiresAtUtc ?? "",
  };
}

export async function login(emailOrUserName: string, password: string): Promise<LoginResult> {
  const result = await api.POST("/api/auth/login", {
    body: { emailOrUserName, password },
  });
  const payload = await unwrap(result);
  if (payload.tokens) {
    tokenStore.set(tokenSetFromResponse(payload.tokens));
  }
  return payload;
}

export async function verifyMfa(mfaPendingToken: string, code: string): Promise<void> {
  const result = await api.POST("/api/auth/mfa/verify", {
    body: { mfaPendingToken, code },
  });
  const tokens = await unwrap(result);
  tokenStore.set(tokenSetFromResponse(tokens));
}

export async function logout(): Promise<void> {
  const refreshToken = tokenStore.get()?.refreshToken;
  if (refreshToken) {
    await api.POST("/api/auth/logout", { body: { refreshToken } }).catch(() => undefined);
  }
  tokenStore.clear();
}

export async function getCurrentUser(): Promise<CurrentUserResponse> {
  const result = await api.GET("/api/auth/me");
  return unwrap(result);
}

export async function beginMfaEnrollment(): Promise<MfaEnrollment> {
  const result = await api.POST("/api/auth/mfa/enroll");
  return unwrap(result);
}

export async function confirmMfaEnrollment(code: string): Promise<void> {
  const result = await api.POST("/api/auth/mfa/confirm", { body: { code } });
  await unwrap(result);
}

export async function disableMfa(password: string): Promise<void> {
  const result = await api.POST("/api/auth/mfa/disable", { body: { password } });
  await unwrap(result);
}