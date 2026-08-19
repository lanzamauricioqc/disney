import type { EChartsCoreOption } from 'echarts/core'
import type {
  DailyWaitTime,
  WeekdayWaitTimePattern,
} from '../../api/contracts'

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
      axisLabel: axisStyle,
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#d0d5dd' } },
    },
    yAxis: {
      type: 'value',
      name: 'Minutes',
      nameTextStyle: axisStyle,
      axisLabel: axisStyle,
      axisLine: { show: false },
      splitLine: { lineStyle: { color: '#eaecf0' } },
    },
    series: [
      {
        name: 'Daily average',
        type: 'line',
        smooth: true,
        showSymbol: false,
        data: history.map((point) => point.averageWaitMinutes),
        lineStyle: { color: '#146c70', width: 2.5 },
        areaStyle: { color: 'rgba(20, 108, 112, 0.08)' },
      },
      {
        name: 'Daily maximum',
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
): EChartsCoreOption {
  const times = [...new Set(patterns.map(formatPatternTime))].sort()
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
        return `${dayOrder[value[1]]} ${times[value[0]]}<br/><strong>${value[2]} min</strong>`
      },
    },
    grid: { top: 12, right: 22, bottom: 76, left: 78 },
    xAxis: {
      type: 'category',
      data: times,
      axisLabel: { color: '#667085', interval: Math.max(0, Math.floor(times.length / 8)) },
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#d0d5dd' } },
    },
    yAxis: {
      type: 'category',
      data: dayOrder,
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
      inRange: { color: ['#e8f3f2', '#8bc7c3', '#f3c98b', '#c4543d'] },
    },
    series: [
      {
        name: 'Average wait',
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
