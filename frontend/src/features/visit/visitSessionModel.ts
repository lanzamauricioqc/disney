const visitSessionStorageKey = 'park-pilot-visit-session-id'
const visitSessionIdPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export function readVisitSessionId(): string | null {
  const value = localStorage.getItem(visitSessionStorageKey)
  return value && visitSessionIdPattern.test(value) ? value : null
}

export function saveVisitSessionId(sessionId: string) {
  if (!visitSessionIdPattern.test(sessionId)) {
    throw new Error('The visit session ID is invalid.')
  }
  localStorage.setItem(visitSessionStorageKey, sessionId)
}

export function clearVisitSessionId() {
  localStorage.removeItem(visitSessionStorageKey)
}
