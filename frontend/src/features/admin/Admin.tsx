import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import {
  collectAdminPark,
  createAdminAttraction,
  createAdminLand,
  createAdminObservation,
  createAdminPark,
  getAdminAttractions,
  getAdminCollectionRuns,
  getAdminLands,
  getAdminObservations,
  getAdminParks,
  purgeAdminObservations,
  retryAdminCollectionRun,
  saveAdminAttraction,
  saveAdminLand,
  saveAdminPark,
  setAdminObservationValidity,
} from '../../api/client'
import type {
  AdminAttraction,
  AdminLand,
  AdminPark,
  SaveAdminAttraction,
  SaveAdminLand,
  SaveAdminPark,
} from '../../api/contracts'
import { LanguageSelector, useI18n } from '../../i18n'

type AdminTab = 'operations' | 'catalog' | 'observations' | 'runs'

export function Admin() {
  const { t } = useI18n()
  const [activeTab, setActiveTab] = useState<AdminTab>('operations')
  const [selectedParkId, setSelectedParkId] = useState<number>()
  const [creatingPark, setCreatingPark] = useState(false)
  const parksQuery = useQuery({
    queryKey: ['admin', 'parks'],
    queryFn: ({ signal }) => getAdminParks(signal),
    refetchInterval: 30_000,
  })

  useEffect(() => {
    if (!selectedParkId && parksQuery.data?.length) {
      setSelectedParkId(parksQuery.data[0].id)
    }
  }, [parksQuery.data, selectedParkId])

  const selectedPark = parksQuery.data?.find((park) => park.id === selectedParkId)

  return (
    <div className="application-shell admin-shell">
      <header className="topbar">
        <Link className="brand" to="/">
          <AdminMark />
          <span>
            <strong>{t('brand')}</strong>
            <small>{t('administration')}</small>
          </span>
        </Link>
        <div className="topbar-actions">
          <LanguageSelector />
          <Link className="topbar-link" to="/">{t('visitorDashboard')}</Link>
        </div>
      </header>

      <main className="admin-main">
        <header className="admin-header">
          <div>
            <p className="eyebrow">{t('operationsTooling')}</p>
            <h1>{t('administration')}</h1>
            <p className="page-description">
              {t('adminDescription')}
            </p>
          </div>
          <div className="admin-park-controls">
            <label className="park-selector" htmlFor="admin-park-selector">
              <span>{t('workingPark')}</span>
              <select
                id="admin-park-selector"
                value={selectedParkId ?? ''}
                onChange={(event) => {
                  setSelectedParkId(Number(event.target.value))
                  setCreatingPark(false)
                }}
              >
                {parksQuery.data?.map((park) => (
                  <option key={park.id} value={park.id}>
                    {park.name}
                  </option>
                ))}
              </select>
            </label>
            <button
              className="secondary-button"
              onClick={() => {
                setActiveTab('operations')
                setCreatingPark(true)
              }}
              type="button"
            >
              {t('addPark')}
            </button>
          </div>
        </header>

        <nav className="admin-tabs" aria-label={t('adminSections')}>
          {(
            [
              ['operations', t('operations')],
              ['catalog', t('catalog')],
              ['observations', t('observations')],
              ['runs', t('collectionRuns')],
            ] as const
          ).map(([value, label]) => (
            <button
              className={activeTab === value ? 'active' : undefined}
              key={value}
              onClick={() => setActiveTab(value)}
              type="button"
            >
              {label}
            </button>
          ))}
        </nav>

        {parksQuery.isLoading && <AdminStatus message={t('loadingAdmin')} />}
        {parksQuery.isError && (
          <AdminStatus message={t('adminUnavailable')} error />
        )}
        {creatingPark && activeTab === 'operations' && (
          <CreateParkPanel
            onCreated={(parkId) => {
              setSelectedParkId(parkId)
              setCreatingPark(false)
            }}
          />
        )}
        {!creatingPark && selectedPark && activeTab === 'operations' && (
          <OperationsPanel park={selectedPark} />
        )}
        {selectedPark && activeTab === 'catalog' && (
          <CatalogPanel park={selectedPark} />
        )}
        {selectedPark && activeTab === 'observations' && (
          <ObservationsPanel park={selectedPark} />
        )}
        {selectedPark && activeTab === 'runs' && <RunsPanel park={selectedPark} />}
      </main>
    </div>
  )
}

function CreateParkPanel({ onCreated }: { onCreated: (parkId: number) => void }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [form, setForm] = useState<SaveAdminPark>({
    sourceParkId: 0,
    name: '',
    timezone: 'America/New_York',
    isActive: true,
    collectionEnabled: true,
    collectionIntervalMinutes: 5,
  })
  const mutation = useMutation({
    mutationFn: () => createAdminPark(form),
    onSuccess: async (park) => {
      await queryClient.invalidateQueries({ queryKey: ['admin', 'parks'] })
      onCreated(park.id)
    },
  })

  return (
    <article className="surface admin-card admin-create-card">
      <AdminCardHeading
        eyebrow={t('catalogSetup')}
        title={t('addSupportedPark')}
        detail={t('externalQueueId')}
      />
      <form
        className="admin-form"
        onSubmit={(event) => {
          event.preventDefault()
          mutation.mutate()
        }}
      >
        <div className="form-grid two-columns">
          <Field label={t('parkName')}>
            <input
              required
              value={form.name}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
            />
          </Field>
          <Field label={t('externalParkId')}>
            <input
              min="1"
              required
              type="number"
              value={form.sourceParkId || ''}
              onChange={(event) =>
                setForm({ ...form, sourceParkId: Number(event.target.value) })
              }
            />
          </Field>
          <Field label={t('timezone')}>
            <input
              required
              value={form.timezone}
              onChange={(event) =>
                setForm({ ...form, timezone: event.target.value })
              }
            />
          </Field>
          <Field label={t('collectionInterval')}>
            <div className="input-suffix">
              <input
                max="1440"
                min="1"
                required
                type="number"
                value={form.collectionIntervalMinutes}
                onChange={(event) =>
                  setForm({
                    ...form,
                    collectionIntervalMinutes: Number(event.target.value),
                  })
                }
              />
              <span>{t('minutesWord')}</span>
            </div>
          </Field>
        </div>
        <div className="toggle-row">
          <Toggle
            checked={form.collectionEnabled}
            label={t('startAutomatic')}
            onChange={(collectionEnabled) =>
              setForm({ ...form, collectionEnabled })
            }
          />
          <Toggle
            checked={form.isActive}
            label={t('parkActive')}
            onChange={(isActive) => setForm({ ...form, isActive })}
          />
        </div>
        <button
          className="primary-button"
          disabled={mutation.isPending}
          type="submit"
        >
          {mutation.isPending ? t('addingPark') : t('addPark')}
        </button>
        <MutationNotice error={mutation.error} />
      </form>
    </article>
  )
}

function OperationsPanel({ park }: { park: AdminPark }) {
  const { locale, t } = useI18n()
  const queryClient = useQueryClient()
  const [form, setForm] = useState<SaveAdminPark>(() => parkToForm(park))

  useEffect(() => {
    setForm(parkToForm(park))
  }, [park])

  const saveMutation = useMutation({
    mutationFn: () => saveAdminPark(park.id, form),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin', 'parks'] })
    },
  })
  const collectMutation = useMutation({
    mutationFn: () => collectAdminPark(park.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin'] })
    },
  })

  return (
    <section className="admin-grid">
      <article className="surface admin-card">
        <AdminCardHeading
          eyebrow={t('collectionHealth')}
          title={park.name}
          detail={
            park.lastCollectionSucceeded === false
              ? t('attentionRequired')
              : park.collectionEnabled
                ? t('collectionActive')
                : t('collectionPaused')
          }
        />
        <div className="admin-metrics">
          <AdminMetric label={t('attractions')} value={new Intl.NumberFormat(locale).format(park.attractionCount)} />
          <AdminMetric
            label={t('observations')}
            value={new Intl.NumberFormat(locale).format(park.observationCount)}
          />
          <AdminMetric
            label={t('lastAttempt')}
            value={formatDateTime(park.lastCollectionStartedAt, locale, t('never'))}
          />
          <AdminMetric
            label={t('lastResult')}
            value={
              park.lastCollectionSucceeded === null
                ? t('noRuns')
                : park.lastCollectionSucceeded
                  ? t('successful')
                  : t('failed')
            }
          />
        </div>
        {park.lastCollectionError && (
          <div className="admin-alert error">{park.lastCollectionError}</div>
        )}
        <div className="admin-card-actions">
          <button
            className="primary-button"
            disabled={collectMutation.isPending || !park.isActive}
            onClick={() => collectMutation.mutate()}
            type="button"
          >
            {collectMutation.isPending ? t('collecting') : t('collectNow')}
          </button>
          <span className="field-help">
            {t('collectHelp')}
          </span>
        </div>
        <MutationNotice
          error={collectMutation.error}
          message={collectMutation.isSuccess ? t('collectionSuccess') : undefined}
        />
      </article>

      <article className="surface admin-card">
        <AdminCardHeading eyebrow={t('configuration')} title={t('parkSettings')} />
        <form
          className="admin-form"
          onSubmit={(event) => {
            event.preventDefault()
            saveMutation.mutate()
          }}
        >
          <div className="form-grid two-columns">
            <Field label={t('parkName')}>
              <input
                required
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
              />
            </Field>
            <Field label={t('externalParkId')}>
              <input
                min="1"
                required
                type="number"
                value={form.sourceParkId}
                onChange={(event) =>
                  setForm({ ...form, sourceParkId: Number(event.target.value) })
                }
              />
            </Field>
            <Field label={t('timezone')}>
              <input
                required
                value={form.timezone}
                onChange={(event) =>
                  setForm({ ...form, timezone: event.target.value })
                }
              />
            </Field>
            <Field label={t('collectionInterval')}>
              <div className="input-suffix">
                <input
                  max="1440"
                  min="1"
                  required
                  type="number"
                  value={form.collectionIntervalMinutes}
                  onChange={(event) =>
                    setForm({
                      ...form,
                      collectionIntervalMinutes: Number(event.target.value),
                    })
                  }
                />
                <span>{t('minutesWord')}</span>
              </div>
            </Field>
          </div>
          <div className="toggle-row">
            <Toggle
              checked={form.collectionEnabled}
              label={t('automaticCollection')}
              onChange={(collectionEnabled) =>
                setForm({ ...form, collectionEnabled })
              }
            />
            <Toggle
              checked={form.isActive}
              label={t('parkActive')}
              onChange={(isActive) => setForm({ ...form, isActive })}
            />
          </div>
          <button
            className="primary-button"
            disabled={saveMutation.isPending}
            type="submit"
          >
            {saveMutation.isPending ? t('saving') : t('saveSettings')}
          </button>
          <MutationNotice
            error={saveMutation.error}
            message={saveMutation.isSuccess ? t('parkSaved') : undefined}
          />
        </form>
      </article>
    </section>
  )
}

function CatalogPanel({ park }: { park: AdminPark }) {
  const landsQuery = useQuery({
    queryKey: ['admin', 'lands', park.id],
    queryFn: ({ signal }) => getAdminLands(park.id, signal),
  })
  const attractionsQuery = useQuery({
    queryKey: ['admin', 'attractions', park.id],
    queryFn: ({ signal }) => getAdminAttractions(park.id, signal),
  })

  return (
    <section className="admin-stack">
      <LandEditor park={park} lands={landsQuery.data ?? []} />
      <AttractionEditor
        attractions={attractionsQuery.data ?? []}
        lands={landsQuery.data ?? []}
        park={park}
      />
    </section>
  )
}

function LandEditor({ park, lands }: { park: AdminPark; lands: AdminLand[] }) {
  const { locale, t } = useI18n()
  const queryClient = useQueryClient()
  const [selectedId, setSelectedId] = useState<number>()
  const selectedLand = lands.find((land) => land.id === selectedId)
  const [form, setForm] = useState<SaveAdminLand>(() => emptyLand(park.id))

  useEffect(() => {
    setSelectedId(undefined)
    setForm(emptyLand(park.id))
  }, [park.id])

  useEffect(() => {
    setForm(selectedLand ? landToForm(selectedLand) : emptyLand(park.id))
  }, [park.id, selectedLand])

  const mutation = useMutation({
    mutationFn: () =>
      selectedLand
        ? saveAdminLand(selectedLand.id, form)
        : createAdminLand(form),
    onSuccess: async (land) => {
      setSelectedId(land.id)
      await queryClient.invalidateQueries({
        queryKey: ['admin', 'lands', park.id],
      })
    },
  })

  return (
    <article className="surface admin-card catalog-editor">
      <AdminCardHeading
        eyebrow={t('parkStructure')}
        title={t('lands')}
        detail={t('configured', { count: new Intl.NumberFormat(locale).format(lands.length) })}
      />
      <div className="catalog-layout">
        <div className="catalog-list" role="list">
          <button
            className={!selectedLand ? 'selected' : undefined}
            onClick={() => setSelectedId(undefined)}
            type="button"
          >
            <strong>{t('addLandPlus')}</strong>
            <small>{t('manualCatalogEntry')}</small>
          </button>
          {lands.map((land) => (
            <button
              className={selectedId === land.id ? 'selected' : undefined}
              key={land.id}
              onClick={() => setSelectedId(land.id)}
              type="button"
            >
              <strong>{land.name}</strong>
              <small>
                {t('source')} {new Intl.NumberFormat(locale).format(land.sourceLandId)} · {land.isActive ? t('active') : t('inactive')}
              </small>
            </button>
          ))}
        </div>
        <form
          className="admin-form"
          onSubmit={(event) => {
            event.preventDefault()
            mutation.mutate()
          }}
        >
          <Field label={t('landName')}>
            <input
              required
              value={form.name}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
            />
          </Field>
          <Field label={t('externalLandId')}>
            <input
              min="1"
              required
              type="number"
              value={form.sourceLandId || ''}
              onChange={(event) =>
                setForm({ ...form, sourceLandId: Number(event.target.value) })
              }
            />
          </Field>
          <Toggle
            checked={form.isActive}
            label={t('landActive')}
            onChange={(isActive) => setForm({ ...form, isActive })}
          />
          <button
            className="primary-button"
            disabled={mutation.isPending}
            type="submit"
          >
            {selectedLand ? t('saveLand') : t('addLand')}
          </button>
          <MutationNotice error={mutation.error} />
        </form>
      </div>
    </article>
  )
}

function AttractionEditor({
  park,
  lands,
  attractions,
}: {
  park: AdminPark
  lands: AdminLand[]
  attractions: AdminAttraction[]
}) {
  const { locale, t } = useI18n()
  const queryClient = useQueryClient()
  const [selectedId, setSelectedId] = useState<number>()
  const [search, setSearch] = useState('')
  const selectedAttraction = attractions.find(
    (attraction) => attraction.id === selectedId,
  )
  const [form, setForm] = useState<SaveAdminAttraction>(() =>
    emptyAttraction(park.id),
  )
  const filteredAttractions = useMemo(() => {
    const normalizedSearch = search.trim().toLocaleLowerCase(locale)
    return attractions.filter(
      (attraction) =>
        !normalizedSearch ||
        attraction.name.toLocaleLowerCase(locale).includes(normalizedSearch),
    )
  }, [attractions, locale, search])

  useEffect(() => {
    setSelectedId(undefined)
    setForm(emptyAttraction(park.id))
  }, [park.id])

  useEffect(() => {
    setForm(
      selectedAttraction
        ? attractionToForm(selectedAttraction)
        : emptyAttraction(park.id),
    )
  }, [park.id, selectedAttraction])

  const mutation = useMutation({
    mutationFn: () =>
      selectedAttraction
        ? saveAdminAttraction(selectedAttraction.id, form)
        : createAdminAttraction(form),
    onSuccess: async (attraction) => {
      setSelectedId(attraction.id)
      await queryClient.invalidateQueries({
        queryKey: ['admin', 'attractions', park.id],
      })
    },
  })

  return (
    <article className="surface admin-card catalog-editor">
      <AdminCardHeading
        eyebrow={t('queueCatalog')}
        title={t('attractions')}
        detail={t('configured', { count: new Intl.NumberFormat(locale).format(attractions.length) })}
      />
      <div className="catalog-layout">
        <div>
          <input
            className="catalog-search"
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t('searchAttractions')}
            type="search"
            value={search}
          />
          <div className="catalog-list tall" role="list">
            <button
              className={!selectedAttraction ? 'selected' : undefined}
              onClick={() => setSelectedId(undefined)}
              type="button"
            >
              <strong>{t('addAttractionPlus')}</strong>
              <small>{t('manualCatalogEntry')}</small>
            </button>
            {filteredAttractions.map((attraction) => (
              <button
                className={selectedId === attraction.id ? 'selected' : undefined}
                key={attraction.id}
                onClick={() => setSelectedId(attraction.id)}
                type="button"
              >
                <strong>{attraction.name}</strong>
                <small>
                  {attraction.landName ?? t('parkWide')} ·{' '}
                  {attraction.isActive ? t('active') : t('inactive')}
                </small>
              </button>
            ))}
          </div>
        </div>
        <form
          className="admin-form"
          onSubmit={(event) => {
            event.preventDefault()
            mutation.mutate()
          }}
        >
          <div className="form-grid two-columns">
            <Field label={t('attractionName')}>
              <input
                required
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
              />
            </Field>
            <Field label={t('externalAttractionId')}>
              <input
                min="1"
                required
                type="number"
                value={form.sourceRideId || ''}
                onChange={(event) =>
                  setForm({ ...form, sourceRideId: Number(event.target.value) })
                }
              />
            </Field>
            <Field label={t('land')}>
              <select
                value={form.currentLandId ?? ''}
                onChange={(event) =>
                  setForm({
                    ...form,
                    currentLandId: event.target.value
                      ? Number(event.target.value)
                      : null,
                  })
                }
              >
                <option value="">{t('parkWide')}</option>
                {lands.map((land) => (
                  <option key={land.id} value={land.id}>
                    {land.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label={t('duration')}>
              <div className="input-suffix">
                <input
                  min="1"
                  type="number"
                  value={form.durationMinutes ?? ''}
                  onChange={(event) =>
                    setForm({
                      ...form,
                      durationMinutes: numberOrNull(event.target.value),
                    })
                  }
                />
                <span>{t('minutesWord')}</span>
              </div>
            </Field>
            <Field label={t('latitude')}>
              <input
                max="90"
                min="-90"
                step="any"
                type="number"
                value={form.latitude ?? ''}
                onChange={(event) =>
                  setForm({ ...form, latitude: numberOrNull(event.target.value) })
                }
              />
            </Field>
            <Field label={t('longitude')}>
              <input
                max="180"
                min="-180"
                step="any"
                type="number"
                value={form.longitude ?? ''}
                onChange={(event) =>
                  setForm({ ...form, longitude: numberOrNull(event.target.value) })
                }
              />
            </Field>
          </div>
          <Toggle
            checked={form.isActive}
            label={t('attractionActive')}
            onChange={(isActive) => setForm({ ...form, isActive })}
          />
          <button
            className="primary-button"
            disabled={mutation.isPending}
            type="submit"
          >
            {selectedAttraction ? t('saveAttraction') : t('addAttraction')}
          </button>
          <MutationNotice error={mutation.error} />
        </form>
      </div>
    </article>
  )
}

function ObservationsPanel({ park }: { park: AdminPark }) {
  const { locale, t } = useI18n()
  const queryClient = useQueryClient()
  const attractionsQuery = useQuery({
    queryKey: ['admin', 'attractions', park.id],
    queryFn: ({ signal }) => getAdminAttractions(park.id, signal),
  })
  const [attractionId, setAttractionId] = useState<number>()
  const [observedAt, setObservedAt] = useState(localDateTimeValue())
  const [isOpen, setIsOpen] = useState(true)
  const [waitMinutes, setWaitMinutes] = useState(0)
  const [invalidatingId, setInvalidatingId] = useState<number>()
  const [invalidReason, setInvalidReason] = useState('')
  const observationsQuery = useQuery({
    queryKey: ['admin', 'observations', park.id, attractionId],
    queryFn: ({ signal }) =>
      getAdminObservations(park.id, attractionId, signal),
  })

  useEffect(() => {
    setAttractionId(undefined)
    setObservedAt(localDateTimeValue())
  }, [park.id])

  const createMutation = useMutation({
    mutationFn: () =>
      createAdminObservation({
        attractionId: attractionId!,
        observedAt: new Date(observedAt).toISOString(),
        isOpen,
        waitMinutes: isOpen ? waitMinutes : null,
      }),
    onSuccess: async () => {
      setObservedAt(localDateTimeValue())
      await queryClient.invalidateQueries({
        queryKey: ['admin', 'observations', park.id],
      })
    },
  })
  const validityMutation = useMutation({
    mutationFn: ({
      observationId,
      valid,
      reason,
    }: {
      observationId: number
      valid: boolean
      reason?: string
    }) => setAdminObservationValidity(observationId, valid, reason),
    onSuccess: async () => {
      setInvalidatingId(undefined)
      setInvalidReason('')
      await queryClient.invalidateQueries({
        queryKey: ['admin', 'observations', park.id],
      })
    },
  })

  return (
    <section className="admin-stack">
      <article className="surface admin-card">
        <AdminCardHeading
          eyebrow={t('lastResort')}
          title={t('enterObservation')}
          detail={t('auditableRun')}
        />
        <form
          className="admin-form"
          onSubmit={(event) => {
            event.preventDefault()
            createMutation.mutate()
          }}
        >
          <div className="form-grid four-columns">
            <Field label={t('attraction')}>
              <select
                required
                value={attractionId ?? ''}
                onChange={(event) => setAttractionId(Number(event.target.value))}
              >
                <option disabled value="">{t('selectAttractionOption')}</option>
                {attractionsQuery.data?.map((attraction) => (
                  <option key={attraction.id} value={attraction.id}>
                    {attraction.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label={t('observedAt')}>
              <input
                required
                type="datetime-local"
                value={observedAt}
                onChange={(event) => setObservedAt(event.target.value)}
              />
            </Field>
            <Field label={t('status')}>
              <select
                value={isOpen ? 'open' : 'closed'}
                onChange={(event) => setIsOpen(event.target.value === 'open')}
              >
                <option value="open">{t('open')}</option>
                <option value="closed">{t('closed')}</option>
              </select>
            </Field>
            <Field label={t('waitTime')}>
              <div className="input-suffix">
                <input
                  disabled={!isOpen}
                  min="0"
                  required={isOpen}
                  type="number"
                  value={waitMinutes}
                  onChange={(event) => setWaitMinutes(Number(event.target.value))}
                />
                <span>{t('minutesWord')}</span>
              </div>
            </Field>
          </div>
          <div className="admin-card-actions">
            <button
              className="primary-button"
              disabled={!attractionId || createMutation.isPending}
              type="submit"
            >
              {createMutation.isPending ? t('saving') : t('saveObservation')}
            </button>
            <span className="field-help">
              {t('manualHelp')}
            </span>
          </div>
          <MutationNotice
            error={createMutation.error}
            message={
              createMutation.isSuccess
                ? t('manualSaved')
                : undefined
            }
          />
        </form>
      </article>

      <article className="surface admin-card">
        <AdminCardHeading
          eyebrow={t('dataReview')}
          title={t('recentObservations')}
          detail={t('latestRecords')}
        />
        <div className="admin-toolbar">
          <label>
            <span>{t('filterAttraction')}</span>
            <select
              value={attractionId ?? ''}
              onChange={(event) =>
                setAttractionId(
                  event.target.value ? Number(event.target.value) : undefined,
                )
              }
            >
              <option value="">{t('allAttractions')}</option>
              {attractionsQuery.data?.map((attraction) => (
                <option key={attraction.id} value={attraction.id}>
                  {attraction.name}
                </option>
              ))}
            </select>
          </label>
        </div>
        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead>
              <tr>
                <th>{t('attraction')}</th>
                <th>{t('observed')}</th>
                <th>{t('queue')}</th>
                <th>{t('source')}</th>
                <th>{t('status')}</th>
                <th aria-label={t('actions')} />
              </tr>
            </thead>
            <tbody>
              {observationsQuery.data?.map((observation) => (
                <tr className={!observation.isValid ? 'invalid' : undefined} key={observation.id}>
                  <td>
                    <strong>{observation.attractionName}</strong>
                    <small>{observation.landName ?? t('parkWide')}</small>
                  </td>
                  <td>{formatDateTime(observation.observedAt, locale, t('never'))}</td>
                  <td>
                    {observation.isOpen
                      ? `${new Intl.NumberFormat(locale).format(observation.waitMinutes ?? 0)} ${t('min')}`
                      : t('closed')}
                  </td>
                  <td>
                    <span className="source-badge">{observation.triggerSource}</span>
                  </td>
                  <td>
                    {observation.isValid
                      ? t('valid')
                      : t('invalid', { reason: observation.invalidReason ?? '' })}
                  </td>
                  <td>
                    {observation.isValid ? (
                      <button
                        className="text-button danger"
                        onClick={() => setInvalidatingId(observation.id)}
                        type="button"
                      >
                        {t('invalidate')}
                      </button>
                    ) : (
                      <button
                        className="text-button"
                        onClick={() =>
                          validityMutation.mutate({
                            observationId: observation.id,
                            valid: true,
                          })
                        }
                        type="button"
                      >
                        {t('restore')}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {invalidatingId && (
          <form
            className="inline-confirmation"
            onSubmit={(event) => {
              event.preventDefault()
              validityMutation.mutate({
                observationId: invalidatingId,
                valid: false,
                reason: invalidReason,
              })
            }}
          >
            <Field label={t('invalidReason')}>
              <input
                autoFocus
                required
                value={invalidReason}
                onChange={(event) => setInvalidReason(event.target.value)}
              />
            </Field>
            <button className="danger-button" type="submit">
              {t('confirmInvalidation')}
            </button>
            <button
              className="secondary-button"
              onClick={() => setInvalidatingId(undefined)}
              type="button"
            >
              {t('cancel')}
            </button>
          </form>
        )}
        <MutationNotice error={validityMutation.error} />
      </article>

      <PurgePanel
        attractions={attractionsQuery.data ?? []}
        park={park}
      />
    </section>
  )
}

function PurgePanel({
  park,
  attractions,
}: {
  park: AdminPark
  attractions: AdminAttraction[]
}) {
  const { locale, t } = useI18n()
  const queryClient = useQueryClient()
  const [attractionId, setAttractionId] = useState<number | null>(null)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const mutation = useMutation({
    mutationFn: () =>
      purgeAdminObservations({
        parkId: park.id,
        attractionId,
        from: new Date(from).toISOString(),
        to: new Date(to).toISOString(),
        confirmation,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ['admin', 'observations', park.id],
      })
    },
  })

  return (
    <details className="surface danger-zone">
      <summary>{t('permanentDeletion')}</summary>
      <div className="danger-zone-content">
        <p>
          {t('deletionHelp')}
        </p>
        <form
          className="admin-form"
          onSubmit={(event) => {
            event.preventDefault()
            mutation.mutate()
          }}
        >
          <div className="form-grid four-columns">
            <Field label={t('attraction')}>
              <select
                value={attractionId ?? ''}
                onChange={(event) =>
                  setAttractionId(
                    event.target.value ? Number(event.target.value) : null,
                  )
                }
              >
                <option value="">{t('allAttractions')}</option>
                {attractions.map((attraction) => (
                  <option key={attraction.id} value={attraction.id}>
                    {attraction.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label={t('from')}>
              <input
                required
                type="datetime-local"
                value={from}
                onChange={(event) => setFrom(event.target.value)}
              />
            </Field>
            <Field label={t('to')}>
              <input
                required
                type="datetime-local"
                value={to}
                onChange={(event) => setTo(event.target.value)}
              />
            </Field>
            <Field label={t('typeDelete')}>
              <input
                required
                value={confirmation}
                onChange={(event) => setConfirmation(event.target.value)}
              />
            </Field>
          </div>
          <button
            className="danger-button"
            disabled={confirmation !== 'DELETE' || mutation.isPending}
            type="submit"
          >
            {t('deleteObservations')}
          </button>
          <MutationNotice
            error={mutation.error}
            message={
              mutation.data
                ? t('deleted', { count: new Intl.NumberFormat(locale).format(mutation.data.deletedCount) })
                : undefined
            }
          />
        </form>
      </div>
    </details>
  )
}

function RunsPanel({ park }: { park: AdminPark }) {
  const { locale, t } = useI18n()
  const queryClient = useQueryClient()
  const runsQuery = useQuery({
    queryKey: ['admin', 'runs', park.id],
    queryFn: ({ signal }) => getAdminCollectionRuns(park.id, signal),
    refetchInterval: 30_000,
  })
  const retryMutation = useMutation({
    mutationFn: retryAdminCollectionRun,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin'] })
    },
  })

  return (
    <article className="surface admin-card">
      <AdminCardHeading
        eyebrow={t('diagnostics')}
        title={t('collectionRuns')}
        detail={t('recentAttempts')}
      />
      <div className="admin-table-wrap">
        <table className="admin-table">
          <thead>
            <tr>
              <th>{t('started')}</th>
              <th>{t('result')}</th>
              <th>{t('source')}</th>
              <th>{t('observations')}</th>
              <th>{t('error')}</th>
              <th aria-label={t('actions')} />
            </tr>
          </thead>
          <tbody>
            {runsQuery.data?.map((run) => (
              <tr key={run.id}>
                <td>{formatDateTime(run.startedAt, locale, t('never'))}</td>
                <td>
                  <span className={run.success ? 'status-pill success' : 'status-pill error'}>
                    {run.success ? t('successful') : t('failed')}
                  </span>
                </td>
                <td>
                  <span className="source-badge">{run.triggerSource}</span>
                </td>
                <td>{new Intl.NumberFormat(locale).format(run.observationCount)}</td>
                <td className="error-cell">{run.errorMessage ?? '—'}</td>
                <td>
                  {!run.success && (
                    <button
                      className="text-button"
                      disabled={retryMutation.isPending}
                      onClick={() => retryMutation.mutate(run.id)}
                      type="button"
                    >
                      {t('retry')}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <MutationNotice
        error={retryMutation.error}
        message={
          retryMutation.isSuccess ? t('retrySuccess') : undefined
        }
      />
    </article>
  )
}

function AdminCardHeading({
  eyebrow,
  title,
  detail,
}: {
  eyebrow: string
  title: string
  detail?: string
}) {
  return (
    <header className="admin-card-heading">
      <div>
        <p className="eyebrow">{eyebrow}</p>
        <h2>{title}</h2>
      </div>
      {detail && <span>{detail}</span>}
    </header>
  )
}

function AdminMetric({ label, value }: { label: string; value: string }) {
  return (
    <div className="admin-metric">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function Field({
  label,
  children,
}: {
  label: string
  children: React.ReactNode
}) {
  return (
    <label className="form-field">
      <span>{label}</span>
      {children}
    </label>
  )
}

function Toggle({
  checked,
  label,
  onChange,
}: {
  checked: boolean
  label: string
  onChange: (checked: boolean) => void
}) {
  return (
    <label className="toggle">
      <input
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        type="checkbox"
      />
      <span aria-hidden="true" />
      <strong>{label}</strong>
    </label>
  )
}

function MutationNotice({
  error,
  message,
}: {
  error: Error | null
  message?: string
}) {
  const { t } = useI18n()
  if (error) {
    return <div className="mutation-notice error">{t('mutationError', { message: error.message })}</div>
  }
  return message ? <div className="mutation-notice success">{message}</div> : null
}

function AdminStatus({ message, error = false }: { message: string; error?: boolean }) {
  return <div className={error ? 'admin-alert error' : 'admin-alert'}>{message}</div>
}

function AdminMark() {
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

function parkToForm(park: AdminPark): SaveAdminPark {
  return {
    sourceParkId: park.sourceParkId,
    name: park.name,
    timezone: park.timezone,
    isActive: park.isActive,
    collectionEnabled: park.collectionEnabled,
    collectionIntervalMinutes: park.collectionIntervalMinutes,
  }
}

function emptyLand(parkId: number): SaveAdminLand {
  return { parkId, sourceLandId: 0, name: '', isActive: true }
}

function landToForm(land: AdminLand): SaveAdminLand {
  return {
    parkId: land.parkId,
    sourceLandId: land.sourceLandId,
    name: land.name,
    isActive: land.isActive,
  }
}

function emptyAttraction(parkId: number): SaveAdminAttraction {
  return {
    parkId,
    currentLandId: null,
    sourceRideId: 0,
    name: '',
    isActive: true,
    durationMinutes: null,
    latitude: null,
    longitude: null,
  }
}

function attractionToForm(attraction: AdminAttraction): SaveAdminAttraction {
  return {
    parkId: attraction.parkId,
    currentLandId: attraction.currentLandId,
    sourceRideId: attraction.sourceRideId,
    name: attraction.name,
    isActive: attraction.isActive,
    durationMinutes: attraction.durationMinutes,
    latitude: attraction.latitude,
    longitude: attraction.longitude,
  }
}

function numberOrNull(value: string) {
  return value === '' ? null : Number(value)
}

function localDateTimeValue() {
  const now = new Date()
  const localTime = new Date(now.getTime() - now.getTimezoneOffset() * 60_000)
  return localTime.toISOString().slice(0, 16)
}

function formatDateTime(value: string | null, locale: string, empty: string) {
  return value
    ? new Intl.DateTimeFormat(locale, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : empty
}
