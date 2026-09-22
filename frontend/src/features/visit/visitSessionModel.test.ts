import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  clearVisitSessionId,
  readVisitSessionId,
  saveVisitSessionId,
} from './visitSessionModel'

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
