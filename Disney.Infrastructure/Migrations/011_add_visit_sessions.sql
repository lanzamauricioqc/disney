CREATE TABLE public.visit_sessions (
    id uuid PRIMARY KEY,
    park_id bigint NOT NULL REFERENCES public.parks(id),
    visit_start_at timestamptz NOT NULL,
    visit_end_at timestamptz NOT NULL,
    party_size integer NOT NULL,
    started_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    status text NOT NULL,
    total_walking_minutes integer NOT NULL,
    total_queue_minutes integer NOT NULL,
    total_attraction_minutes integer NOT NULL,
    algorithm_version text NOT NULL,
    CONSTRAINT ck_visit_sessions_window CHECK (visit_end_at > visit_start_at),
    CONSTRAINT ck_visit_sessions_party_size CHECK (party_size BETWEEN 1 AND 20),
    CONSTRAINT ck_visit_sessions_status CHECK (status IN ('Active', 'Completed')),
    CONSTRAINT ck_visit_sessions_totals CHECK (
        total_walking_minutes >= 0
        AND total_queue_minutes >= 0
        AND total_attraction_minutes >= 0
    )
);

CREATE TABLE public.visit_session_stops (
    session_id uuid NOT NULL REFERENCES public.visit_sessions(id),
    sequence integer NOT NULL,
    attraction_id bigint NOT NULL REFERENCES public.attractions(id),
    attraction_name text NOT NULL,
    preference text NOT NULL,
    travel_starts_at timestamptz NOT NULL,
    walking_minutes integer NOT NULL,
    queue_starts_at timestamptz NOT NULL,
    queue_minutes integer NOT NULL,
    attraction_starts_at timestamptz NOT NULL,
    attraction_duration_minutes integer NOT NULL,
    completes_at timestamptz NOT NULL,
    status text NOT NULL,
    status_changed_at timestamptz,
    PRIMARY KEY (session_id, attraction_id),
    CONSTRAINT uq_visit_session_stops_sequence UNIQUE (session_id, sequence),
    CONSTRAINT ck_visit_session_stops_sequence CHECK (sequence > 0),
    CONSTRAINT ck_visit_session_stops_preference
        CHECK (preference IN ('MustDo', 'WouldLike')),
    CONSTRAINT ck_visit_session_stops_status
        CHECK (status IN ('Pending', 'Completed', 'Skipped')),
    CONSTRAINT ck_visit_session_stops_durations CHECK (
        walking_minutes >= 0
        AND queue_minutes >= 0
        AND attraction_duration_minutes >= 0
    ),
    CONSTRAINT ck_visit_session_stops_status_time CHECK (
        (status = 'Pending' AND status_changed_at IS NULL)
        OR (status <> 'Pending' AND status_changed_at IS NOT NULL)
    )
);

CREATE INDEX ix_visit_session_stops_pending
    ON public.visit_session_stops (session_id, sequence)
    WHERE status = 'Pending';
