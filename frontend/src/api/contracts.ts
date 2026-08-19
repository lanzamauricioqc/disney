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
