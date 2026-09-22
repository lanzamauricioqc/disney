import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  clearVisitSessionId,
  getFreshCurrentWaitForStop,
  getLastCompletedStop,
  getNextPendingStop,
  readVisitSessionId,
  saveVisitSessionId,
} from './visitSessionModel'
import type {
  CurrentWaitTime,
  VisitSession,
  VisitSessionStop,
} from '../../api/contracts'

const storage = {
  clear: vi.fn(),
  getItem: vi.fn<(key: string) => string | null>(),
  key: vi.fn<(index: number) => string | null>(),
  length: 0,
  removeItem: vi.fn<(key: string) => void>(),
  setItem: vi.fn<(key: string, value: string) => void>(),
}

Object.defineProperty(globalThis, 'localStorage', {
  configurable: true,
  value: storage,
})

describe('visit session storage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('next visit action', () => {
    it('selects the earliest pending stop', () => {
      const session = createSession([
        createStop(3, 103, 'Pending'),
        createStop(1, 101, 'Completed'),
        createStop(2, 102, 'Pending'),
      ])

      expect(getNextPendingStop(session)?.attractionId).toBe(102)
    })

    it('returns no next action after every stop is resolved', () => {
      const session = createSession([
        createStop(1, 101, 'Completed'),
        createStop(2, 102, 'Skipped'),
      ])

      expect(getNextPendingStop(session)).toBeNull()
    })

    it('matches the live wait by attraction ID', () => {
      const stop = createStop(1, 101, 'Pending')
      const waits: CurrentWaitTime[] = [
        {
          attractionId: 102,
          attractionName: 'Other attraction',
          landId: null,
          landName: null,
          observedAt: '2026-09-22T13:00:00Z',
          isOpen: true,
          waitMinutes: 10,
        },
        {
          attractionId: 101,
          attractionName: 'Next attraction',
          landId: null,
          landName: null,
          observedAt: '2026-09-22T13:00:00Z',
          isOpen: true,
          waitMinutes: 25,
        },
      ]

      const now = new Date('2026-09-22T13:10:00Z')
      expect(getFreshCurrentWaitForStop(waits, stop, now)?.waitMinutes).toBe(25)
      expect(getFreshCurrentWaitForStop([], stop, now)).toBeNull()
    })

    it('rejects stale or invalid observations', () => {
      const stop = createStop(1, 101, 'Pending')
      const wait: CurrentWaitTime = {
        attractionId: 101,
        attractionName: 'Next attraction',
        landId: null,
        landName: null,
        observedAt: '2026-09-22T12:30:00Z',
        isOpen: true,
        waitMinutes: 25,
      }
      const now = new Date('2026-09-22T13:00:00Z')

      expect(getFreshCurrentWaitForStop([wait], stop, now)).toBeNull()
      expect(getFreshCurrentWaitForStop(
        [{ ...wait, observedAt: 'invalid' }],
        stop,
        now,
      )).toBeNull()
      expect(getFreshCurrentWaitForStop(
        [{ ...wait, observedAt: '2026-09-22T13:10:00Z' }],
        stop,
        now,
      )).toBeNull()
    })

    it('selects the latest completed location', () => {
      const session = createSession([
        createStop(1, 101, 'Completed'),
        createStop(2, 102, 'Skipped'),
        createStop(3, 103, 'Completed'),
        createStop(4, 104, 'Pending'),
      ])

      expect(getLastCompletedStop(session)?.attractionId).toBe(103)
      expect(getLastCompletedStop(createSession([
        createStop(1, 101, 'Pending'),
      ]))).toBeNull()
    })
  })

  function createSession(stops: VisitSessionStop[]): VisitSession {
    return {
      id: '23f2af33-3f18-4d10-8ca4-a649f0cd60c1',
      parkId: 1,
      visitStartAt: '2026-09-22T13:00:00Z',
      visitEndAt: '2026-09-22T21:00:00Z',
      partySize: 2,
      startedAt: '2026-09-22T13:00:00Z',
      updatedAt: '2026-09-22T13:00:00Z',
      status: 'Active',
      stops,
      totalWalkingMinutes: 10,
      totalQueueMinutes: 20,
      totalAttractionMinutes: 30,
      algorithmVersion: 'test',
    }
  }

  function createStop(
    sequence: number,
    attractionId: number,
    status: VisitSessionStop['status'],
  ): VisitSessionStop {
    return {
      sequence,
      attractionId,
      attractionName: `Attraction ${attractionId}`,
      preference: 'MustDo',
      travelStartsAt: '2026-09-22T13:00:00Z',
      walkingMinutes: 5,
      queueStartsAt: '2026-09-22T13:05:00Z',
      queueMinutes: 10,
      attractionStartsAt: '2026-09-22T13:15:00Z',
      attractionDurationMinutes: 10,
      completesAt: '2026-09-22T13:25:00Z',
      status,
      statusChangedAt: status === 'Pending' ? null : '2026-09-22T13:30:00Z',
    }
  }

  it('reads only a valid persisted session ID', () => {
    storage.getItem.mockReturnValueOnce('23f2af33-3f18-4d10-8ca4-a649f0cd60c1')
    expect(readVisitSessionId()).toBe('23f2af33-3f18-4d10-8ca4-a649f0cd60c1')

    storage.getItem.mockReturnValueOnce('not-a-session')
    expect(readVisitSessionId()).toBeNull()
  })

  it('saves and clears the active session ID', () => {
    const sessionId = '23f2af33-3f18-4d10-8ca4-a649f0cd60c1'

    saveVisitSessionId(sessionId)
    clearVisitSessionId()

    expect(storage.setItem).toHaveBeenCalledWith(
      'park-pilot-visit-session-id',
      sessionId,
    )
    expect(storage.removeItem).toHaveBeenCalledWith(
      'park-pilot-visit-session-id',
    )
  })

  it('rejects an invalid session ID', () => {
    expect(() => saveVisitSessionId('invalid')).toThrow(/invalid/)
    expect(storage.setItem).not.toHaveBeenCalled()
  })
})
