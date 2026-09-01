CREATE TABLE public.waitlist_registrations (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    email text NOT NULL,
    registered_at timestamptz NOT NULL,
    CONSTRAINT uq_waitlist_registrations_email UNIQUE (email),
    CONSTRAINT ck_waitlist_registrations_email_normalized
        CHECK (email = lower(trim(email)))
);

CREATE TABLE public.payment_checkout_sessions (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    provider_session_id text NOT NULL,
    provider_customer_id text,
    provider_payment_intent_id text,
    email text NOT NULL,
    product_code text NOT NULL,
    status text NOT NULL,
    amount_expected bigint NOT NULL,
    amount_paid bigint,
    currency text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT uq_payment_checkout_sessions_provider_session
        UNIQUE (provider_session_id),
    CONSTRAINT ck_payment_checkout_sessions_product
        CHECK (product_code IN ('visit-pass', 'trip-pass')),
    CONSTRAINT ck_payment_checkout_sessions_status
        CHECK (status IN ('pending', 'paid', 'failed', 'expired')),
    CONSTRAINT ck_payment_checkout_sessions_amount_expected
        CHECK (amount_expected > 0),
    CONSTRAINT ck_payment_checkout_sessions_amount_paid
        CHECK (amount_paid IS NULL OR amount_paid >= 0),
    CONSTRAINT ck_payment_checkout_sessions_email_normalized
        CHECK (email = lower(trim(email)))
);

CREATE INDEX ix_payment_checkout_sessions_email
    ON public.payment_checkout_sessions (email, created_at DESC);

CREATE TABLE public.payment_events (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    provider_event_id text NOT NULL,
    event_type text NOT NULL,
    provider_session_id text NOT NULL,
    occurred_at timestamptz NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_payment_events_provider_event UNIQUE (provider_event_id)
);

CREATE INDEX ix_payment_events_provider_session
    ON public.payment_events (provider_session_id, occurred_at DESC);
