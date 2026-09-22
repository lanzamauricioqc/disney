import { useMemo, type ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, Navigate } from 'react-router-dom'
import {
  completeVisitAttraction,
  estimateWalkingTime,
  getCurrentWaitTimes,
  getParks,
  getVisitSession,
  skipVisitAttraction,
} from '../../api/client'
import type { CurrentWaitTime, VisitSessionStop } from '../../api/contracts'
import { LanguageSelector, useI18n } from '../../i18n'
import {
  clearVisitSessionId,
  getFreshCurrentWaitForStop,
  getLastCompletedStop,
  getNextPendingStop,
  readVisitSessionId,
} from './visitSessionModel'

type StopAction = 'complete' | 'skip'

export function ActiveVisit() {
  const { locale, t } = useI18n()
  const queryClient = useQueryClient()
  const sessionId = useMemo(readVisitSessionId, [])
  const sessionQuery = useQuery({
    queryKey: ['visit-session', sessionId],
    queryFn: ({ signal }) => getVisitSession(sessionId!, signal),
    enabled: sessionId !== null,
    refetchOnWindowFocus: true,
  })
  const parksQuery = useQuery({
    queryKey: ['parks'],
    queryFn: ({ signal }) => getParks(signal),
    staleTime: 30 * 60_000,
  })
  const activeParkId = sessionQuery.data?.parkId
  const nextStopForQuery = sessionQuery.data
    ? getNextPendingStop(sessionQuery.data)
    : null
  const lastCompletedStop = sessionQuery.data
    ? getLastCompletedStop(sessionQuery.data)
    : null
  const waitsQuery = useQuery({
    queryKey: ['current-waits', activeParkId],
    queryFn: ({ signal }) => getCurrentWaitTimes(activeParkId!, signal),
    enabled: activeParkId !== undefined,
    refetchInterval: 30_000,
  })
  const walkingQuery = useQuery({
    queryKey: [
      'walking-time',
      activeParkId,
      lastCompletedStop?.attractionId,
      nextStopForQuery?.attractionId,
    ],
    queryFn: ({ signal }) => estimateWalkingTime(
      activeParkId!,
      lastCompletedStop!.attractionId,
      nextStopForQuery!.attractionId,
      signal,
    ),
    enabled:
      activeParkId !== undefined &&
      lastCompletedStop !== null &&
      nextStopForQuery !== null,
  })
  const updateStop = useMutation({
    mutationFn: ({ action, attractionId }: {
      action: StopAction
      attractionId: number
    }) => action === 'complete'
      ? completeVisitAttraction(sessionId!, attractionId)
      : skipVisitAttraction(sessionId!, attractionId),
    onSuccess: (session) => {
      queryClient.setQueryData(['visit-session', sessionId], session)
    },
  })

  if (!sessionId) {
    return <Navigate to="/visit" replace />
  }

  if (sessionQuery.isPending || parksQuery.isPending) {
    return <VisitStatus message={t('loadingVisitSession')} />
  }

  if (sessionQuery.isError || parksQuery.isError) {
    return (
      <VisitStatus
        message={t('visitSessionUnavailable')}
        action={
          <Link
            className="primary-button button-link"
            onClick={clearVisitSessionId}
            to="/visit"
          >
            {t('planNewVisit')}
          </Link>
        }
      />
    )
  }

  const session = sessionQuery.data
  const park = parksQuery.data.find(candidate => candidate.id === session.parkId)
  const timeZone = park?.timezone ?? 'UTC'
  const completed = session.stops.filter(stop => stop.status === 'Completed').length
  const resolved = session.stops.filter(stop => stop.status !== 'Pending').length
  const nextStop = getNextPendingStop(session)
  const currentWait = nextStop
    ? getFreshCurrentWaitForStop(waitsQuery.data?.attractions ?? [], nextStop)
    : null
  const walkingMinutes = lastCompletedStop
    ? walkingQuery.data?.status === 'Available'
      ? walkingQuery.data.estimatedWalkingMinutes
      : null
    : nextStop?.walkingMinutes ?? null
  const formatTime = (value: string) => new Intl.DateTimeFormat(locale, {
    hour: 'numeric',
    minute: '2-digit',
    timeZone,
  }).format(new Date(value))

  return (
    <div className="application-shell visit-shell">
      <title>{t('activeVisitTitle')} · Park Pilot</title>
      <a className="skip-link" href="#active-visit-main">{t('skipToContent')}</a>
      <header className="topbar visit-topbar">
        <Link className="brand" to="/" aria-label={t('home')}>
          <span>
            <strong>Park Pilot</strong>
            <small>{t('activeVisit')}</small>
          </span>
        </Link>
        <LanguageSelector />
      </header>

      <main className="itinerary-main" id="active-visit-main">
        <section className="itinerary-hero">
          <div>
            <p className="eyebrow">{park?.name ?? t('visitPlanner')}</p>
            <h1>{t('activeVisitTitle')}</h1>
            <p className="page-description">
              {t('visitProgress', {
                completed,
                count: session.stops.length,
              })}
            </p>
          </div>
          <div className={`visit-session-status status-${session.status.toLowerCase()}`}>
            {session.status === 'Completed' ? t('visitComplete') : t('visitInProgress')}
          </div>
        </section>

        {nextStop ? (
          <NextAction
            busy={updateStop.isPending}
            currentWait={currentWait}
            formatTime={formatTime}
            isWaitLoading={waitsQuery.isPending}
            isWalkingLoading={lastCompletedStop !== null && walkingQuery.isPending}
            onAction={(action) => updateStop.mutate({
              action,
              attractionId: nextStop.attractionId,
            })}
            stop={nextStop}
            waitUnavailable={waitsQuery.isError}
            walkingMinutes={walkingMinutes}
            walkingUnavailable={
              walkingQuery.isError ||
              walkingQuery.data?.status === 'CoordinatesUnavailable' ||
              walkingQuery.data?.status === 'RouteUnavailable'
            }
          />
        ) : (
          <section className="surface next-action-complete" role="status">
            <p className="eyebrow">{t('nextAction')}</p>
            <h2>{t('allVisitStopsResolved')}</h2>
            <p>{t('allVisitStopsResolvedHelp')}</p>
          </section>
        )}

        <section className="surface itinerary-overview" aria-labelledby="visit-progress-title">
          <div className="itinerary-section-heading">
            <div>
              <p className="eyebrow">{t('persistedVisitProgress')}</p>
              <h2 id="visit-progress-title">{t('visitProgressHeading')}</h2>
            </div>
            <span>{resolved}/{session.stops.length}</span>
          </div>
          <progress
            aria-label={t('visitProgressHeading')}
            className="visit-progress"
            max={session.stops.length}
            value={resolved}
          />
          <p className="visit-progress-detail">
            {t('completedAttractions', { completed })}
          </p>
        </section>

        <section className="surface itinerary-schedule" aria-labelledby="active-schedule-title">
          <div className="itinerary-section-heading">
            <div>
              <p className="eyebrow">{t('yourSchedule')}</p>
              <h2 id="active-schedule-title">{t('trackVisit')}</h2>
            </div>
          </div>
          <ol className="itinerary-stop-list">
            {session.stops.map(stop => (
              <ActiveStop
                formatTime={formatTime}
                key={stop.attractionId}
                stop={stop}
              />
            ))}
          </ol>
        </section>

        {updateStop.isError && (
          <div className="itinerary-generation-error" role="alert">
            <div>
              <strong>{t('visitUpdateFailed')}</strong>
              <p>{updateStop.error.message}</p>
            </div>
          </div>
        )}
      </main>
    </div>
  )
}

function NextAction({
  busy,
  currentWait,
  formatTime,
  isWaitLoading,
  isWalkingLoading,
  onAction,
  stop,
  waitUnavailable,
  walkingMinutes,
  walkingUnavailable,
}: {
  busy: boolean
  currentWait: CurrentWaitTime | null
  formatTime: (value: string) => string
  isWaitLoading: boolean
  isWalkingLoading: boolean
  onAction: (action: StopAction) => void
  stop: VisitSessionStop
  waitUnavailable: boolean
  walkingMinutes: number | null
  walkingUnavailable: boolean
}) {
  const { t } = useI18n()
  const waitText = currentWait
    ? currentWait.isOpen
      ? currentWait.waitMinutes === null
        ? t('openWithoutWait')
        : t('liveWaitMinutes', { count: currentWait.waitMinutes })
      : t('closed')
    : isWaitLoading
      ? t('loadingCurrentWait')
      : t('currentWaitUnavailable')

  return (
    <section className="surface next-action-card" aria-labelledby="next-action-title">
      <div className="next-action-heading">
        <div>
          <p className="eyebrow">{t('nextAction')}</p>
          <h2 id="next-action-title">{stop.attractionName}</h2>
          <p>{t('plannedFor', { time: formatTime(stop.attractionStartsAt) })}</p>
        </div>
        <span>{t('stopNumber', { number: stop.sequence })}</span>
      </div>
      <dl className="next-action-facts">
        <div>
          <dt>{t('currentWait')}</dt>
          <dd>{waitUnavailable ? t('currentWaitUnavailable') : waitText}</dd>
        </div>
        <div>
          <dt>{t('walkingTime')}</dt>
          <dd>
            {walkingUnavailable
              ? t('walkingTimeUnavailable')
              : isWalkingLoading
                ? t('calculatingWalkingTime')
                : walkingMinutes === null
                  ? t('walkingTimeUnavailable')
                  : t('durationMinutes', { count: walkingMinutes })}
          </dd>
        </div>
      </dl>
      <div className="next-action-controls">
        <button
          className="primary-button"
          disabled={busy}
          onClick={() => onAction('complete')}
          type="button"
        >
          {t('markCompleted')}
        </button>
        <button
          className="secondary-button"
          disabled={busy}
          onClick={() => onAction('skip')}
          type="button"
        >
          {t('skipThisAttraction')}
        </button>
      </div>
    </section>
  )
}

function ActiveStop({
  formatTime,
  stop,
}: {
  formatTime: (value: string) => string
  stop: VisitSessionStop
}) {
  const { t } = useI18n()
  return (
    <li>
      <article className={`itinerary-stop visit-stop-${stop.status.toLowerCase()}`}>
        <div className="stop-sequence" aria-hidden="true">{stop.sequence}</div>
        <div className="stop-content">
          <div className="stop-heading">
            <div>
              <p>{formatTime(stop.attractionStartsAt)}</p>
              <h3>{stop.attractionName}</h3>
            </div>
            <span className="visit-stop-status">
              {t(stopStatusKey(stop.status))}
            </span>
          </div>
          <dl className="stop-phases">
            <div>
              <dt>{t('walkingTime')}</dt>
              <dd><strong>{t('durationMinutes', { count: stop.walkingMinutes })}</strong></dd>
            </div>
            <div>
              <dt>{t('queueTime')}</dt>
              <dd><strong>{t('durationMinutes', { count: stop.queueMinutes })}</strong></dd>
            </div>
          </dl>
        </div>
      </article>
    </li>
  )
}

function stopStatusKey(status: VisitSessionStop['status']) {
  if (status === 'Completed') return 'stopCompleted' as const
  if (status === 'Skipped') return 'stopSkipped' as const
  return 'stopPending' as const
}

function VisitStatus({
  action,
  message,
}: {
  action?: ReactNode
  message: string
}) {
  return (
    <main className="application-state">
      <div className="state-card">
        <strong>{message}</strong>
        {action}
      </div>
    </main>
  )
}
