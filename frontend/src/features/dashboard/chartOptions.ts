import type { EChartsCoreOption } from 'echarts/core'
import type {
  DailyParkWaitTime,
  DailyWaitTime,
  WeekdayWaitTimePattern,
} from '../../api/contracts'
import type { Locale, Translate } from '../../i18n'

const parkColors = ['#146c70', '#b25e09', '#6941c6', '#c4320a', '#1570ef']

export function createDailyParkChartOption(
  weekStart: string,
  parks: DailyParkWaitTime[],
  locale: Locale,
  t: Translate,
): EChartsCoreOption {
  const dates = Array.from({ length: 7 }, (_, dayOffset) =>
    addDays(weekStart, dayOffset),
  )
  const parksById = new Map(
    parks.map((point) => [point.parkId, point.parkName]),
  )
  const parkNameCounts = new Map<string, number>()
  parksById.forEach((parkName) =>
    parkNameCounts.set(parkName, (parkNameCounts.get(parkName) ?? 0) + 1),
  )
  const parkSeries = [...parksById]
    .map(([parkId, parkName]) => ({
      parkId,
      name:
        parkNameCounts.get(parkName) === 1
          ? parkName
          : `${parkName} (#${parkId})`,
    }))
    .sort((left, right) => left.name.localeCompare(right.name, locale))
  const pointLookup = new Map(
    parks.map((point) => [`${point.parkId}:${point.localDate}`, point]),
  )

  return {
    animationDuration: 250,
    color: parkColors,
    tooltip: {
      trigger: 'axis',
      backgroundColor: '#101828',
      borderWidth: 0,
      textStyle: { color: '#ffffff' },
      formatter: (parameters: unknown) => {
        const points = parameters as Array<{
          axisValue: string
          marker: string
          seriesId: string
          seriesName: string
          value: number | null
        }>
        const date = points[0]?.axisValue
        if (!date) {
          return ''
        }
        const heading = formatChartDate(date, locale, {
          weekday: 'long',
          month: 'short',
          day: 'numeric',
        })
        const rows = points
          .filter((point) => point.value !== null)
          .map((point) => {
            const detail = pointLookup.get(`${point.seriesId}:${date}`)
            return `${point.marker}${point.seriesName}: <strong>${new Intl.NumberFormat(locale).format(point.value!)} ${t('min')}</strong>` +
              (detail
                ? `<br/><span style="padding-left:14px;color:#d0d5dd">${t('attractionsCount', { count: new Intl.NumberFormat(locale).format(detail.attractionCount) })} · ${t('samplesCount', { count: new Intl.NumberFormat(locale).format(detail.observationCount) })}</span>`
                : '')
          })
        return [heading, ...rows].join('<br/>')
      },
    },
    legend: {
      top: 0,
      icon: 'roundRect',
      itemHeight: 3,
      textStyle: { color: '#475467' },
    },
    grid: { top: 54, right: 28, bottom: 58, left: 58 },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: dates,
      axisLabel: {
        color: '#667085',
        formatter: (value: string) =>
          formatChartDate(value, locale, { weekday: 'short', month: 'short', day: 'numeric' }),
      },
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#d0d5dd' } },
    },
    yAxis: {
      type: 'value',
      name: t('averageMinutes'),
      min: 0,
      nameTextStyle: { color: '#667085' },
      axisLabel: { color: '#667085', formatter: (value: number) => new Intl.NumberFormat(locale).format(value) },
      axisLine: { show: false },
      splitLine: { lineStyle: { color: '#eaecf0' } },
    },
    series: parkSeries.map((park) => ({
      id: park.parkId.toString(),
      name: park.name,
      type: 'line',
      connectNulls: false,
      symbol: 'circle',
      symbolSize: 7,
      data: dates.map(
        (date) =>
          pointLookup.get(`${park.parkId}:${date}`)?.averageWaitMinutes ?? null,
      ),
      lineStyle: { width: 2.5 },
    })),
  }
}

const dayOrder = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
]

export function createHistoryChartOption(
  history: DailyWaitTime[],
  locale: Locale,
  t: Translate,
): EChartsCoreOption {
  const axisStyle = { color: '#667085' }

  return {
    animationDuration: 250,
    color: ['#146c70', '#b25e09'],
    tooltip: {
      trigger: 'axis',
      backgroundColor: '#101828',
      borderWidth: 0,
      textStyle: { color: '#ffffff' },
      formatter: (parameters: unknown) => {
        const points = parameters as Array<{ axisValue: string; marker: string; seriesName: string; value: number | null }>
        const date = points[0]?.axisValue
        if (!date) return ''
        const rows = points.filter((point) => point.value !== null).map((point) => `${point.marker}${point.seriesName}: <strong>${new Intl.NumberFormat(locale).format(point.value!)} ${t('min')}</strong>`)
        return [formatChartDate(date, locale, { month: 'short', day: 'numeric', year: 'numeric' }), ...rows].join('<br/>')
      },
    },
    legend: {
      top: 0,
      icon: 'roundRect',
      itemHeight: 3,
      textStyle: { color: '#475467' },
    },
    grid: { top: 44, right: 24, bottom: 44, left: 56 },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: history.map((point) => point.localDate),
      axisLabel: {
        ...axisStyle,
        formatter: (value: string) => formatChartDate(value, locale, { month: 'short', day: 'numeric' }),
      },
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#d0d5dd' } },
    },
    yAxis: {
      type: 'value',
      name: t('minutes'),
      nameTextStyle: axisStyle,
      axisLabel: { ...axisStyle, formatter: (value: number) => new Intl.NumberFormat(locale).format(value) },
      axisLine: { show: false },
      splitLine: { lineStyle: { color: '#eaecf0' } },
    },
    series: [
      {
        name: t('dailyAverage'),
        type: 'line',
        smooth: true,
        showSymbol: false,
        data: history.map((point) => point.averageWaitMinutes),
        lineStyle: { color: '#146c70', width: 2.5 },
        areaStyle: { color: 'rgba(20, 108, 112, 0.08)' },
      },
      {
        name: t('dailyMaximum'),
        type: 'line',
        showSymbol: false,
        data: history.map((point) => point.maximumWaitMinutes),
        lineStyle: { color: '#b25e09', width: 1.5, type: 'dashed' },
      },
    ],
  }
}

export function createPatternChartOption(
  patterns: WeekdayWaitTimePattern[],
  locale: Locale,
  t: Translate,
): EChartsCoreOption {
  const localizedDays = [t('sunday'), t('monday'), t('tuesday'), t('wednesday'), t('thursday'), t('friday'), t('saturday')]
  const times = [...new Set(patterns.map(formatPatternTime))].sort()
  const localizedTimes = times.map((time) => formatChartTime(time, locale))
  const maximumWait = Math.max(
    1,
    ...patterns.map((pattern) => pattern.averageWaitMinutes),
  )
  const data = patterns.map((pattern) => [
    times.indexOf(formatPatternTime(pattern)),
    dayOrder.indexOf(pattern.dayOfWeek),
    pattern.averageWaitMinutes,
  ])

  return {
    tooltip: {
      position: 'top',
      backgroundColor: '#101828',
      borderWidth: 0,
      textStyle: { color: '#ffffff' },
      formatter: (parameters: unknown) => {
        const value = (parameters as { value: [number, number, number] }).value
        return `${localizedDays[value[1]]} ${localizedTimes[value[0]]}<br/><strong>${new Intl.NumberFormat(locale).format(value[2])} ${t('min')}</strong>`
      },
    },
    grid: { top: 12, right: 22, bottom: 76, left: 78 },
    xAxis: {
      type: 'category',
      data: localizedTimes,
      axisLabel: { color: '#667085', interval: Math.max(0, Math.floor(times.length / 8)) },
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#d0d5dd' } },
    },
    yAxis: {
      type: 'category',
      data: localizedDays,
      axisLabel: { color: '#667085' },
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#d0d5dd' } },
    },
    visualMap: {
      min: 0,
      max: maximumWait,
      calculable: true,
      orient: 'horizontal',
      left: 'center',
      bottom: 0,
      textStyle: { color: '#475467' },
      formatter: (value: number) => new Intl.NumberFormat(locale).format(value),
      inRange: { color: ['#e8f3f2', '#8bc7c3', '#f3c98b', '#c4543d'] },
    },
    series: [
      {
        name: t('averageWait'),
        type: 'heatmap',
        data,
        emphasis: {
          itemStyle: {
            borderColor: '#101828',
            borderWidth: 1,
          },
        },
      },
    ],
  }
}

function formatPatternTime(pattern: WeekdayWaitTimePattern) {
  return `${pattern.localHour.toString().padStart(2, '0')}:${pattern.localMinute
    .toString()
    .padStart(2, '0')}`
}

function addDays(date: string, days: number) {
  const parsedDate = new Date(`${date}T12:00:00Z`)
  parsedDate.setUTCDate(parsedDate.getUTCDate() + days)
  return parsedDate.toISOString().slice(0, 10)
}

function formatChartDate(
  date: string,
  locale: Locale,
  options: Intl.DateTimeFormatOptions,
) {
  return new Intl.DateTimeFormat(locale, {
    ...options,
    timeZone: 'UTC',
  }).format(new Date(`${date}T12:00:00Z`))
}

function formatChartTime(time: string, locale: Locale) {
  const [hour, minute] = time.split(':').map(Number)
  return new Intl.DateTimeFormat(locale, { hour: 'numeric', minute: '2-digit', timeZone: 'UTC' }).format(new Date(Date.UTC(2000, 0, 1, hour, minute)))
}
