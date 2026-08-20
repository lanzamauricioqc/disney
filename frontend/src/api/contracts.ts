export interface Park {
  id: number
  name: string
  timezone: string
}

export interface CurrentWaitTime {
  attractionId: number
  attractionName: string
  landId: number | null
  landName: string | null
  observedAt: string
  isOpen: boolean
  waitMinutes: number | null
}

export interface CurrentWaitTimesResult {
  parkId: number
  windowStart: string
  generatedAt: string
  attractions: CurrentWaitTime[]
}

export interface DailyWaitTime {
  attractionId: number
  attractionName: string
  localDate: string
  averageWaitMinutes: number
  minimumWaitMinutes: number
  maximumWaitMinutes: number
  observationCount: number
}

export interface DailyWaitTimeHistoryResult {
  parkId: number
  attractionId: number
  windowStart: string
  windowEnd: string
  history: DailyWaitTime[]
}

export interface WeekdayWaitTimePattern {
  attractionId: number
  attractionName: string
  dayOfWeek: string
  localHour: number
  localMinute: number
  averageWaitMinutes: number
  medianWaitMinutes: number
  minimumWaitMinutes: number
  maximumWaitMinutes: number
  observationCount: number
}

export interface WeekdayWaitTimePatternsResult {
  parkId: number
  windowStart: string
  windowEnd: string
  patterns: WeekdayWaitTimePattern[]
}

export interface AdminPark {
  id: number
  sourceParkId: number
  name: string
  timezone: string
  isActive: boolean
  collectionEnabled: boolean
  collectionIntervalMinutes: number
  lastCollectionStartedAt: string | null
  lastCollectionCompletedAt: string | null
  lastCollectionSucceeded: boolean | null
  lastCollectionError: string | null
  attractionCount: number
  observationCount: number
}

export interface AdminLand {
  id: number
  parkId: number
  sourceLandId: number
  name: string
  isActive: boolean
}

export interface AdminAttraction {
  id: number
  parkId: number
  currentLandId: number | null
  landName: string | null
  sourceRideId: number
  name: string
  isActive: boolean
  durationMinutes: number | null
  latitude: number | null
  longitude: number | null
}

export interface AdminCollectionRun {
  id: number
  parkId: number
  parkName: string
  startedAt: string
  completedAt: string | null
  success: boolean
  errorMessage: string | null
  triggerSource: 'scheduled' | 'manual' | 'retry'
  observationCount: number
}

export interface AdminObservation {
  id: number
  parkId: number
  parkName: string
  attractionId: number
  attractionName: string
  landId: number | null
  landName: string | null
  observedAt: string
  isOpen: boolean
  waitMinutes: number | null
  isValid: boolean
  invalidReason: string | null
  triggerSource: 'scheduled' | 'manual' | 'retry'
}

export interface SaveAdminPark {
  sourceParkId: number
  name: string
  timezone: string
  isActive: boolean
  collectionEnabled: boolean
  collectionIntervalMinutes: number
}

export interface SaveAdminLand {
  parkId: number
  sourceLandId: number
  name: string
  isActive: boolean
}

export interface SaveAdminAttraction {
  parkId: number
  currentLandId: number | null
  sourceRideId: number
  name: string
  isActive: boolean
  durationMinutes: number | null
  latitude: number | null
  longitude: number | null
}
