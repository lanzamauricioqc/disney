import type {
  CurrentWaitTime,
  VisitSession,
  VisitSessionStop,
} from '../../api/contracts'

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

export function getNextPendingStop(
  session: VisitSession,
): VisitSessionStop | null {
  return session.stops
    .filter(stop => stop.status === 'Pending')
    .sort((left, right) => left.sequence - right.sequence)[0] ?? null
}

export function getFreshCurrentWaitForStop(
  waits: readonly CurrentWaitTime[],
  stop: VisitSessionStop,
  now = new Date(),
): CurrentWaitTime | null {
  const wait = waits.find(candidate => candidate.attractionId === stop.attractionId)
  if (!wait) return null

  const observedAt = new Date(wait.observedAt).getTime()
  const ageMilliseconds = now.getTime() - observedAt
  const maximumAgeMilliseconds = 15 * 60_000
  const maximumFutureSkewMilliseconds = 5 * 60_000
  if (
    !Number.isFinite(observedAt) ||
    ageMilliseconds > maximumAgeMilliseconds ||
    ageMilliseconds < -maximumFutureSkewMilliseconds
  ) {
    return null
  }
  return wait
}

export function getLastCompletedStop(
  session: VisitSession,
): VisitSessionStop | null {
  return session.stops
    .filter(stop => stop.status === 'Completed')
    .sort((left, right) => right.sequence - left.sequence)[0] ?? null
}
