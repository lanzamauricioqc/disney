import { describe, expect, it } from 'vitest'
import type { CurrentWaitTime } from '../../api/contracts'
import {
  countPriorities,
  recommendUnselectedAttractions,
  updateAttractionPriority,
  updatePriorityForPark,
  type AttractionPriorities,
} from './priorityModel'

function attraction(
  attractionId: number,
  attractionName: string,
  waitMinutes: number | null,
  isOpen = true,
): CurrentWaitTime {
  return {
    attractionId,
    attractionName,
    landId: 1,
    landName: 'Test Land',
    observedAt: '2026-09-21T12:00:00Z',
    isOpen,
    waitMinutes,
  }
}

describe('attraction priority state', () => {
  it('keeps exactly one mutually exclusive priority per attraction', () => {
    const mustDo = updateAttractionPriority({}, 10, 'must-do')
    const changed = updateAttractionPriority(mustDo, 10, 'skip')

    expect(mustDo).toEqual({ 10: 'must-do' })
    expect(changed).toEqual({ 10: 'skip' })
  })

  it('can return an attraction to unselected without mutating prior state', () => {
    const original: AttractionPriorities = { 10: 'would-like', 20: 'must-do' }
    const changed = updateAttractionPriority(original, 10, null)

    expect(changed).toEqual({ 20: 'must-do' })
    expect(original).toEqual({ 10: 'would-like', 20: 'must-do' })
  })

  it('preserves separate choices when the visitor changes parks', () => {
    const firstPark = updatePriorityForPark({}, 1, 10, 'must-do')
    const secondPark = updatePriorityForPark(firstPark, 2, 20, 'skip')

    expect(secondPark[1]).toEqual({ 10: 'must-do' })
    expect(secondPark[2]).toEqual({ 20: 'skip' })
  })

  it('counts saved and unselected catalog attractions', () => {
    expect(
      countPriorities({ 1: 'must-do', 2: 'would-like', 3: 'skip' }, 5),
    ).toEqual({ mustDo: 1, wouldLike: 1, skip: 1, unselected: 2 })
  })
})

describe('unselected attraction recommendations', () => {
  const catalog = [
    attraction(1, 'Long Queue', 50),
    attraction(2, 'Zulu Walk-on', 10),
    attraction(3, 'Alpha Walk-on', 10),
    attraction(4, 'No Wait Data', null),
    attraction(5, 'Closed Ride', 0, false),
    attraction(6, 'Already Chosen', 5),
  ]

  it('suggests only open, unselected attractions using explainable live-wait order', () => {
    const result = recommendUnselectedAttractions(catalog, { 6: 'would-like' }, 10)

    expect(result.map(({ attraction: item }) => item.attractionId)).toEqual([3, 2, 1, 4])
    expect(result.map(({ reason }) => reason)).toEqual([
      'known-live-wait',
      'known-live-wait',
      'known-live-wait',
      'open-without-wait',
    ])
  })

  it('excludes every kind of saved user choice, including Skip', () => {
    const result = recommendUnselectedAttractions(catalog, {
      1: 'skip',
      2: 'must-do',
      3: 'would-like',
      6: 'would-like',
    })

    expect(result.map(({ attraction: item }) => item.attractionId)).toEqual([4])
  })

  it('is deterministic and respects the result limit', () => {
    const first = recommendUnselectedAttractions(catalog, {}, 2)
    const second = recommendUnselectedAttractions([...catalog].reverse(), {}, 2)

    expect(first.map(({ attraction: item }) => item.attractionId)).toEqual([6, 3])
    expect(second).toEqual(first)
  })
})