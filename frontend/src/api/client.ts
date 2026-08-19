import type {
  CurrentWaitTimesResult,
  DailyWaitTimeHistoryResult,
  Park,
  WeekdayWaitTimePatternsResult,
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

export function getParks(signal?: AbortSignal) {
  return getJson<Park[]>('/api/v1/parks', signal)
}

export function getCurrentWaitTimes(parkId: number, signal?: AbortSignal) {
  return getJson<CurrentWaitTimesResult>(
    `/api/v1/parks/${parkId}/wait-times/current`,
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
