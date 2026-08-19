import { describe, expect, it } from 'vitest'
import { formatWindow } from './formatters'

describe('formatWindow', () => {
  it('formats both limits of the analytics window', () => {
    const result = formatWindow(
      '2026-05-19T00:00:00Z',
      '2026-08-19T00:00:00Z',
    )

    expect(result).toContain('2026')
    expect(result).toContain(' - ')
  })
})
