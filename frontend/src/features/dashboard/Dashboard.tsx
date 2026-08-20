import { lazy, Suspense, useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  getCurrentWaitTimes,
  getDailyWaitTimeHistory,
  getParks,
  getWeekdayWaitTimePatterns,
} from '../../api/client'
import type { CurrentWaitTime } from '../../api/contracts'
import { createHistoryChartOption, createPatternChartOption } from './chartOptions'
import { formatObservedAt, formatWindow } from './formatters'

const EChart = lazy(() =>
  import('../../components/EChart').then((module) => ({ default: module.EChart })),
)

export function Dashboard() {
  const [selectedParkId, setSelectedParkId] = useState<number>()
  const [selectedAttractionId, setSelectedAttractionId] = useState<number>()
  const [selectedLand, setSelectedLand] = useState('all')
  const [attractionNameFilter, setAttractionNameFilter] = useState('')

  const parksQuery = useQuery({
    queryKey: ['parks'],
    queryFn: ({ signal }) => getParks(signal),
    staleTime: 30 * 60_000,
  })

  useEffect(() => {
    if (!selectedParkId && parksQuery.data?.length) {
      setSelectedParkId(parksQuery.data[0].id)
    }
  }, [parksQuery.data, selectedParkId])

  const currentWaitsQuery = useQuery({
    queryKey: ['current-waits', selectedParkId],
    queryFn: ({ signal }) => getCurrentWaitTimes(selectedParkId!, signal),
    enabled: selectedParkId !== undefined,
    refetchInterval: 30_000,
  })

  const attractions = useMemo(
    () =>
      [...(currentWaitsQuery.data?.attractions ?? [])].sort(
        (left, right) =>
          (right.waitMinutes ?? -1) - (left.waitMinutes ?? -1),
      ),
    [currentWaitsQuery.data],
  )

  const landOptions = useMemo(() => {
    const lands = new Map<string, string>()

    attractions.forEach((attraction) => {
      lands.set(getLandFilterValue(attraction), attraction.landName ?? 'Park-wide')
    })

    return [...lands.entries()].sort((left, right) =>
      left[1].localeCompare(right[1]),
    )
  }, [attractions])

  const filteredAttractions = useMemo(() => {
    const normalizedNameFilter = attractionNameFilter.trim().toLocaleLowerCase()

    return attractions.filter((attraction) => {
      const matchesLand =
        selectedLand === 'all' || getLandFilterValue(attraction) === selectedLand
      const matchesName =
        !normalizedNameFilter ||
        attraction.attractionName.toLocaleLowerCase().includes(normalizedNameFilter)

      return matchesLand && matchesName
    })
  }, [attractionNameFilter, attractions, selectedLand])

  useEffect(() => {
    if (!filteredAttractions.length) {
      setSelectedAttractionId(undefined)
      return
    }

    const selectionExists = filteredAttractions.some(
      (attraction) => attraction.attractionId === selectedAttractionId,
    )
    if (!selectionExists) {
      setSelectedAttractionId(filteredAttractions[0].attractionId)
    }
  }, [filteredAttractions, selectedAttractionId])

  const historyQuery = useQuery({
    queryKey: ['daily-history', selectedParkId, selectedAttractionId],
    queryFn: ({ signal }) =>
      getDailyWaitTimeHistory(selectedParkId!, selectedAttractionId!, signal),
    enabled: selectedParkId !== undefined && selectedAttractionId !== undefined,
    staleTime: 10 * 60_000,
  })

  const patternsQuery = useQuery({
    queryKey: ['weekday-patterns', selectedParkId, selectedAttractionId],
    queryFn: ({ signal }) =>
      getWeekdayWaitTimePatterns(selectedParkId!, selectedAttractionId!, signal),
    enabled: selectedParkId !== undefined && selectedAttractionId !== undefined,
    staleTime: 10 * 60_000,
  })

  const selectedPark = parksQuery.data?.find((park) => park.id === selectedParkId)
  const selectedAttraction = attractions.find(
    (attraction) => attraction.attractionId === selectedAttractionId,
  )
  const openCount = attractions.filter((attraction) => attraction.isOpen).length
  const averageCurrentWait = calculateAverageCurrentWait(attractions)

  if (parksQuery.isLoading) {
    return <StatusScreen message="Loading park intelligence..." />
  }

  if (parksQuery.isError) {
    return <StatusScreen message="The park catalog is unavailable." error />
  }

  return (
    <div className="application-shell">
      <header className="topbar">
        <a className="brand" href="/" aria-label="Park Queue Intelligence home">
          <BrandMark />
          <span>
            <strong>Queue Intelligence</strong>
            <small>Park operations</small>
          </span>
        </a>
        <div className="topbar-status">
          <span className="status-dot" aria-hidden="true" />
          Data service online
        </div>
      </header>

      <main>
        <header className="page-header">
          <div>
            <p className="eyebrow">Live operations</p>
            <h1>Attraction wait times</h1>
            <p className="page-description">
              Monitor current queues and compare three months of historical patterns.
            </p>
          </div>
          <label className="park-selector" htmlFor="park-selector">
            <span>Viewing park</span>
            <select
              id="park-selector"
              value={selectedParkId ?? ''}
              onChange={(event) => {
                setSelectedParkId(Number(event.target.value))
                setSelectedAttractionId(undefined)
                setSelectedLand('all')
                setAttractionNameFilter('')
              }}
            >
              {parksQuery.data?.map((park) => (
                <option key={park.id} value={park.id}>
                  {park.name}
                </option>
              ))}
            </select>
          </label>
        </header>

        <section className="summary-bar" aria-label="Park summary">
          <Metric label="Tracked attractions" value={attractions.length.toString()} />
          <Metric label="Currently open" value={openCount.toString()} accent />
          <Metric
            label="Average current wait"
            value={averageCurrentWait === null ? '--' : `${averageCurrentWait} min`}
          />
          <Metric label="Park timezone" value={selectedPark?.timezone ?? '--'} compact />
        </section>

        <section className="dashboard-grid">
          <article className="surface queue-panel">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Current conditions</p>
                <h2>Attraction queues</h2>
              </div>
              <div className="live-status">
                <span className="live-indicator">Live</span>
                {currentWaitsQuery.data && (
                  <span className="updated-at">
                    Updated {formatObservedAt(currentWaitsQuery.data.generatedAt)}
                  </span>
                )}
              </div>
            </div>
            <div className="queue-filters" aria-label="Filter attraction queues">
              <label>
                <span>Land</span>
                <select
                  aria-label="Filter attractions by land"
                  value={selectedLand}
                  onChange={(event) => setSelectedLand(event.target.value)}
                >
                  <option value="all">All lands</option>
                  {landOptions.map(([value, name]) => (
                    <option key={value} value={value}>
                      {name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>Attraction name</span>
                <input
                  aria-label="Filter attractions by name"
                  onChange={(event) => setAttractionNameFilter(event.target.value)}
                  placeholder="Search attractions"
                  type="search"
                  value={attractionNameFilter}
                />
              </label>
            </div>
            <div className="queue-column-labels" aria-hidden="true">
              <span>Attraction</span>
              <span>Wait</span>
            </div>

            {currentWaitsQuery.isLoading && <QueueSkeleton />}
            {currentWaitsQuery.isError && (
              <InlineStatus message="Current waits could not be loaded." error />
            )}
            {!currentWaitsQuery.isLoading &&
              !currentWaitsQuery.isError &&
              attractions.length === 0 && (
                <InlineStatus message="No attraction observations are available yet." />
              )}
            {!currentWaitsQuery.isLoading &&
              !currentWaitsQuery.isError &&
              attractions.length > 0 &&
              filteredAttractions.length === 0 && (
                <InlineStatus message="No attractions match the selected filters." />
              )}
            <div className="queue-list">
              {filteredAttractions.map((attraction) => (
                <button
                  aria-pressed={attraction.attractionId === selectedAttractionId}
                  className={
                    attraction.attractionId === selectedAttractionId
                      ? 'queue-row selected'
                      : 'queue-row'
                  }
                  key={attraction.attractionId}
                  onClick={() => setSelectedAttractionId(attraction.attractionId)}
                  type="button"
                >
                  <span className="queue-copy">
                    <strong>{attraction.attractionName}</strong>
                    <small>
                      {attraction.landName ?? 'Park-wide'}
                      <span aria-hidden="true"> · </span>
                      <span className="observation-time">
                        {formatObservedAt(attraction.observedAt)}
                      </span>
                    </small>
                  </span>
                  <WaitBadge attraction={attraction} />
                </button>
              ))}
            </div>
          </article>

          <div className="analytics-column">
            <article className="surface chart-panel">
              <PanelTitle
                eyebrow="Three-month trend"
                title={selectedAttraction?.attractionName ?? 'Select an attraction'}
                detail={
                  historyQuery.data
                    ? formatWindow(
                        historyQuery.data.windowStart,
                        historyQuery.data.windowEnd,
                      )
                    : undefined
                }
              />
              <ChartContent
                loading={historyQuery.isLoading}
                error={historyQuery.isError}
                empty={!historyQuery.data?.history.length}
              >
                {historyQuery.data && (
                  <Suspense fallback={<InlineStatus message="Preparing chart..." />}>
                    <EChart
                      ariaLabel={`Daily wait-time history for ${selectedAttraction?.attractionName}`}
                      option={createHistoryChartOption(historyQuery.data.history)}
                    />
                  </Suspense>
                )}
              </ChartContent>
            </article>

            <article className="surface chart-panel">
              <PanelTitle
                eyebrow="Typical demand"
                title="Wait by weekday and time"
                detail="15-minute averages"
              />
              <ChartContent
                loading={patternsQuery.isLoading}
                error={patternsQuery.isError}
                empty={!patternsQuery.data?.patterns.length}
              >
                {patternsQuery.data && (
                  <Suspense fallback={<InlineStatus message="Preparing chart..." />}>
                    <EChart
                      ariaLabel={`Average waits by weekday and time for ${selectedAttraction?.attractionName}`}
                      option={createPatternChartOption(patternsQuery.data.patterns)}
                    />
                  </Suspense>
                )}
              </ChartContent>
            </article>
          </div>
        </section>
      </main>
    </div>
  )
}

function Metric({
  label,
  value,
  accent = false,
  compact = false,
}: {
  label: string
  value: string
  accent?: boolean
  compact?: boolean
}) {
  return (
    <article className={compact ? 'metric compact' : 'metric'}>
      <span>{label}</span>
      <strong className={accent ? 'accent' : undefined}>{value}</strong>
    </article>
  )
}

function WaitBadge({ attraction }: { attraction: CurrentWaitTime }) {
  if (!attraction.isOpen) {
    return <span className="wait-badge closed">Closed</span>
  }

  return (
    <span className={getWaitBadgeClassName(attraction.waitMinutes)}>
      <strong>{attraction.waitMinutes ?? '--'}</strong>
      <small>min</small>
    </span>
  )
}

function PanelTitle({
  eyebrow,
  title,
  detail,
}: {
  eyebrow: string
  title: string
  detail?: string
}) {
  return (
    <div className="panel-heading">
      <div>
        <p className="eyebrow">{eyebrow}</p>
        <h2>{title}</h2>
      </div>
      {detail && <span className="panel-detail">{detail}</span>}
    </div>
  )
}

function ChartContent({
  loading,
  error,
  empty,
  children,
}: {
  loading: boolean
  error: boolean
  empty: boolean
  children: React.ReactNode
}) {
  if (loading) {
    return <InlineStatus message="Loading analytics..." />
  }
  if (error) {
    return <InlineStatus message="Analytics could not be loaded." />
  }
  if (empty) {
    return <InlineStatus message="Not enough observations are available yet." />
  }
  return children
}

function InlineStatus({
  message,
  error = false,
}: {
  message: string
  error?: boolean
}) {
  return (
    <div className={error ? 'inline-status error' : 'inline-status'} role={error ? 'alert' : 'status'}>
      <span className="status-symbol" aria-hidden="true">
        {error ? '!' : 'i'}
      </span>
      <p>{message}</p>
    </div>
  )
}

function StatusScreen({ message, error = false }: { message: string; error?: boolean }) {
  return (
    <div className="status-screen" role={error ? 'alert' : 'status'}>
      <BrandMark />
      <p className="eyebrow">{error ? 'Connection error' : 'Please wait'}</p>
      <h1>{message}</h1>
      {!error && <div className="loading-line" aria-hidden="true" />}
    </div>
  )
}

function QueueSkeleton() {
  return (
    <div className="queue-skeleton" aria-label="Loading current attraction waits">
      {Array.from({ length: 7 }, (_, index) => (
        <div className="skeleton-row" key={index}>
          <span />
          <span />
        </div>
      ))}
    </div>
  )
}

function BrandMark() {
  return (
    <svg
      aria-hidden="true"
      className="brand-mark"
      viewBox="0 0 32 32"
      xmlns="http://www.w3.org/2000/svg"
    >
      <path d="M5 24V12l11-7 11 7v12l-11 5-11-5Z" fill="currentColor" />
      <path d="M10 21v-7l6-4 6 4v7l-6 2.8L10 21Z" fill="white" />
      <circle cx="16" cy="17" r="2.5" fill="currentColor" />
    </svg>
  )
}

function getWaitBadgeClassName(waitMinutes: number | null) {
  if (waitMinutes === null || waitMinutes < 30) {
    return 'wait-badge low'
  }
  if (waitMinutes < 60) {
    return 'wait-badge moderate'
  }
  return 'wait-badge high'
}

function calculateAverageCurrentWait(attractions: CurrentWaitTime[]) {
  const waits = attractions
    .filter((attraction) => attraction.isOpen && attraction.waitMinutes !== null)
    .map((attraction) => attraction.waitMinutes!)

  if (!waits.length) {
    return null
  }

  return Math.round(waits.reduce((total, wait) => total + wait, 0) / waits.length)
}

function getLandFilterValue(attraction: CurrentWaitTime) {
  return attraction.landId === null ? 'park-wide' : attraction.landId.toString()
}
