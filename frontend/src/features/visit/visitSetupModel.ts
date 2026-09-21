export const MIN_PARTY_SIZE = 1
export const MAX_PARTY_SIZE = 20

export interface VisitSetupValues {
  date: string
  arrivalTime: string
  departureTime: string
  partySize: string
}

export interface VisitDetails {
  date: string
  arrivalTime: string
  departureTime: string
  partySize: number
}

export type VisitSetupField = keyof VisitSetupValues

export type VisitSetupError =
  | 'dateRequired'
  | 'dateInvalid'
  | 'datePast'
  | 'arrivalRequired'
  | 'arrivalInvalid'
  | 'departureRequired'
  | 'departureInvalid'
  | 'departureAfterArrival'
  | 'partyRequired'
  | 'partyWholeNumber'
  | 'partyRange'

export type VisitSetupErrors = Partial<Record<VisitSetupField, VisitSetupError>>

const DATE_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/
const TIME_PATTERN = /^(\d{2}):(\d{2})$/

export function toLocalDateInputValue(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function isValidDate(value: string): boolean {
  const match = DATE_PATTERN.exec(value)
  if (!match) return false

  const year = Number(match[1])
  const month = Number(match[2])
  const day = Number(match[3])
  const date = new Date(year, month - 1, day)

  return (
    date.getFullYear() === year &&
    date.getMonth() === month - 1 &&
    date.getDate() === day
  )
}

export function timeToMinutes(value: string): number | null {
  const match = TIME_PATTERN.exec(value)
  if (!match) return null

  const hours = Number(match[1])
  const minutes = Number(match[2])
  if (hours > 23 || minutes > 59) return null
  return hours * 60 + minutes
}

export function validateVisitSetup(
  values: VisitSetupValues,
  today = new Date(),
): VisitSetupErrors {
  const errors: VisitSetupErrors = {}
  const todayValue = toLocalDateInputValue(today)

  if (!values.date) errors.date = 'dateRequired'
  else if (!isValidDate(values.date)) errors.date = 'dateInvalid'
  else if (values.date < todayValue) errors.date = 'datePast'

  const arrivalMinutes = timeToMinutes(values.arrivalTime)
  if (!values.arrivalTime) errors.arrivalTime = 'arrivalRequired'
  else if (arrivalMinutes === null) errors.arrivalTime = 'arrivalInvalid'

  const departureMinutes = timeToMinutes(values.departureTime)
  if (!values.departureTime) errors.departureTime = 'departureRequired'
  else if (departureMinutes === null) errors.departureTime = 'departureInvalid'
  else if (arrivalMinutes !== null && departureMinutes <= arrivalMinutes) {
    errors.departureTime = 'departureAfterArrival'
  }

  if (!values.partySize) errors.partySize = 'partyRequired'
  else {
    const partySize = Number(values.partySize)
    if (!Number.isInteger(partySize)) errors.partySize = 'partyWholeNumber'
    else if (partySize < MIN_PARTY_SIZE || partySize > MAX_PARTY_SIZE) {
      errors.partySize = 'partyRange'
    }
  }

  return errors
}

export function toVisitDetails(values: VisitSetupValues): VisitDetails {
  return {
    date: values.date,
    arrivalTime: values.arrivalTime,
    departureTime: values.departureTime,
    partySize: Number(values.partySize),
  }
}
