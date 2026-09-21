import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { LanguageSelector, useI18n, type TranslationKey } from '../../i18n'
import {
  MAX_PARTY_SIZE,
  MIN_PARTY_SIZE,
  toLocalDateInputValue,
  toVisitDetails,
  validateVisitSetup,
  type VisitDetails,
  type VisitSetupError,
  type VisitSetupErrors,
  type VisitSetupField,
  type VisitSetupValues,
} from './visitSetupModel'

interface VisitSetupProps {
  savedDetails: VisitDetails | null
  onContinue: (details: VisitDetails) => void
}

const errorMessages: Record<VisitSetupError, TranslationKey> = {
  dateRequired: 'visitDateRequired',
  dateInvalid: 'visitDateInvalid',
  datePast: 'visitDatePast',
  arrivalRequired: 'arrivalRequired',
  arrivalInvalid: 'arrivalInvalid',
  departureRequired: 'departureRequired',
  departureInvalid: 'departureInvalid',
  departureAfterArrival: 'departureAfterArrival',
  partyRequired: 'partyRequired',
  partyWholeNumber: 'partyWholeNumber',
  partyRange: 'partyRange',
}

const fieldIds: Record<VisitSetupField, string> = {
  date: 'visit-date',
  arrivalTime: 'arrival-time',
  departureTime: 'departure-time',
  partySize: 'party-size',
}

function initialValues(savedDetails: VisitDetails | null): VisitSetupValues {
  if (savedDetails) {
    return {
      date: savedDetails.date,
      arrivalTime: savedDetails.arrivalTime,
      departureTime: savedDetails.departureTime,
      partySize: String(savedDetails.partySize),
    }
  }

  return { date: '', arrivalTime: '09:00', departureTime: '18:00', partySize: '2' }
}

export function VisitSetup({ savedDetails, onContinue }: VisitSetupProps) {
  const { locale, t } = useI18n()
  const navigate = useNavigate()
  const readyHeadingRef = useRef<HTMLHeadingElement>(null)
  const [values, setValues] = useState<VisitSetupValues>(() => initialValues(savedDetails))
  const [errors, setErrors] = useState<VisitSetupErrors>({})
  const [touched, setTouched] = useState<Partial<Record<VisitSetupField, boolean>>>({})
  const [confirmed, setConfirmed] = useState(savedDetails !== null)
  const minDate = toLocalDateInputValue(new Date())
  const details = confirmed ? toVisitDetails(values) : null

  useEffect(() => {
    if (savedDetails) readyHeadingRef.current?.focus()
  }, [savedDetails])

  const messageFor = (field: VisitSetupField) => {
    const error = errors[field]
    return error && touched[field] ? t(errorMessages[error], { min: MIN_PARTY_SIZE, max: MAX_PARTY_SIZE }) : undefined
  }

  const updateValue = (field: VisitSetupField, value: string) => {
    const nextValues = { ...values, [field]: value }
    setValues(nextValues)
    if (Object.keys(errors).length > 0) setErrors(validateVisitSetup(nextValues))
  }

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    const nextErrors = validateVisitSetup(values)
    setErrors(nextErrors)
    setTouched({ date: true, arrivalTime: true, departureTime: true, partySize: true })

    const firstInvalidField = (Object.keys(nextErrors) as VisitSetupField[])[0]
    if (firstInvalidField) {
      requestAnimationFrame(() => form.querySelector<HTMLElement>(`#${fieldIds[firstInvalidField]}`)?.focus())
      return
    }

    onContinue(toVisitDetails(values))
    navigate('/visit/priorities')
  }

  const adjustPartySize = (change: number) => {
    const current = Number(values.partySize)
    const fallback = change > 0 ? MIN_PARTY_SIZE : MAX_PARTY_SIZE
    const next = Number.isInteger(current) ? current + change : fallback
    updateValue('partySize', String(Math.min(MAX_PARTY_SIZE, Math.max(MIN_PARTY_SIZE, next))))
  }

  return (
    <div className="application-shell visit-shell">
      <title>{t('visitSetupTitle')} · Park Pilot</title>
      <a className="skip-link" href="#visit-main">{t('skipToContent')}</a>
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

      <main className="visit-main" id="visit-main">
        <section className="visit-introduction" aria-labelledby="visit-page-title">
          <div>
            <p className="eyebrow">{t('visitSetupEyebrow')}</p>
            <h1 id="visit-page-title">{t('visitSetupTitle')}</h1>
            <p className="page-description">{t('visitSetupDescription')}</p>
          </div>
          <ol className="setup-progress" aria-label={t('planningProgress')}>
            <li className="active" aria-current="step"><span>1</span>{t('visitDetailsStep')}</li>
            <li><span>2</span>{t('prioritiesStep')}</li>
          </ol>
          <aside className="visit-note">
            <span aria-hidden="true">✦</span>
            <div>
              <strong>{t('visitTipTitle')}</strong>
              <p>{t('visitTip')}</p>
            </div>
          </aside>
        </section>

        {details ? (
          <section className="surface visit-card visit-ready" aria-labelledby="ready-title">
            <div className="ready-icon" aria-hidden="true">✓</div>
            <p className="eyebrow">{t('detailsReadyEyebrow')}</p>
            <h2 id="ready-title" ref={readyHeadingRef} tabIndex={-1}>{t('detailsReadyTitle')}</h2>
            <p className="ready-description">{t('detailsReadyDescription')}</p>
            <dl className="visit-summary" aria-label={t('visitSummary')}>
              <div>
                <dt>{t('visitDate')}</dt>
                <dd>{formatVisitDate(details.date, locale)}</dd>
              </div>
              <div>
                <dt>{t('visitWindow')}</dt>
                <dd>{formatVisitTime(details.arrivalTime, locale)} – {formatVisitTime(details.departureTime, locale)}</dd>
              </div>
              <div>
                <dt>{t('partySize')}</dt>
                <dd>{t('peopleCount', { count: details.partySize })}</dd>
              </div>
            </dl>
            <div className="visit-actions">
              <Link className="primary-button visit-primary-button button-link" to="/visit/priorities">
                {t('chooseAttractionPriorities')}<span aria-hidden="true">→</span>
              </Link>
              <button
                className="secondary-button button-link"
                onClick={() => {
                  setConfirmed(false)
                  requestAnimationFrame(() => document.getElementById('visit-date')?.focus())
                }}
                type="button"
              >
                {t('editVisitDetails')}
              </button>
            </div>
          </section>
        ) : (
          <section className="surface visit-card" aria-labelledby="schedule-title">
            <div className="visit-card-heading">
              <p className="eyebrow">{t('visitDetailsStep')}</p>
              <h2 id="schedule-title">{t('scheduleTitle')}</h2>
              <p>{t('scheduleDescription')}</p>
            </div>

            {Object.keys(errors).length > 0 && (
              <div className="form-error-summary" role="alert" aria-labelledby="form-errors-title">
                <strong id="form-errors-title">{t('formErrorTitle')}</strong>
                <ul>
                  {(Object.keys(errors) as VisitSetupField[]).map((field) => (
                    <li key={field}><a href={`#${fieldIds[field]}`}>{t(errorMessages[errors[field]!], { min: MIN_PARTY_SIZE, max: MAX_PARTY_SIZE })}</a></li>
                  ))}
                </ul>
              </div>
            )}

            <form className="visit-form" noValidate onSubmit={handleSubmit}>
              <div className="visit-field visit-date-field">
                <label htmlFor="visit-date">{t('visitDate')}</label>
                <input
                  aria-describedby={`visit-date-help${messageFor('date') ? ' visit-date-error' : ''}`}
                  aria-invalid={messageFor('date') ? true : undefined}
                  id="visit-date"
                  min={minDate}
                  onBlur={() => {
                    setTouched((current) => ({ ...current, date: true }))
                    setErrors(validateVisitSetup(values))
                  }}
                  onChange={(event) => updateValue('date', event.target.value)}
                  required
                  type="date"
                  value={values.date}
                />
                <span className="field-help" id="visit-date-help">{t('visitDateHelp')}</span>
                {messageFor('date') && <span className="field-error" id="visit-date-error">{messageFor('date')}</span>}
              </div>

              <div className="visit-time-grid">
                <div className="visit-field">
                  <label htmlFor="arrival-time">{t('arrivalTime')}</label>
                  <input
                    aria-describedby={`arrival-time-help${messageFor('arrivalTime') ? ' arrival-time-error' : ''}`}
                    aria-invalid={messageFor('arrivalTime') ? true : undefined}
                    id="arrival-time"
                    onBlur={() => {
                      setTouched((current) => ({ ...current, arrivalTime: true }))
                      setErrors(validateVisitSetup(values))
                    }}
                    onChange={(event) => updateValue('arrivalTime', event.target.value)}
                    required
                    step="900"
                    type="time"
                    value={values.arrivalTime}
                  />
                  <span className="field-help" id="arrival-time-help">{t('arrivalTimeHelp')}</span>
                  {messageFor('arrivalTime') && <span className="field-error" id="arrival-time-error">{messageFor('arrivalTime')}</span>}
                </div>

                <div className="visit-field">
                  <label htmlFor="departure-time">{t('departureTime')}</label>
                  <input
                    aria-describedby={`departure-time-help${messageFor('departureTime') ? ' departure-time-error' : ''}`}
                    aria-invalid={messageFor('departureTime') ? true : undefined}
                    id="departure-time"
                    onBlur={() => {
                      setTouched((current) => ({ ...current, departureTime: true }))
                      setErrors(validateVisitSetup(values))
                    }}
                    onChange={(event) => updateValue('departureTime', event.target.value)}
                    required
                    step="900"
                    type="time"
                    value={values.departureTime}
                  />
                  <span className="field-help" id="departure-time-help">{t('departureTimeHelp')}</span>
                  {messageFor('departureTime') && <span className="field-error" id="departure-time-error">{messageFor('departureTime')}</span>}
                </div>
              </div>

              <div className="visit-field party-field">
                <label htmlFor="party-size">{t('partySize')}</label>
                <div className="party-stepper">
                  <button
                    aria-label={t('decreaseParty')}
                    disabled={Number(values.partySize) <= MIN_PARTY_SIZE}
                    onClick={() => adjustPartySize(-1)}
                    type="button"
                  >−</button>
                  <input
                    aria-describedby={`party-size-help${messageFor('partySize') ? ' party-size-error' : ''}`}
                    aria-invalid={messageFor('partySize') ? true : undefined}
                    id="party-size"
                    inputMode="numeric"
                    max={MAX_PARTY_SIZE}
                    min={MIN_PARTY_SIZE}
                    onBlur={() => {
                      setTouched((current) => ({ ...current, partySize: true }))
                      setErrors(validateVisitSetup(values))
                    }}
                    onChange={(event) => updateValue('partySize', event.target.value)}
                    required
                    step="1"
                    type="number"
                    value={values.partySize}
                  />
                  <button
                    aria-label={t('increaseParty')}
                    disabled={Number(values.partySize) >= MAX_PARTY_SIZE}
                    onClick={() => adjustPartySize(1)}
                    type="button"
                  >+</button>
                </div>
                <span className="field-help" id="party-size-help">{t('partySizeHelp', { min: MIN_PARTY_SIZE, max: MAX_PARTY_SIZE })}</span>
                {messageFor('partySize') && <span className="field-error" id="party-size-error">{messageFor('partySize')}</span>}
              </div>

              <div className="visit-form-footer">
                <p><span aria-hidden="true">●</span>{t('visitNotSaved')}</p>
                <button className="primary-button visit-primary-button" type="submit">
                  {t('continueToPriorities')}<span aria-hidden="true">→</span>
                </button>
              </div>
            </form>
          </section>
        )}
      </main>
    </div>
  )
}

function formatVisitDate(value: string, locale: string): string {
  const [year, month, day] = value.split('-').map(Number)
  return new Intl.DateTimeFormat(locale, { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' }).format(new Date(year, month - 1, day, 12))
}

function formatVisitTime(value: string, locale: string): string {
  const [hours, minutes] = value.split(':').map(Number)
  return new Intl.DateTimeFormat(locale, { hour: 'numeric', minute: '2-digit' }).format(new Date(2000, 0, 1, hours, minutes))
}

function VisitMark() {
  return (
    <svg aria-hidden="true" className="brand-mark" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <path d="M16 2.5a10 10 0 0 0-10 10c0 7.4 10 17 10 17s10-9.6 10-17a10 10 0 0 0-10-10Z" fill="currentColor" />
      <circle cx="16" cy="12.5" r="4" fill="white" />
    </svg>
  )
}
