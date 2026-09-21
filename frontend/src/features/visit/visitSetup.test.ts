import { describe, expect, it } from 'vitest'
import {
  MAX_PARTY_SIZE,
  MIN_PARTY_SIZE,
  toLocalDateInputValue,
  validateVisitSetup,
  type VisitSetupValues,
} from './visitSetupModel'

const validVisit: VisitSetupValues = {
  date: '2026-09-22',
  arrivalTime: '09:00',
  departureTime: '18:00',
  partySize: '4',
}
const today = new Date(2026, 8, 21, 8, 30)

describe('validateVisitSetup', () => {
  it('accepts a complete visit on a future date', () => {
    expect(validateVisitSetup(validVisit, today)).toEqual({})
  })

  it('allows a visit later today', () => {
    expect(validateVisitSetup({ ...validVisit, date: '2026-09-21' }, today)).toEqual({})
  })

  it('rejects past and impossible dates', () => {
    expect(validateVisitSetup({ ...validVisit, date: '2026-09-20' }, today).date).toBe('datePast')
    expect(validateVisitSetup({ ...validVisit, date: '2026-02-30' }, today).date).toBe('dateInvalid')
  })

  it('requires departure to be later than arrival', () => {
    expect(validateVisitSetup({ ...validVisit, departureTime: '09:00' }, today).departureTime).toBe('departureAfterArrival')
    expect(validateVisitSetup({ ...validVisit, departureTime: '08:45' }, today).departureTime).toBe('departureAfterArrival')
  })

  it('enforces whole-number party-size bounds', () => {
    expect(validateVisitSetup({ ...validVisit, partySize: String(MIN_PARTY_SIZE - 1) }, today).partySize).toBe('partyRange')
    expect(validateVisitSetup({ ...validVisit, partySize: String(MAX_PARTY_SIZE + 1) }, today).partySize).toBe('partyRange')
    expect(validateVisitSetup({ ...validVisit, partySize: '2.5' }, today).partySize).toBe('partyWholeNumber')
  })
})

describe('toLocalDateInputValue', () => {
  it('uses the browser local calendar date rather than UTC', () => {
    expect(toLocalDateInputValue(new Date(2026, 0, 5, 23, 30))).toBe('2026-01-05')
  })
})
