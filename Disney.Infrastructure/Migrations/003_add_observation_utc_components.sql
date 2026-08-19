ALTER TABLE public.queue_observations
    ADD COLUMN observed_utc_date date,
    ADD COLUMN observed_utc_time time,
    ADD COLUMN observed_utc_hour smallint,
    ADD COLUMN observed_utc_slot_minutes smallint,
    ADD COLUMN observed_utc_day_of_week smallint;

UPDATE public.queue_observations
SET observed_utc_date = (observed_at AT TIME ZONE 'UTC')::date,
    observed_utc_time = (observed_at AT TIME ZONE 'UTC')::time,
    observed_utc_hour =
        EXTRACT(HOUR FROM observed_at AT TIME ZONE 'UTC')::smallint,
    observed_utc_slot_minutes = (
        EXTRACT(HOUR FROM observed_at AT TIME ZONE 'UTC') * 60
        + EXTRACT(MINUTE FROM observed_at AT TIME ZONE 'UTC')
    )::smallint,
    observed_utc_day_of_week =
        EXTRACT(DOW FROM observed_at AT TIME ZONE 'UTC')::smallint;

ALTER TABLE public.queue_observations
    ALTER COLUMN observed_utc_date SET NOT NULL,
    ALTER COLUMN observed_utc_time SET NOT NULL,
    ALTER COLUMN observed_utc_hour SET NOT NULL,
    ALTER COLUMN observed_utc_slot_minutes SET NOT NULL,
    ALTER COLUMN observed_utc_day_of_week SET NOT NULL,
    ADD CONSTRAINT ck_queue_observations_utc_hour
        CHECK (observed_utc_hour BETWEEN 0 AND 23),
    ADD CONSTRAINT ck_queue_observations_utc_slot
        CHECK (observed_utc_slot_minutes BETWEEN 0 AND 1439),
    ADD CONSTRAINT ck_queue_observations_utc_day_of_week
        CHECK (observed_utc_day_of_week BETWEEN 0 AND 6);
