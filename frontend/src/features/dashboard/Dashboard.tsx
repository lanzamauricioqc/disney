import { lazy, Suspense, useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import {
  getCurrentWaitTimes,
  getDailyParkWaitTimes,
  getDailyWaitTimeHistory,
  getParks,
  getWeekdayWaitTimePatterns,
} from '../../api/client'
import type { CurrentWaitTime } from '../../api/contracts'
import {
  createDailyParkChartOption,
  createHistoryChartOption,
  createPatternChartOption,
} from './chartOptions'
import { formatObservedAt, formatWindow } from './formatters'
import { LanguageSelector, useI18n } from '../../i18n'

const EChart = lazy(() =>
  import('../../components/EChart').then((module) => ({ default: module.EChart })),
)

export function Dashboard() {
  const { locale, t } = useI18n()
  const [selectedParkId, setSelectedParkId] = useState<number>()
  const [selectedAttractionId, setSelectedAttractionId] = useState<number>()
  const [selectedLand, setSelectedLand] = useState('all')
  const [attractionNameFilter, setAttractionNameFilter] = useState('')
  const [selectedWeekStart, setSelectedWeekStart] = useState<string>()

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
      lands.set(getLandFilterValue(attraction), attraction.landName ?? t('parkWide'))
    })

    return [...lands.entries()].sort((left, right) =>
      left[1].localeCompare(right[1], locale),
    )
  }, [attractions, locale, t])

  const filteredAttractions = useMemo(() => {
    const normalizedNameFilter = attractionNameFilter.trim().toLocaleLowerCase(locale)

    return attractions.filter((attraction) => {
      const matchesLand =
        selectedLand === 'all' || getLandFilterValue(attraction) === selectedLand
      const matchesName =
        !normalizedNameFilter ||
        attraction.attractionName.toLocaleLowerCase(locale).includes(normalizedNameFilter)

      return matchesLand && matchesName
    })
  }, [attractionNameFilter, attractions, locale, selectedLand])

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

  const dailyParksQuery = useQuery({
    queryKey: ['daily-park-waits', selectedWeekStart],
    queryFn: ({ signal }) => getDailyParkWaitTimes(selectedWeekStart, signal),
    staleTime: 10 * 60_000,
  })

  const selectedPark = parksQuery.data?.find((park) => park.id === selectedParkId)
  const selectedAttraction = attractions.find(
    (attraction) => attraction.attractionId === selectedAttractionId,
  )
  const openCount = attractions.filter((attraction) => attraction.isOpen).length
  const averageCurrentWait = calculateAverageCurrentWait(attractions)

  if (parksQuery.isLoading) {
    return <StatusScreen message={t('loadingParks')} />
  }

  if (parksQuery.isError) {
    return <StatusScreen message={t('catalogUnavailable')} error />
  }

  return (
    <div className="application-shell">
      <header className="topbar">
        <a className="brand" href="/" aria-label={t('home')}>
          <BrandMark />
          <span>
            <strong>{t('brand')}</strong>
            <small>{t('parkOperations')}</small>
          </span>
        </a>
        <div className="topbar-actions">
          <LanguageSelector />
          <Link className="topbar-link" to="/admin">{t('administration')}</Link>
          <div className="topbar-status">
            <span className="status-dot" aria-hidden="true" />
            {t('serviceOnline')}
          </div>
        </div>
      </header>

      <main>
        <header className="page-header">
          <div>
            <p className="eyebrow">{t('liveOperations')}</p>
            <h1>{t('waitTimes')}</h1>
            <p className="page-description">
              {t('dashboardDescription')}
            </p>
          </div>
          <label className="park-selector" htmlFor="park-selector">
            <span>{t('viewingPark')}</span>
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

        <section className="summary-bar" aria-label={t('parkSummary')}>
          <Metric label={t('trackedAttractions')} value={new Intl.NumberFormat(locale).format(attractions.length)} />
          <Metric label={t('currentlyOpen')} value={new Intl.NumberFormat(locale).format(openCount)} accent />
          <Metric
            label={t('averageCurrentWait')}
            value={averageCurrentWait === null ? '--' : `${new Intl.NumberFormat(locale).format(averageCurrentWait)} ${t('min')}`}
          />
          <Metric label={t('parkTimezone')} value={selectedPark?.timezone ?? '--'} compact />
        </section>

        <article className="surface chart-panel weekly-park-panel">
          <div className="panel-heading weekly-park-heading">
            <div>
              <p className="eyebrow">{t('dailyComparison')}</p>
              <h2>{t('averageEachPark')}</h2>
              <p className="metric-description">
                {t('dailyDescription')}
              </p>
            </div>
            <WeekNavigation
              data={dailyParksQuery.data}
              disabled={dailyParksQuery.isFetching}
              onChange={setSelectedWeekStart}
            />
          </div>
          <ChartContent
            loading={dailyParksQuery.isLoading}
            error={dailyParksQuery.isError}
            empty={!dailyParksQuery.data?.parks.length}
          >
            {dailyParksQuery.data && (
              <Suspense fallback={<InlineStatus message={t('preparingChart')} />}>
                <EChart
                  ariaLabel={t('dailyParkAria', { start: formatDashboardDate(dailyParksQuery.data.weekStart, locale), end: formatDashboardDate(dailyParksQuery.data.weekEnd, locale) })}
                  option={createDailyParkChartOption(
                    dailyParksQuery.data.weekStart,
                    dailyParksQuery.data.parks,
                    locale,
                    t,
                  )}
                />
              </Suspense>
            )}
          </ChartContent>
        </article>

        <section className="dashboard-grid">
          <article className="surface queue-panel">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">{t('currentConditions')}</p>
                <h2>{t('attractionQueues')}</h2>
              </div>
              <div className="live-status">
                <span className="live-indicator">{t('live')}</span>
                {currentWaitsQuery.data && (
                  <span className="updated-at">
                    {t('updated', { date: formatObservedAt(currentWaitsQuery.data.generatedAt, locale) })}
                  </span>
                )}
              </div>
            </div>
            <div className="queue-filters" aria-label={t('filterQueues')}>
              <label>
                <span>{t('land')}</span>
                <select
                  aria-label={t('filterLand')}
                  value={selectedLand}
                  onChange={(event) => setSelectedLand(event.target.value)}
                >
                  <option value="all">{t('allLands')}</option>
                  {landOptions.map(([value, name]) => (
                    <option key={value} value={value}>
                      {name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>{t('attractionName')}</span>
                <input
                  aria-label={t('filterName')}
                  onChange={(event) => setAttractionNameFilter(event.target.value)}
                  placeholder={t('searchAttractions')}
                  type="search"
                  value={attractionNameFilter}
                />
              </label>
            </div>
            <div className="queue-column-labels" aria-hidden="true">
              <span>{t('attraction')}</span>
              <span>{t('wait')}</span>
            </div>

            {currentWaitsQuery.isLoading && <QueueSkeleton />}
            {currentWaitsQuery.isError && (
              <InlineStatus message={t('currentWaitsUnavailable')} error />
            )}
            {!currentWaitsQuery.isLoading &&
              !currentWaitsQuery.isError &&
              attractions.length === 0 && (
                <InlineStatus message={t('noObservations')} />
              )}
            {!currentWaitsQuery.isLoading &&
              !currentWaitsQuery.isError &&
              attractions.length > 0 &&
              filteredAttractions.length === 0 && (
                <InlineStatus message={t('noFilterMatches')} />
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
                      {attraction.landName ?? t('parkWide')}
                      <span aria-hidden="true"> · </span>
                      <span className="observation-time">
                        {formatObservedAt(attraction.observedAt, locale)}
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
                eyebrow={t('threeMonthTrend')}
                title={selectedAttraction?.attractionName ?? t('selectAttraction')}
                detail={
                  historyQuery.data
                    ? formatWindow(
                        historyQuery.data.windowStart,
                        historyQuery.data.windowEnd,
                        locale,
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
                  <Suspense fallback={<InlineStatus message={t('preparingChart')} />}>
                    <EChart
                      ariaLabel={t('dailyHistoryAria', { name: selectedAttraction?.attractionName ?? '' })}
                      option={createHistoryChartOption(historyQuery.data.history, locale, t)}
                    />
                  </Suspense>
                )}
              </ChartContent>
            </article>

            <article className="surface chart-panel">
              <PanelTitle
                eyebrow={t('typicalDemand')}
                title={t('waitByWeekday')}
                detail={t('averages15')}
              />
              <ChartContent
                loading={patternsQuery.isLoading}
                error={patternsQuery.isError}
                empty={!patternsQuery.data?.patterns.length}
              >
                {patternsQuery.data && (
                  <Suspense fallback={<InlineStatus message={t('preparingChart')} />}>
                    <EChart
                      ariaLabel={t('patternAria', { name: selectedAttraction?.attractionName ?? '' })}
                      option={createPatternChartOption(patternsQuery.data.patterns, locale, t)}
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

function WeekNavigation({
  data,
  disabled,
  onChange,
}: {
  data: Awaited<ReturnType<typeof getDailyParkWaitTimes>> | undefined
  disabled: boolean
  onChange: (weekStart: string) => void
}) {
  const { locale, t } = useI18n()
  if (!data) {
    return <span className="panel-detail">{t('currentWeek')}</span>
  }

  const previousWeekStart = addDays(data.weekStart, -7)
  const nextWeekStart = addDays(data.weekStart, 7)
  const previousWeekEnd = addDays(previousWeekStart, 6)
  const canGoBack = previousWeekEnd >= data.availableFrom
  const canGoForward = nextWeekStart <= data.currentWeekStart

  return (
    <div className="week-navigation" aria-label={t('selectWeek')}>
      <span>{formatWeekRange(data.weekStart, data.weekEnd, locale)}</span>
      <div>
        <button
          aria-label={t('previousWeek')}
          disabled={disabled || !canGoBack}
          onClick={() => onChange(previousWeekStart)}
          type="button"
        >
          ‹
        </button>
        <button
          aria-label={t('nextWeek')}
          disabled={disabled || !canGoForward}
          onClick={() => onChange(nextWeekStart)}
          type="button"
        >
          ›
        </button>
      </div>
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
  const { locale, t } = useI18n()
  if (!attraction.isOpen) {
    return <span className="wait-badge closed">{t('closed')}</span>
  }

  return (
    <span className={getWaitBadgeClassName(attraction.waitMinutes)}>
      <strong>{attraction.waitMinutes === null ? '--' : new Intl.NumberFormat(locale).format(attraction.waitMinutes)}</strong>
      <small>{t('min')}</small>
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
  const { t } = useI18n()
  if (loading) {
    return <InlineStatus message={t('loadingAnalytics')} />
  }
  if (error) {
    return <InlineStatus message={t('analyticsUnavailable')} />
  }
  if (empty) {
    return <InlineStatus message={t('notEnoughData')} />
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
  const { t } = useI18n()
  return (
    <div className="status-screen" role={error ? 'alert' : 'status'}>
      <BrandMark />
      <p className="eyebrow">{error ? t('connectionError') : t('pleaseWait')}</p>
      <h1>{message}</h1>
      {!error && <div className="loading-line" aria-hidden="true" />}
    </div>
  )
}

function QueueSkeleton() {
  const { t } = useI18n()
  return (
    <div className="queue-skeleton" aria-label={t('loadingWaits')}>
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

function addDays(date: string, days: number) {
  const parsedDate = new Date(`${date}T12:00:00Z`)
  parsedDate.setUTCDate(parsedDate.getUTCDate() + days)
  return parsedDate.toISOString().slice(0, 10)
}

function formatWeekRange(weekStart: string, weekEnd: string, locale: string) {
  const formatter = new Intl.DateTimeFormat(locale, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  })
  return `${formatter.format(new Date(`${weekStart}T12:00:00Z`))} – ${formatter.format(
    new Date(`${weekEnd}T12:00:00Z`),
  )}`
}


function formatDashboardDate(value: string, locale: string) {
  return new Intl.DateTimeFormat(locale, { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`))
}
