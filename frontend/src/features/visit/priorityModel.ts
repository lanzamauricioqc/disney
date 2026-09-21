import type { CurrentWaitTime } from '../../api/contracts'

export const attractionPriorities = ['must-do', 'would-like', 'skip'] as const

export type AttractionPriority = (typeof attractionPriorities)[number]
export type AttractionPriorities = Readonly<Record<number, AttractionPriority>>
export type PrioritySelectionsByPark = Readonly<Record<number, AttractionPriorities>>

export type RecommendationReason = 'known-live-wait' | 'open-without-wait'

export interface AttractionRecommendation {
  attraction: CurrentWaitTime
  reason: RecommendationReason
}

export interface PriorityCounts {
  mustDo: number
  wouldLike: number
  skip: number
  unselected: number
}

export function updateAttractionPriority(
  current: AttractionPriorities,
  attractionId: number,
  priority: AttractionPriority | null,
): AttractionPriorities {
  const next = { ...current }

  if (priority === null) {
    delete next[attractionId]
  } else {
    next[attractionId] = priority
  }

  return next
}

export function updatePriorityForPark(
  current: PrioritySelectionsByPark,
  parkId: number,
  attractionId: number,
  priority: AttractionPriority | null,
): PrioritySelectionsByPark {
  return {
    ...current,
    [parkId]: updateAttractionPriority(current[parkId] ?? {}, attractionId, priority),
  }
}

export function countPriorities(
  priorities: AttractionPriorities,
  attractionCount: number,
): PriorityCounts {
  const values = Object.values(priorities)

  return {
    mustDo: values.filter((priority) => priority === 'must-do').length,
    wouldLike: values.filter((priority) => priority === 'would-like').length,
    skip: values.filter((priority) => priority === 'skip').length,
    unselected: Math.max(0, attractionCount - values.length),
  }
}

/**
 * Suggestions are deliberately deterministic: only open, unselected attractions
 * qualify; known live waits come first from shortest to longest, followed by
 * attractions without a wait observation. Stable name/id tie-breakers make the
 * same catalog snapshot produce the same recommendations every time.
 */
export function recommendUnselectedAttractions(
  attractions: readonly CurrentWaitTime[],
  priorities: AttractionPriorities,
  limit = 3,
): AttractionRecommendation[] {
  if (limit <= 0) return []

  return attractions
    .filter(
      (attraction) =>
        attraction.isOpen && priorities[attraction.attractionId] === undefined,
    )
    .sort(compareRecommendationCandidates)
    .slice(0, limit)
    .map((attraction) => ({
      attraction,
      reason:
        attraction.waitMinutes === null
          ? 'open-without-wait'
          : 'known-live-wait',
    }))
}

function compareRecommendationCandidates(
  left: CurrentWaitTime,
  right: CurrentWaitTime,
): number {
  const leftHasWait = left.waitMinutes !== null
  const rightHasWait = right.waitMinutes !== null

  if (leftHasWait !== rightHasWait) return leftHasWait ? -1 : 1
  if (left.waitMinutes !== right.waitMinutes) {
    return (left.waitMinutes ?? 0) - (right.waitMinutes ?? 0)
  }

  const leftName = left.attractionName.toLocaleLowerCase('en')
  const rightName = right.attractionName.toLocaleLowerCase('en')
  if (leftName < rightName) return -1
  if (leftName > rightName) return 1
  return left.attractionId - right.attractionId
}