import createClient from "openapi-fetch";
import { ApiRequestError } from "../problem";
import type { paths } from "./schema";
import { tokenStore } from "./tokens";

export const API_BASE: string = resolveApiBase();

/**
 * Picks the API base URL, honoring (in order):
 * 1. `window.__CLINIC_API_URL__` set at runtime by nginx via `config.js` (WEB_API_URL),
 * 2. the build-time `VITE_API_URL` constant inlined by Vite,
 * 3. the local development default.
 */
function resolveApiBase(): string {
  const browser = typeof window !== "undefined" ? window : undefined;
  const runtimeUrl = browser
    ? (browser as Window & { __CLINIC_API_URL__?: string }).__CLINIC_API_URL__
    : undefined;
  if (runtimeUrl && runtimeUrl.trim()) {
    return runtimeUrl.trim().replace(/\/+$/, "");
  }
  return (import.meta.env.VITE_API_URL ?? "http://localhost:8080").replace(/\/+$/, "");
}

const PUBLIC_AUTH_PATH_SUFFIXES = ["/api/auth/refresh", "/api/auth/login", "/api/auth/mfa/verify"];

let refreshInFlight: Promise<boolean> | null = null;

export function notifySessionExpired(): void {
  tokenStore.clear();
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event("auth:session-expired"));
  }
}

async function doRefresh(): Promise<boolean> {
  const current = tokenStore.get();
  if (!current?.refreshToken) {
    return false;
  }
  try {
    const response = await fetch(`${API_BASE}/api/auth/refresh`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ refreshToken: current.refreshToken }),
    });
    if (!response.ok) {
      notifySessionExpired();
      return false;
    }
    const body = (await response.json()) as {
      accessToken: string;
      accessTokenExpiresAtUtc: string;
      refreshToken: string;
      refreshTokenExpiresAtUtc: string;
    };
    tokenStore.set({
      accessToken: body.accessToken,
      accessTokenExpiresAtUtc: body.accessTokenExpiresAtUtc,
      refreshToken: body.refreshToken,
      refreshTokenExpiresAtUtc: body.refreshTokenExpiresAtUtc,
    });
    return true;
  } catch {
    notifySessionExpired();
    return false;
  }
}

function refreshTokens(): Promise<boolean> {
  if (!refreshInFlight) {
    refreshInFlight = doRefresh().finally(() => {
      refreshInFlight = null;
    });
  }
  return refreshInFlight;
}

function isPublicAuthPath(url: string): boolean {
  let pathname: string;
  try {
    pathname = new URL(url).pathname;
  } catch {
    return false;
  }
  return PUBLIC_AUTH_PATH_SUFFIXES.some((suffix) => pathname.endsWith(suffix));
}

function withAuthorization(request: Request, token: string): Request {
  const next = new Request(request);
  next.headers.set("Authorization", `Bearer ${token}`);
  return next;
}

/**
 * Fetch wrapper that injects the in-memory access token and transparently tries a single-flight
 * refresh on a 401 before giving up (in which case the session is cleared and the caller is
 * redirected to the login screen via the "auth:session-expired" event).
 */
export async function fetchWithAuth(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const request = input instanceof Request ? input : new Request(input, init);
  const current = tokenStore.get();

  if (!current?.accessToken || isPublicAuthPath(request.url)) {
    return fetch(request);
  }

  const first = await fetch(withAuthorization(request, current.accessToken));
  if (first.status !== 401) {
    return first;
  }

  const refreshed = await refreshTokens();
  const rotated = tokenStore.get();
  if (!refreshed || !rotated?.accessToken) {
    return first;
  }

  return fetch(withAuthorization(request, rotated.accessToken));
}

export const api = createClient<paths>({
  baseUrl: API_BASE,
  fetch: fetchWithAuth,
});

export type ApiResult<T> = { data: T };

/** Resolves an openapi-fetch result into the payload, throwing a typed ApiRequestError on failure. */
export async function unwrap<T>(result: {
  data?: T;
  error?: unknown;
  response: Response;
}): Promise<T> {
  if (result.error !== undefined) {
    throw new ApiRequestError(result.response.status, result.error);
  }
  return result.data as T;
}