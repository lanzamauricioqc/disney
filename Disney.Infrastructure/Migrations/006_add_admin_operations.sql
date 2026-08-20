ALTER TABLE public.parks
    ADD COLUMN is_active boolean NOT NULL DEFAULT true,
    ADD COLUMN collection_enabled boolean NOT NULL DEFAULT true,
    ADD COLUMN collection_interval_minutes integer NOT NULL DEFAULT 5,
    ADD CONSTRAINT ck_parks_collection_interval
        CHECK (collection_interval_minutes BETWEEN 1 AND 1440);

ALTER TABLE public.queue_collection_runs
    ADD COLUMN trigger_source text NOT NULL DEFAULT 'scheduled',
    ADD CONSTRAINT ck_collection_runs_trigger_source
        CHECK (trigger_source IN ('scheduled', 'manual', 'retry'));

ALTER TABLE public.queue_observations
    ADD COLUMN is_valid boolean NOT NULL DEFAULT true,
    ADD COLUMN invalid_reason text,
    ADD COLUMN invalidated_at timestamptz,
    ADD CONSTRAINT ck_queue_observations_invalidation
        CHECK (
            (is_valid AND invalid_reason IS NULL AND invalidated_at IS NULL)
            OR
            (NOT is_valid AND invalid_reason IS NOT NULL AND invalidated_at IS NOT NULL)
        );

CREATE INDEX ix_queue_observations_admin_review
    ON public.queue_observations (park_id, observed_at DESC, is_valid);
