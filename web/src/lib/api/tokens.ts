export interface TokenSet {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

let tokens: TokenSet | null = null;

function emitChange(): void {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event("auth:tokens-changed"));
  }
}

export const tokenStore = {
  get(): TokenSet | null {
    return tokens;
  },
  set(next: TokenSet): void {
    tokens = next;
    emitChange();
  },
  clear(): void {
    tokens = null;
    emitChange();
  },
};

export function hasValidAccessToken(): boolean {
  const current = tokens;
  if (!current?.accessTokenExpiresAtUtc) {
    return current?.accessToken !== undefined;
  }
  return Date.parse(current.accessTokenExpiresAtUtc) > Date.now();
}