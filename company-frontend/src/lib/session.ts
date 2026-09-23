const accessTokenStorageKey = "parkPilotCompanyToken";
const sessionStorageKey = "parkPilotCompanySession";

export function getAccessToken(): string | null {
  return sessionStorage.getItem(accessTokenStorageKey);
}

export function readStoredSession(): string | null {
  return sessionStorage.getItem(sessionStorageKey);
}

export function storeSession(accessToken: string, session: string): void {
  sessionStorage.setItem(accessTokenStorageKey, accessToken);
  sessionStorage.setItem(sessionStorageKey, session);
}

export function clearSession(): void {
  sessionStorage.removeItem(accessTokenStorageKey);
  sessionStorage.removeItem(sessionStorageKey);
}
