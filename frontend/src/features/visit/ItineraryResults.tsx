import { useEffect, useMemo, useRef } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { startVisitSession } from '../../api/client'
import type {
  ItineraryPreference,
  UnscheduledItineraryReason,
} from '../../api/contracts'
import { LanguageSelector, useI18n, type TranslationKey } from '../../i18n'
import { orderItineraryStops, type GeneratedItinerary } from './itineraryModel'
import { saveVisitSessionId } from './visitSessionModel'

interface ItineraryResultsProps {
  plan: GeneratedItinerary
}

const reasonKeys: Record<UnscheduledItineraryReason, TranslationKey> = {
  SkippedByVisitor: 'unscheduledSkippedByVisitor',
  AttractionUnavailable: 'unscheduledAttractionUnavailable',
  AttractionClosed: 'unscheduledAttractionClosed',
  WalkingRouteUnavailable: 'unscheduledWalkingRouteUnavailable',
  VisitWindowExceeded: 'unscheduledVisitWindowExceeded',
}

export function ItineraryResults({ plan }: ItineraryResultsProps) {
  const { itinerary, park, attractionNames } = plan
  const { locale, t } = useI18n()
  const navigate = useNavigate()
  const headingRef = useRef<HTMLHeadingElement>(null)
  const stops = useMemo(() => orderItineraryStops(itinerary.stops), [itinerary.stops])
  const startSession = useMutation({
    mutationFn: () => startVisitSession(park.id, {
      partySize: plan.partySize,
      itinerary: plan.request,
    }),
    onSuccess: (session) => {
      saveVisitSessionId(session.id)
      navigate('/visit/session')
    },
  })

  useEffect(() => {
    headingRef.current?.focus()
  }, [])

  const formatTime = (value: string) =>
    new Intl.DateTimeFormat(locale, {
      hour: 'numeric',
      minute: '2-digit',
      timeZone: park.timezone,
    }).format(new Date(value))
  const formatDate = (value: string) =>
    new Intl.DateTimeFormat(locale, {
      weekday: 'long',
      month: 'long',
      day: 'numeric',
      year: 'numeric',
      timeZone: park.timezone,
    }).format(new Date(value))
  const minutes = (count: number) => t('durationMinutes', {
    count: new Intl.NumberFormat(locale).format(count),
  })

  return (
    <div className="application-shell visit-shell">
      <title>{t('itineraryPageTitle')} · Park Pilot</title>
      <a className="skip-link" href="#itinerary-main">{t('skipToContent')}</a>
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
          <Link className="topbar-link" to="/visit/priorities">{t('editPriorities')}</Link>
        </div>
      </header>

      <main className="itinerary-main" id="itinerary-main">
        <section className="itinerary-hero" aria-labelledby="itinerary-title">
          <div>
            <p className="eyebrow">{t('itineraryEyebrow')}</p>
            <h1 id="itinerary-title" ref={headingRef} tabIndex={-1}>{t('itineraryPageTitle')}</h1>
            <p className="page-description">{t('itineraryDescription')}</p>
          </div>
          <ol className="setup-progress" aria-label={t('planningProgress')}>
            <li className="complete"><span aria-hidden="true">✓</span>{t('visitDetailsStep')}</li>
            <li className="complete"><span aria-hidden="true">✓</span>{t('prioritiesStep')}</li>
            <li className="active" aria-current="step"><span>3</span>{t('itineraryStep')}</li>
          </ol>
        </section>

        <section className="surface itinerary-overview" aria-labelledby="itinerary-overview-title">
          <div className="itinerary-overview-heading">
            <div>
              <p className="eyebrow">{park.name}</p>
              <h2 id="itinerary-overview-title">{formatDate(itinerary.visitStartAt)}</h2>
              <p>
                {formatTime(itinerary.visitStartAt)}–{formatTime(itinerary.visitEndAt)}
                {' · '}{park.timezone}
              </p>
            </div>
            <Link className="secondary-button button-link" to="/visit/priorities">
              <span aria-hidden="true">←</span>{t('editPriorities')}
            </Link>
          </div>

          <dl className="itinerary-totals" aria-label={t('itineraryTotals')}>
            <Total label={t('walkingTime')} value={minutes(itinerary.totalWalkingMinutes)} />
            <Total label={t('queueTime')} value={minutes(itinerary.totalQueueMinutes)} />
            <Total label={t('experienceTime')} value={minutes(itinerary.totalAttractionMinutes)} />
          </dl>
        </section>

        <section className="surface itinerary-schedule" aria-labelledby="schedule-results-title">
          <div className="itinerary-section-heading">
            <div>
              <p className="eyebrow">{t('optimizedOrder')}</p>
              <h2 id="schedule-results-title">{t('yourSchedule')}</h2>
            </div>
            <span>{t('scheduledStops', { count: stops.length })}</span>
          </div>

          {stops.length === 0 ? (
            <div className="empty-itinerary" role="status">
              <span aria-hidden="true">i</span>
              <div>
                <h3>{t('emptyItineraryTitle')}</h3>
                <p>{t('emptyItineraryDescription')}</p>
              </div>
            </div>
          ) : (
            <ol className="itinerary-stop-list">
              {stops.map((stop) => (
                <li key={`${stop.sequence}-${stop.attractionId}`}>
                  <article className="itinerary-stop">
                    <div className="stop-sequence" aria-hidden="true">{stop.sequence}</div>
                    <div className="stop-content">
                      <div className="stop-heading">
                        <div>
                          <p>
                            <time dateTime={stop.attractionStartsAt}>{formatTime(stop.attractionStartsAt)}</time>
                            {'–'}
                            <time dateTime={stop.completesAt}>{formatTime(stop.completesAt)}</time>
                          </p>
                          <h3>{stop.attractionName}</h3>
                        </div>
                        <span className={`itinerary-priority priority-${stop.preference.toLowerCase()}`}>
                          {t(preferenceKey(stop.preference))}
                        </span>
                      </div>
                      <dl className="stop-phases">
                        <Phase
                          label={t('walkPhase')}
                          time={formatTime(stop.travelStartsAt)}
                          dateTime={stop.travelStartsAt}
                          duration={minutes(stop.walkingMinutes)}
                        />
                        <Phase
                          label={t('queuePhase')}
                          time={formatTime(stop.queueStartsAt)}
                          dateTime={stop.queueStartsAt}
                          duration={minutes(stop.queueMinutes)}
                        />
                        <Phase
                          label={t('experiencePhase')}
                          time={formatTime(stop.attractionStartsAt)}
                          dateTime={stop.attractionStartsAt}
                          duration={minutes(stop.attractionDurationMinutes)}
                        />
                      </dl>
                    </div>
                  </article>
                </li>
              ))}
            </ol>
          )}
        </section>

        {itinerary.unscheduledAttractions.length > 0 && (
          <section className="surface unscheduled-section" aria-labelledby="unscheduled-title">
            <div className="itinerary-section-heading">
              <div>
                <p className="eyebrow">{t('outsideSchedule')}</p>
                <h2 id="unscheduled-title">{t('unscheduledTitle')}</h2>
                <p>{t('unscheduledDescription')}</p>
              </div>
              <span>{itinerary.unscheduledAttractions.length}</span>
            </div>
            <ul className="unscheduled-list">
              {itinerary.unscheduledAttractions.map((item) => (
                <li key={`${item.attractionId}-${item.reason}`}>
                  <div>
                    <strong>{attractionNames[item.attractionId] ?? t('attractionNumber', { id: item.attractionId })}</strong>
                    <span>{t(preferenceKey(item.preference))}</span>
                  </div>
                  <p>{t(reasonKeys[item.reason])}</p>
                </li>
              ))}
            </ul>
          </section>
        )}

        <footer className="itinerary-footer">
          <p>{t('generatedAt', { time: formatTime(itinerary.generatedAt) })} · {itinerary.algorithmVersion}</p>
          <div className="itinerary-footer-actions">
            <Link className="secondary-button button-link" to="/visit/priorities">
              {t('backToPriorities')}
            </Link>
            <button
              className="primary-button"
              disabled={stops.length === 0 || startSession.isPending}
              onClick={() => startSession.mutate()}
              type="button"
            >
              {startSession.isPending ? t('startingVisit') : t('startVisit')}
            </button>
          </div>
        </footer>
        {startSession.isError && (
          <div className="itinerary-generation-error" role="alert">
            <div>
              <strong>{t('visitStartFailed')}</strong>
              <p>{startSession.error.message}</p>
            </div>
          </div>
        )}
      </main>
    </div>
  )
}

function Total({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>
}

function Phase({
  label,
  time,
  dateTime,
  duration,
}: {
  label: string
  time: string
  dateTime: string
  duration: string
}) {
  return (
    <div>
      <dt>{label}</dt>
      <dd><time dateTime={dateTime}>{time}</time><strong>{duration}</strong></dd>
    </div>
  )
}

function preferenceKey(preference: ItineraryPreference): TranslationKey {
  if (preference === 'MustDo') return 'mustDo'
  if (preference === 'WouldLike') return 'wouldLike'
  return 'skip'
}

function VisitMark() {
  return (
    <svg aria-hidden="true" className="brand-mark" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <path d="M16 2.5a10 10 0 0 0-10 10c0 7.4 10 17 10 17s10-9.6 10-17a10 10 0 0 0-10-10Z" fill="currentColor" />
      <circle cx="16" cy="12.5" r="4" fill="white" />
    </svg>
  )
}
