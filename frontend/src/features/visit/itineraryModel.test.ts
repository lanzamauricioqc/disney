import { describe, expect, it } from 'vitest'
import type { ItineraryStop } from '../../api/contracts'
import {
  buildOptimizeItineraryRequest,
  orderItineraryStops,
  parkLocalDateTimeToIso,
} from './itineraryModel'

describe('parkLocalDateTimeToIso', () => {
  it('uses the park IANA timezone and its seasonal DST offset', () => {
    expect(
      parkLocalDateTimeToIso('2026-01-15', '09:30', 'America/New_York'),
    ).toBe('2026-01-15T09:30:00-05:00')
    expect(
      parkLocalDateTimeToIso('2026-07-15', '09:30', 'America/New_York'),
    ).toBe('2026-07-15T09:30:00-04:00')
  })

  it('uses the offset on each side of a DST transition', () => {
    expect(
      parkLocalDateTimeToIso('2026-03-08', '01:30', 'America/New_York'),
    ).toBe('2026-03-08T01:30:00-05:00')
    expect(
      parkLocalDateTimeToIso('2026-03-08', '03:30', 'America/New_York'),
    ).toBe('2026-03-08T03:30:00-04:00')
  })

  it('rejects a wall-clock time skipped by the spring DST transition', () => {
    expect(() =>
      parkLocalDateTimeToIso('2026-03-08', '02:30', 'America/New_York'),
    ).toThrow(/does not exist/)
  })

  it('rejects a wall-clock time repeated by the fall DST transition', () => {
    expect(() =>
      parkLocalDateTimeToIso('2026-11-01', '01:30', 'America/New_York'),
    ).toThrow(/ambiguous/)
  })
})

describe('buildOptimizeItineraryRequest', () => {
  it('maps visit details and park-local priorities to the backend contract', () => {
    expect(
      buildOptimizeItineraryRequest(
        {
          date: '2026-09-22',
          arrivalTime: '09:00',
          departureTime: '18:15',
          partySize: 4,
        },
        'America/New_York',
        { 42: 'skip', 7: 'must-do', 18: 'would-like' },
        new Date('2026-01-01T00:00:00Z'),
      ),
    ).toEqual({
      visitStartAt: '2026-09-22T09:00:00-04:00',
      visitEndAt: '2026-09-22T18:15:00-04:00',
      startingAttractionId: null,
      preferences: [
        { attractionId: 7, level: 'MustDo' },
        { attractionId: 18, level: 'WouldLike' },
        { attractionId: 42, level: 'Skip' },
      ],
    })
  })

  it('rejects a date that has already passed in the park timezone', () => {
    expect(() =>
      buildOptimizeItineraryRequest(
        {
          date: '2026-09-21',
          arrivalTime: '09:00',
          departureTime: '18:00',
          partySize: 2,
        },
        'America/New_York',
        { 7: 'must-do' },
        new Date('2026-09-22T04:30:00Z'),
      ),
    ).toThrow(/past/)
  })
})

describe('orderItineraryStops', () => {
  it('orders a defensive copy by sequence', () => {
    const stop = (sequence: number, attractionId: number): ItineraryStop => ({
      sequence,
      attractionId,
      attractionName: `Attraction ${attractionId}`,
      preference: 'MustDo',
      travelStartsAt: '2026-09-22T09:00:00-04:00',
      walkingMinutes: 5,
      queueStartsAt: '2026-09-22T09:05:00-04:00',
      queueMinutes: 10,
      attractionStartsAt: '2026-09-22T09:15:00-04:00',
      attractionDurationMinutes: 5,
      completesAt: '2026-09-22T09:20:00-04:00',
    })
    const input = [stop(2, 20), stop(1, 10)]

    expect(orderItineraryStops(input).map((item) => item.attractionId)).toEqual([10, 20])
    expect(input.map((item) => item.attractionId)).toEqual([20, 10])
  })
})
