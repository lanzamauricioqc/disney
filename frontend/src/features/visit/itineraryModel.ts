import type {
  CurrentWaitTime,
  ItineraryPreference,
  ItineraryStop,
  OptimizedItinerary,
  OptimizeItineraryRequest,
  Park,
} from '../../api/contracts'
import type { AttractionPriorities, AttractionPriority } from './priorityModel'
import type { VisitDetails } from './visitSetupModel'

interface ZonedDateTimeParts {
  year: number
  month: number
  day: number
  hour: number
  minute: number
  second: number
}

export interface GeneratedItinerary {
  itinerary: OptimizedItinerary
  park: Park
  attractionNames: Readonly<Record<number, string>>
}

const priorityMap: Record<AttractionPriority, ItineraryPreference> = {
  'must-do': 'MustDo',
  'would-like': 'WouldLike',
  skip: 'Skip',
}

export function parkLocalDateTimeToIso(
  date: string,
  time: string,
  timeZone: string,
): string {
  const target = parseLocalDateTime(date, time)
  const wallClockUtc = partsToTimestamp(target)
  const possibleInstants = new Set<number>()

  for (let hours = -48; hours <= 48; hours += 6) {
    const sample = wallClockUtc + hours * 60 * 60_000
    const represented = zonedParts(new Date(sample), timeZone)
    const offset = partsToTimestamp(represented) - sample
    const candidate = wallClockUtc - offset
    if (sameParts(target, zonedParts(new Date(candidate), timeZone))) {
      possibleInstants.add(candidate)
    }
  }

  if (possibleInstants.size !== 1) {
    const reason = possibleInstants.size === 0 ? 'does not exist' : 'is ambiguous'
    throw new RangeError(`The local time ${date} ${time} ${reason} in ${timeZone}.`)
  }

  const [instant] = possibleInstants
  const offsetMinutes = Math.round((wallClockUtc - instant) / 60_000)
  return `${date}T${time}:00${formatOffset(offsetMinutes)}`
}

export function buildOptimizeItineraryRequest(
  visit: VisitDetails,
  timeZone: string,
  priorities: AttractionPriorities,
  now = new Date(),
): OptimizeItineraryRequest {
  const today = zonedParts(now, timeZone)
  const parkDate = `${today.year}-${String(today.month).padStart(2, '0')}-${String(today.day).padStart(2, '0')}`
  if (visit.date < parkDate) {
    throw new RangeError(`The visit date is in the past in ${timeZone}.`)
  }

  return {
    visitStartAt: parkLocalDateTimeToIso(visit.date, visit.arrivalTime, timeZone),
    visitEndAt: parkLocalDateTimeToIso(visit.date, visit.departureTime, timeZone),
    startingAttractionId: null,
    preferences: Object.entries(priorities)
      .map(([attractionId, level]) => ({
        attractionId: Number(attractionId),
        level: priorityMap[level],
      }))
      .sort((left, right) => left.attractionId - right.attractionId),
  }
}

export function orderItineraryStops(stops: readonly ItineraryStop[]): ItineraryStop[] {
  return [...stops].sort(
    (left, right) => left.sequence - right.sequence || left.attractionId - right.attractionId,
  )
}

export function attractionNameSnapshot(
  attractions: readonly CurrentWaitTime[],
): Readonly<Record<number, string>> {
  return Object.fromEntries(
    attractions.map((attraction) => [attraction.attractionId, attraction.attractionName]),
  )
}

function parseLocalDateTime(date: string, time: string): ZonedDateTimeParts {
  const dateMatch = /^(\d{4})-(\d{2})-(\d{2})$/.exec(date)
  const timeMatch = /^(\d{2}):(\d{2})$/.exec(time)
  if (!dateMatch || !timeMatch) throw new Error('Invalid local date or time.')

  const parts = {
    year: Number(dateMatch[1]),
    month: Number(dateMatch[2]),
    day: Number(dateMatch[3]),
    hour: Number(timeMatch[1]),
    minute: Number(timeMatch[2]),
    second: 0,
  }
  const timestamp = partsToTimestamp(parts)
  const validated = new Date(timestamp)
  if (
    parts.month < 1 ||
    parts.month > 12 ||
    parts.day < 1 ||
    parts.hour > 23 ||
    parts.minute > 59 ||
    validated.getUTCFullYear() !== parts.year ||
    validated.getUTCMonth() + 1 !== parts.month ||
    validated.getUTCDate() !== parts.day
  ) {
    throw new Error('Invalid local date or time.')
  }
  return parts
}

function zonedParts(instant: Date, timeZone: string): ZonedDateTimeParts {
  const values: Record<string, number> = {}
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(instant)

  for (const part of parts) {
    if (part.type !== 'literal') values[part.type] = Number(part.value)
  }
  return {
    year: values.year,
    month: values.month,
    day: values.day,
    hour: values.hour,
    minute: values.minute,
    second: values.second,
  }
}

function partsToTimestamp(parts: ZonedDateTimeParts): number {
  return Date.UTC(
    parts.year,
    parts.month - 1,
    parts.day,
    parts.hour,
    parts.minute,
    parts.second,
  )
}

function sameParts(left: ZonedDateTimeParts, right: ZonedDateTimeParts): boolean {
  return (
    left.year === right.year &&
    left.month === right.month &&
    left.day === right.day &&
    left.hour === right.hour &&
    left.minute === right.minute &&
    left.second === right.second
  )
}

function formatOffset(offsetMinutes: number): string {
  const sign = offsetMinutes >= 0 ? '+' : '-'
  const absolute = Math.abs(offsetMinutes)
  const hours = String(Math.floor(absolute / 60)).padStart(2, '0')
  const minutes = String(absolute % 60).padStart(2, '0')
  return `${sign}${hours}:${minutes}`
}
