import type {
  AdminAttraction,
  AdminCollectionRun,
  AdminLand,
  AdminObservation,
  AdminPark,
  CurrentWaitTimesResult,
  OptimizedItinerary,
  OptimizeItineraryRequest,
  StartVisitSessionRequest,
  VisitSession,
  DailyParkWaitTimesResult,
  DailyWaitTimeHistoryResult,
  Park,
  SaveAdminAttraction,
  SaveAdminLand,
  SaveAdminPark,
  WeekdayWaitTimePatternsResult,
  WalkingTimeEstimate,
} from './contracts'

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(path, {
    signal,
    headers: { Accept: 'application/json' },
    credentials: 'same-origin',
  })

  if (!response.ok) {
    throw new Error(`API request failed with status ${response.status}.`)
  }

  return response.json() as Promise<T>
}

async function requestJson<T>(
  path: string,
  options: RequestInit,
): Promise<T> {
  const response = await fetch(path, {
    ...options,
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      ...options.headers,
    },
    credentials: 'same-origin',
  })

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as
      | { detail?: string; errors?: Record<string, string[]> }
      | null
    const validationMessage = problem?.errors
      ? Object.values(problem.errors).flat()[0]
      : undefined
    throw new Error(
      validationMessage ??
        problem?.detail ??
        `API request failed with status ${response.status}.`,
    )
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}

export function getParks(signal?: AbortSignal) {
  return getJson<Park[]>('/api/v1/parks', signal)
}

export function getCurrentWaitTimes(parkId: number, signal?: AbortSignal) {
  return getJson<CurrentWaitTimesResult>(
    `/api/v1/parks/${parkId}/wait-times/current`,
    signal,
  )
}

export function optimizeItinerary(
  parkId: number,
  request: OptimizeItineraryRequest,
  signal?: AbortSignal,
) {
  return requestJson<OptimizedItinerary>(
    `/api/v1/parks/${parkId}/itineraries/optimize`,
    {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    },
  )
}

export function startVisitSession(
  parkId: number,
  request: StartVisitSessionRequest,
  signal?: AbortSignal,
) {
  return requestJson<VisitSession>(
    `/api/v1/parks/${parkId}/visit-sessions`,
    {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    },
  )
}

export function getVisitSession(sessionId: string, signal?: AbortSignal) {
  return getJson<VisitSession>(
    `/api/v1/visit-sessions/${encodeURIComponent(sessionId)}`,
    signal,
  )
}

export function completeVisitAttraction(
  sessionId: string,
  attractionId: number,
) {
  return requestJson<VisitSession>(
    `/api/v1/visit-sessions/${encodeURIComponent(sessionId)}` +
      `/stops/${attractionId}/complete`,
    { method: 'POST' },
  )
}

export function skipVisitAttraction(
  sessionId: string,
  attractionId: number,
) {
  return requestJson<VisitSession>(
    `/api/v1/visit-sessions/${encodeURIComponent(sessionId)}` +
      `/stops/${attractionId}/skip`,
    { method: 'POST' },
  )
}

export function estimateWalkingTime(
  parkId: number,
  fromAttractionId: number,
  toAttractionId: number,
  signal?: AbortSignal,
) {
  const query = new URLSearchParams({
    fromAttractionId: fromAttractionId.toString(),
    toAttractionId: toAttractionId.toString(),
  })
  return getJson<WalkingTimeEstimate>(
    `/api/v1/parks/${parkId}/walking-time?${query}`,
    signal,
  )
}

export function getDailyWaitTimeHistory(
  parkId: number,
  attractionId: number,
  signal?: AbortSignal,
) {
  const query = new URLSearchParams({ attractionId: attractionId.toString() })
  return getJson<DailyWaitTimeHistoryResult>(
    `/api/v1/parks/${parkId}/analytics/wait-times/daily?${query}`,
    signal,
  )
}

export function getDailyParkWaitTimes(
  weekStart?: string,
  signal?: AbortSignal,
) {
  const query = new URLSearchParams()
  if (weekStart) {
    query.set('weekStart', weekStart)
  }
  const queryString = query.size ? `?${query}` : ''
  return getJson<DailyParkWaitTimesResult>(
    `/api/v1/analytics/parks/daily${queryString}`,
    signal,
  )
}

export function getWeekdayWaitTimePatterns(
  parkId: number,
  attractionId: number,
  signal?: AbortSignal,
) {
  const query = new URLSearchParams({ attractionId: attractionId.toString() })
  return getJson<WeekdayWaitTimePatternsResult>(
    `/api/v1/parks/${parkId}/analytics/wait-times/weekday-quarter-hourly?${query}`,
    signal,
  )
}

export function getAdminParks(signal?: AbortSignal) {
  return getJson<AdminPark[]>('/api/v1/admin/parks', signal)
}

export function createAdminPark(park: SaveAdminPark) {
  return requestJson<AdminPark>('/api/v1/admin/parks', {
    method: 'POST',
    body: JSON.stringify(park),
  })
}

export function saveAdminPark(parkId: number, park: SaveAdminPark) {
  return requestJson<AdminPark>(`/api/v1/admin/parks/${parkId}`, {
    method: 'PUT',
    body: JSON.stringify(park),
  })
}

export function collectAdminPark(parkId: number) {
  return requestJson(`/api/v1/admin/parks/${parkId}/collect`, {
    method: 'POST',
  })
}

export function getAdminLands(parkId: number, signal?: AbortSignal) {
  return getJson<AdminLand[]>(
    `/api/v1/admin/parks/${parkId}/lands`,
    signal,
  )
}

export function createAdminLand(land: SaveAdminLand) {
  return requestJson<AdminLand>('/api/v1/admin/lands', {
    method: 'POST',
    body: JSON.stringify(land),
  })
}

export function saveAdminLand(landId: number, land: SaveAdminLand) {
  return requestJson<AdminLand>(`/api/v1/admin/lands/${landId}`, {
    method: 'PUT',
    body: JSON.stringify(land),
  })
}

export function getAdminAttractions(parkId: number, signal?: AbortSignal) {
  return getJson<AdminAttraction[]>(
    `/api/v1/admin/parks/${parkId}/attractions`,
    signal,
  )
}

export function createAdminAttraction(attraction: SaveAdminAttraction) {
  return requestJson<AdminAttraction>('/api/v1/admin/attractions', {
    method: 'POST',
    body: JSON.stringify(attraction),
  })
}

export function saveAdminAttraction(
  attractionId: number,
  attraction: SaveAdminAttraction,
) {
  return requestJson<AdminAttraction>(
    `/api/v1/admin/attractions/${attractionId}`,
    {
      method: 'PUT',
      body: JSON.stringify(attraction),
    },
  )
}

export function getAdminCollectionRuns(
  parkId?: number,
  signal?: AbortSignal,
) {
  const query = new URLSearchParams({ limit: '100' })
  if (parkId) {
    query.set('parkId', parkId.toString())
  }
  return getJson<AdminCollectionRun[]>(
    `/api/v1/admin/collection-runs?${query}`,
    signal,
  )
}

export function retryAdminCollectionRun(runId: number) {
  return requestJson(`/api/v1/admin/collection-runs/${runId}/retry`, {
    method: 'POST',
  })
}

export function getAdminObservations(
  parkId: number,
  attractionId?: number,
  signal?: AbortSignal,
) {
  const query = new URLSearchParams({
    parkId: parkId.toString(),
    includeInvalid: 'true',
    limit: '150',
  })
  if (attractionId) {
    query.set('attractionId', attractionId.toString())
  }
  return getJson<AdminObservation[]>(
    `/api/v1/admin/observations?${query}`,
    signal,
  )
}

export function createAdminObservation(observation: {
  attractionId: number
  observedAt: string
  isOpen: boolean
  waitMinutes: number | null
}) {
  return requestJson<AdminObservation>('/api/v1/admin/observations', {
    method: 'POST',
    body: JSON.stringify(observation),
  })
}

export function setAdminObservationValidity(
  observationId: number,
  isValid: boolean,
  reason?: string,
) {
  return requestJson<void>(
    `/api/v1/admin/observations/${observationId}/validity`,
    {
      method: 'PUT',
      body: JSON.stringify({ isValid, reason }),
    },
  )
}

export function purgeAdminObservations(request: {
  parkId: number
  attractionId: number | null
  from: string
  to: string
  confirmation: string
}) {
  return requestJson<{ deletedCount: number }>(
    '/api/v1/admin/observations/purge',
    {
    method: 'POST',
    body: JSON.stringify(request),
    },
  )
}
