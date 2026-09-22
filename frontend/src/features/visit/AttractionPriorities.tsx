import { useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { getCurrentWaitTimes, getParks, optimizeItinerary } from '../../api/client'
import type { CurrentWaitTime, OptimizeItineraryRequest, Park } from '../../api/contracts'
import { LanguageSelector, useI18n, type TranslationKey } from '../../i18n'
import type { VisitDetails } from './visitSetupModel'
import {
  attractionNameSnapshot,
  buildOptimizeItineraryRequest,
  type GeneratedItinerary,
} from './itineraryModel'
import {
  countPriorities,
  recommendUnselectedAttractions,
  type AttractionPriorities,
  type AttractionPriority,
} from './priorityModel'

interface AttractionPrioritiesProps {
  visitDetails: VisitDetails
  selectedParkId: number | undefined
  priorities: AttractionPriorities
  onParkChange: (parkId: number) => void
  onGenerated: (itinerary: GeneratedItinerary) => void
  onPriorityChange: (
    parkId: number,
    attractionId: number,
    priority: AttractionPriority | null,
  ) => void
}

type PriorityFilter = 'all' | 'unselected' | AttractionPriority

const priorityOptions: ReadonlyArray<{
  value: AttractionPriority | null
  label: TranslationKey
  symbol: string
}> = [
  { value: null, label: 'unselected', symbol: '○' },
  { value: 'must-do', label: 'mustDo', symbol: '★' },
  { value: 'would-like', label: 'wouldLike', symbol: '+' },
  { value: 'skip', label: 'skip', symbol: '×' },
]

export function AttractionPriorities({
  visitDetails,
  selectedParkId,
  priorities,
  onParkChange,
  onGenerated,
  onPriorityChange,
}: AttractionPrioritiesProps) {
  const { locale, t } = useI18n()
  const navigate = useNavigate()
  const headingRef = useRef<HTMLHeadingElement>(null)
  const recommendationsRef = useRef<HTMLElement>(null)
  const recommendationHeadingRef = useRef<HTMLHeadingElement>(null)
  const recommendationFocusPending = useRef(false)
  const generationErrorRef = useRef<HTMLDivElement>(null)
  const generationAbortController = useRef<AbortController | null>(null)
  const [search, setSearch] = useState('')
  const [landFilter, setLandFilter] = useState('all')
  const [priorityFilter, setPriorityFilter] = useState<PriorityFilter>('all')
  const [announcement, setAnnouncement] = useState('')
  const [requestValidationError, setRequestValidationError] = useState(false)

  const parksQuery = useQuery({
    queryKey: ['parks'],
    queryFn: ({ signal }) => getParks(signal),
    staleTime: 30 * 60_000,
  })

  const parks = parksQuery.data ?? []

  useEffect(() => {
    headingRef.current?.focus()
  }, [])

  useEffect(() => {
    if (parks.length > 0 && !parks.some((park) => park.id === selectedParkId)) {
      onParkChange(parks[0].id)
    }
  }, [onParkChange, parks, selectedParkId])

  useEffect(() => {
    setSearch('')
    setLandFilter('all')
    setPriorityFilter('all')
  }, [selectedParkId])

  useEffect(() => {
    if (!recommendationFocusPending.current) return
    recommendationFocusPending.current = false
    const frame = requestAnimationFrame(() => {
      const nextSuggestion =
        recommendationsRef.current?.querySelector<HTMLButtonElement>('.recommendation-add')
      const focusTarget = nextSuggestion ?? recommendationHeadingRef.current
      focusTarget?.focus()
    })
    return () => cancelAnimationFrame(frame)
  }, [priorities])

  const waitsQuery = useQuery({
    queryKey: ['current-waits', selectedParkId],
    queryFn: ({ signal }) => getCurrentWaitTimes(selectedParkId!, signal),
    enabled: selectedParkId !== undefined,
    refetchInterval: 30_000,
  })

  const attractions = useMemo(
    () =>
      [...(waitsQuery.data?.attractions ?? [])].sort((left, right) => {
        const landComparison = (left.landName ?? '').localeCompare(
          right.landName ?? '',
          locale,
        )
        return (
          landComparison ||
          left.attractionName.localeCompare(right.attractionName, locale) ||
          left.attractionId - right.attractionId
        )
      }),
    [locale, waitsQuery.data],
  )

  const lands = useMemo(
    () =>
      [...new Set(attractions.map((attraction) => attraction.landName ?? t('parkWide')))].sort(
        (left, right) => left.localeCompare(right, locale),
      ),
    [attractions, locale, t],
  )

  const filteredAttractions = useMemo(() => {
    const normalizedSearch = search.trim().toLocaleLowerCase(locale)

    return attractions.filter((attraction) => {
      const priority = priorities[attraction.attractionId]
      const land = attraction.landName ?? t('parkWide')
      const matchesSearch =
        !normalizedSearch ||
        attraction.attractionName.toLocaleLowerCase(locale).includes(normalizedSearch)
      const matchesLand = landFilter === 'all' || land === landFilter
      const matchesPriority =
        priorityFilter === 'all' ||
        (priorityFilter === 'unselected'
          ? priority === undefined
          : priority === priorityFilter)

      return matchesSearch && matchesLand && matchesPriority
    })
  }, [attractions, landFilter, locale, priorities, priorityFilter, search, t])

  const recommendations = useMemo(
    () => recommendUnselectedAttractions(attractions, priorities),
    [attractions, priorities],
  )
  const counts = countPriorities(priorities, attractions.length)
  const selectedPark = parks.find((park) => park.id === selectedParkId)
  const generation = useMutation({
    mutationFn: (submission: {
      park: Park
      request: OptimizeItineraryRequest
      attractionNames: Readonly<Record<number, string>>
      signal: AbortSignal
    }) => optimizeItinerary(submission.park.id, submission.request, submission.signal),
    onSuccess: (itinerary, submission) => {
      if (submission.signal.aborted) return
      onGenerated({
        itinerary,
        park: submission.park,
        attractionNames: submission.attractionNames,
        partySize: visitDetails.partySize,
        request: submission.request,
      })
      navigate('/visit/itinerary')
    },
  })

  useEffect(() => {
    generationAbortController.current?.abort()
    generation.reset()
    setRequestValidationError(false)
  }, [selectedParkId])

  useEffect(() => {
    return () => generationAbortController.current?.abort()
  }, [])

  useEffect(() => {
    if (generation.isError || requestValidationError) generationErrorRef.current?.focus()
  }, [generation.isError, requestValidationError])

  const generateItinerary = () => {
    if (!selectedPark || Object.keys(priorities).length === 0) return
    generationAbortController.current?.abort()
    generation.reset()
    setRequestValidationError(false)

    let request: OptimizeItineraryRequest
    try {
      request = buildOptimizeItineraryRequest(
        visitDetails,
        selectedPark.timezone,
        priorities,
      )
    } catch (error) {
      if (!(error instanceof RangeError)) throw error
      setRequestValidationError(true)
      return
    }

    const abortController = new AbortController()
    generationAbortController.current = abortController
    generation.mutate({
      park: selectedPark,
      request,
      attractionNames: attractionNameSnapshot(attractions),
      signal: abortController.signal,
    })
  }

  const changePriority = (
    attraction: CurrentWaitTime,
    priority: AttractionPriority | null,
  ) => {
    if (selectedParkId === undefined) return
    generation.reset()
    setRequestValidationError(false)
    onPriorityChange(selectedParkId, attraction.attractionId, priority)
    setAnnouncement(
      priority === null
        ? t('priorityCleared', { name: attraction.attractionName })
        : t('priorityUpdated', {
            name: attraction.attractionName,
            priority: t(priorityLabel(priority)),
          }),
    )
  }

  return (
    <div className="application-shell visit-shell">
      <title>{t('priorityPageTitle')} · Park Pilot</title>
      <a className="skip-link" href="#priorities-main">{t('skipToContent')}</a>
      <header className="topbar visit-topbar">
        <Link className="brand" to="/" aria-label={t('home')}>
          <VisitMark />
          <span>
            <strong>Park Pilot</strong>
            <small>{t('visitPlanner')}</small>
          </span>
        </Link>
        <div className="topbar-actions">
          <LanguageSelector />
          <Link className="topbar-link" to="/">{t('backToWaitTimes')}</Link>
        </div>
      </header>

      <main className="priority-main" id="priorities-main">
        <aside className="priority-introduction" aria-labelledby="priority-page-title">
          <p className="eyebrow">{t('priorityEyebrow')}</p>
          <h1 id="priority-page-title" ref={headingRef} tabIndex={-1}>
            {t('priorityPageTitle')}
          </h1>
          <p className="page-description">{t('priorityDescription')}</p>

          <ol className="setup-progress" aria-label={t('planningProgress')}>
            <li className="complete"><span aria-hidden="true">✓</span>{t('visitDetailsStep')}</li>
            <li className="active" aria-current="step"><span>2</span>{t('prioritiesStep')}</li>
            <li><span>3</span>{t('itineraryStep')}</li>
          </ol>

          <section className="surface priority-visit-summary" aria-labelledby="visit-context-title">
            <div>
              <p className="eyebrow" id="visit-context-title">{t('visitContext')}</p>
              <strong>{formatVisitDate(visitDetails.date, locale)}</strong>
              <span>
                {formatVisitTime(visitDetails.arrivalTime, locale)}–{formatVisitTime(visitDetails.departureTime, locale)}
                {' · '}
                {t('peopleCount', { count: visitDetails.partySize })}
              </span>
            </div>
            <Link to="/visit">{t('editVisit')}</Link>
          </section>

          <aside className="visit-note priority-note">
            <span aria-hidden="true">✦</span>
            <div>
              <strong>{t('priorityTipTitle')}</strong>
              <p>{t('priorityTip')}</p>
            </div>
          </aside>
        </aside>

        <div className="priority-workspace" aria-busy={parksQuery.isLoading || waitsQuery.isLoading || generation.isPending}>
          <section className="surface park-choice" aria-labelledby="park-choice-title">
            <div>
              <p className="eyebrow">{t('catalogChoices')}</p>
              <h2 id="park-choice-title">{t('choosePark')}</h2>
              <p>{t('chooseParkHelp')}</p>
            </div>
            {parksQuery.isLoading ? (
              <span className="compact-loading" role="status">{t('loadingParks')}</span>
            ) : parksQuery.isError ? (
              <StatusPanel
                error
                message={t('catalogUnavailable')}
                onRetry={() => void parksQuery.refetch()}
              />
            ) : parks.length === 0 ? (
              <StatusPanel message={t('noParksConfigured')} />
            ) : (
              <label className="priority-park-selector" htmlFor="priority-park">
                <span>{t('viewingPark')}</span>
                <select
                  disabled={generation.isPending}
                  id="priority-park"
                  value={selectedParkId ?? ''}
                  onChange={(event) => {
                    const parkId = Number(event.target.value)
                    onParkChange(parkId)
                    setAnnouncement(t('parkChangedStatus', {
                      name: parks.find((park) => park.id === parkId)?.name ?? '',
                    }))
                  }}
                >
                  {parks.map((park) => <option key={park.id} value={park.id}>{park.name}</option>)}
                </select>
              </label>
            )}
          </section>

          {selectedParkId !== undefined && (
            <>
              {waitsQuery.isLoading && <PriorityLoading />}
              {waitsQuery.isError && (
                <StatusPanel
                  error
                  message={t('attractionCatalogUnavailable')}
                  onRetry={() => void waitsQuery.refetch()}
                />
              )}
              {!waitsQuery.isLoading && !waitsQuery.isError && attractions.length === 0 && (
                <StatusPanel message={t('noAttractionsInPark')} />
              )}
              {!waitsQuery.isLoading && !waitsQuery.isError && attractions.length > 0 && (
                <>
                  <section className="priority-summary-grid" aria-label={t('prioritySummary')}>
                    <PriorityMetric className="must" count={counts.mustDo} label={t('mustDo')} />
                    <PriorityMetric className="like" count={counts.wouldLike} label={t('wouldLike')} />
                    <PriorityMetric className="skip" count={counts.skip} label={t('skip')} />
                    <PriorityMetric className="none" count={counts.unselected} label={t('unselected')} />
                  </section>

                  <section className="surface recommendations" aria-labelledby="recommendations-title" ref={recommendationsRef}>
                    <div className="recommendations-heading">
                      <div>
                        <p className="eyebrow">{t('suggestionsEyebrow')}</p>
                        <h2 id="recommendations-title" ref={recommendationHeadingRef} tabIndex={-1}>{t('suggestionsTitle')}</h2>
                        <p>{t('suggestionsDescription')}</p>
                      </div>
                      <span className="suggestion-not-saved">{t('suggestionBadge')}</span>
                    </div>
                    <p className="recommendation-method">{t('recommendationMethod')}</p>
                    {recommendations.length === 0 ? (
                      <p className="recommendation-empty">{t('noRecommendations')}</p>
                    ) : (
                      <div className="recommendation-grid">
                        {recommendations.map(({ attraction, reason }) => (
                          <article className="recommendation-card" key={attraction.attractionId}>
                            <div>
                              <span className="suggestion-label">{t('suggestionBadge')}</span>
                              <h3>{attraction.attractionName}</h3>
                              <p className="attraction-meta">
                                {attraction.landName ?? t('parkWide')}
                                {' · '}
                                {formatWait(attraction, locale, t)}
                              </p>
                              <p className="recommendation-reason">
                                {reason === 'known-live-wait'
                                  ? t('recommendationKnownWait', { count: attraction.waitMinutes ?? 0 })
                                  : t('recommendationOpenNoWait')}
                              </p>
                            </div>
                            <button
                              className="recommendation-add"
                              disabled={generation.isPending}
                              onClick={() => {
                                recommendationFocusPending.current = true
                                changePriority(attraction, 'would-like')
                              }}
                              type="button"
                            >
                              <span aria-hidden="true">+</span>{t('addWouldLike')}
                            </button>
                          </article>
                        ))}
                      </div>
                    )}
                  </section>

                  <section className="surface priority-catalog" aria-labelledby="catalog-title">
                    <div className="priority-catalog-heading">
                      <div>
                        <p className="eyebrow">{selectedPark?.name ?? t('attractions')}</p>
                        <h2 id="catalog-title">{t('catalogTitle')}</h2>
                        <p>{t('catalogDescription')}</p>
                      </div>
                      <span>{t('attractionsCount', { count: attractions.length })}</span>
                    </div>

                    <div className="priority-filters" aria-label={t('filters')}>
                      <label>
                        <span>{t('prioritySearchLabel')}</span>
                        <input
                          onChange={(event) => setSearch(event.target.value)}
                          placeholder={t('searchPriorityAttractions')}
                          type="search"
                          value={search}
                        />
                      </label>
                      <label>
                        <span>{t('showLand')}</span>
                        <select value={landFilter} onChange={(event) => setLandFilter(event.target.value)}>
                          <option value="all">{t('allLands')}</option>
                          {lands.map((land) => <option key={land} value={land}>{land}</option>)}
                        </select>
                      </label>
                      <label>
                        <span>{t('showPriority')}</span>
                        <select
                          value={priorityFilter}
                          onChange={(event) => setPriorityFilter(event.target.value as PriorityFilter)}
                        >
                          <option value="all">{t('allPriorities')}</option>
                          <option value="must-do">{t('mustDo')}</option>
                          <option value="would-like">{t('wouldLike')}</option>
                          <option value="skip">{t('skip')}</option>
                          <option value="unselected">{t('unselected')}</option>
                        </select>
                      </label>
                    </div>

                    <div className="priority-list" role={filteredAttractions.length > 0 ? 'list' : undefined}>
                      {filteredAttractions.length === 0 ? (
                        <StatusPanel message={t('noPriorityMatches')} />
                      ) : filteredAttractions.map((attraction) => (
                        <AttractionPriorityRow
                          attraction={attraction}
                          key={attraction.attractionId}
                          disabled={generation.isPending}
                          onChange={(priority) => changePriority(attraction, priority)}
                          priority={priorities[attraction.attractionId]}
                        />
                      ))}
                    </div>

                    {(generation.isError || requestValidationError) && (
                      <div
                        className="itinerary-generation-error"
                        ref={generationErrorRef}
                        role="alert"
                        tabIndex={-1}
                      >
                        <div>
                          <strong>{t('itineraryGenerationFailed')}</strong>
                          <p>
                            {requestValidationError
                              ? t('itineraryVisitTimeInvalid')
                              : t('itineraryGenerationFailedHelp')}
                          </p>
                        </div>
                        {requestValidationError ? (
                          <Link className="secondary-button button-link" to="/visit">
                            {t('editVisitDetails')}
                          </Link>
                        ) : (
                          <button
                            className="secondary-button"
                            disabled={generation.isPending}
                            onClick={generateItinerary}
                            type="button"
                          >
                            {t('retry')}
                          </button>
                        )}
                      </div>
                    )}
                    <footer className="priority-footer">
                      <div className="priority-footer-copy">
                        <p><span aria-hidden="true">●</span>{t('automaticSave')}</p>
                        {Object.keys(priorities).length === 0 && (
                          <p className="generate-help">{t('selectPriorityToGenerate')}</p>
                        )}
                      </div>
                      <div className="priority-footer-actions">
                        <Link className="secondary-button button-link" to="/visit">{t('editVisitDetails')}</Link>
                        <button
                          className="primary-button generate-itinerary-button"
                          disabled={
                            generation.isPending ||
                            !selectedPark ||
                            Object.keys(priorities).length === 0
                          }
                          onClick={generateItinerary}
                          type="button"
                        >
                          {generation.isPending ? t('generatingItinerary') : t('generateItinerary')}
                          <span aria-hidden="true">→</span>
                        </button>
                      </div>
                    </footer>
                  </section>
                </>
              )}
            </>
          )}
          <p className="sr-only" aria-live="polite" aria-atomic="true">{announcement}</p>
        </div>
      </main>
    </div>
  )
}

function AttractionPriorityRow({
  attraction,
  priority,
  onChange,
  disabled,
}: {
  attraction: CurrentWaitTime
  priority: AttractionPriority | undefined
  onChange: (priority: AttractionPriority | null) => void
  disabled: boolean
}) {
  const { locale, t } = useI18n()

  return (
    <article className={`priority-row priority-row-${priority ?? 'unselected'}`} role="listitem">
      <div className="priority-attraction-copy">
        <h3>{attraction.attractionName}</h3>
        <p>
          <span>{attraction.landName ?? t('parkWide')}</span>
          <span className={attraction.isOpen ? 'availability open' : 'availability closed'}>
            {formatWait(attraction, locale, t)}
          </span>
        </p>
      </div>
      <fieldset className="priority-control" disabled={disabled}>
        <legend>{t('setPriorityFor', { name: attraction.attractionName })}</legend>
        <div>
          {priorityOptions.map((option) => {
            const id = `priority-${attraction.attractionId}-${option.value ?? 'none'}`
            return (
              <label className={`priority-option option-${option.value ?? 'unselected'}`} htmlFor={id} key={id}>
                <input
                  checked={(option.value ?? undefined) === priority}
                  id={id}
                  name={`priority-${attraction.attractionId}`}
                  onChange={() => onChange(option.value)}
                  type="radio"
                />
                <span><b aria-hidden="true">{option.symbol}</b>{t(option.label)}</span>
              </label>
            )
          })}
        </div>
      </fieldset>
    </article>
  )
}

function PriorityMetric({ className, count, label }: { className: string; count: number; label: string }) {
  return (
    <article className={`priority-metric ${className}`}>
      <strong>{count}</strong>
      <span>{label}</span>
    </article>
  )
}

function StatusPanel({
  message,
  error = false,
  onRetry,
}: {
  message: string
  error?: boolean
  onRetry?: () => void
}) {
  const { t } = useI18n()
  return (
    <div className={`priority-status${error ? ' error' : ''}`} role={error ? 'alert' : 'status'}>
      <span aria-hidden="true">{error ? '!' : 'i'}</span>
      <p>{message}</p>
      {onRetry && <button onClick={onRetry} type="button">{t('retry')}</button>}
    </div>
  )
}

function PriorityLoading() {
  const { t } = useI18n()
  return (
    <div className="surface priority-loading" role="status">
      <span>{t('loadingAttractionCatalog')}</span>
      {Array.from({ length: 4 }, (_, index) => <i key={index} aria-hidden="true" />)}
    </div>
  )
}

function priorityLabel(priority: AttractionPriority): TranslationKey {
  if (priority === 'must-do') return 'mustDo'
  if (priority === 'would-like') return 'wouldLike'
  return 'skip'
}

function formatWait(
  attraction: CurrentWaitTime,
  locale: string,
  t: ReturnType<typeof useI18n>['t'],
): string {
  if (!attraction.isOpen) return t('closed')
  if (attraction.waitMinutes === null) return t('openWithoutWait')
  return t('liveWaitMinutes', {
    count: new Intl.NumberFormat(locale).format(attraction.waitMinutes),
  })
}

function formatVisitDate(value: string, locale: string): string {
  const [year, month, day] = value.split('-').map(Number)
  return new Intl.DateTimeFormat(locale, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date(year, month - 1, day, 12))
}

function formatVisitTime(value: string, locale: string): string {
  const [hours, minutes] = value.split(':').map(Number)
  return new Intl.DateTimeFormat(locale, { hour: 'numeric', minute: '2-digit' }).format(
    new Date(2000, 0, 1, hours, minutes),
  )
}

function VisitMark() {
  return (
    <svg aria-hidden="true" className="brand-mark" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <path d="M16 2.5a10 10 0 0 0-10 10c0 7.4 10 17 10 17s10-9.6 10-17a10 10 0 0 0-10-10Z" fill="currentColor" />
      <circle cx="16" cy="12.5" r="4" fill="white" />
    </svg>
  )
}