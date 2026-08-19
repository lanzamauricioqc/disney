import { useEffect, useRef } from 'react'
import * as echarts from 'echarts/core'
import {
  GridComponent,
  LegendComponent,
  TooltipComponent,
  VisualMapComponent,
} from 'echarts/components'
import { HeatmapChart, LineChart } from 'echarts/charts'
import { CanvasRenderer } from 'echarts/renderers'
import type { EChartsCoreOption } from 'echarts/core'

echarts.use([
  CanvasRenderer,
  GridComponent,
  HeatmapChart,
  LegendComponent,
  LineChart,
  TooltipComponent,
  VisualMapComponent,
])

interface EChartProps {
  ariaLabel: string
  option: EChartsCoreOption
}

export function EChart({ ariaLabel, option }: EChartProps) {
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!containerRef.current) {
      return
    }

    const chart = echarts.init(containerRef.current, undefined, {
      renderer: 'canvas',
    })
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    chart.setOption(reduceMotion ? { ...option, animation: false } : option)

    const resizeObserver = new ResizeObserver(() => chart.resize())
    resizeObserver.observe(containerRef.current)

    return () => {
      resizeObserver.disconnect()
      chart.dispose()
    }
  }, [option])

  return <div ref={containerRef} className="chart" role="img" aria-label={ariaLabel} />
}
