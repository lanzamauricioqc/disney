CREATE TABLE public.company_organizations (
    id uuid PRIMARY KEY,
    name text NOT NULL,
    display_name text NOT NULL,
    logo_url text,
    primary_color text,
    secondary_color text,
    welcome_message text,
    support_email text,
    support_phone text,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT ck_company_organizations_name CHECK (length(trim(name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_company_organizations_display_name CHECK (length(trim(display_name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_company_organizations_primary_color
        CHECK (primary_color IS NULL OR primary_color ~ '^#[0-9A-Fa-f]{6}$'),
    CONSTRAINT ck_company_organizations_secondary_color
        CHECK (secondary_color IS NULL OR secondary_color ~ '^#[0-9A-Fa-f]{6}$'),
    CONSTRAINT ck_company_organizations_support_email
        CHECK (support_email IS NULL OR support_email = lower(trim(support_email)))
);

CREATE TABLE public.company_users (
    id uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    email text NOT NULL,
    password_hash text NOT NULL,
    role text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    deactivated_at timestamptz,
    CONSTRAINT uq_company_users_email UNIQUE (email),
    CONSTRAINT uq_company_users_organization_id_id UNIQUE (organization_id, id),
    CONSTRAINT ck_company_users_email_normalized CHECK (email = lower(trim(email))),
    CONSTRAINT ck_company_users_role
        CHECK (role IN ('owner', 'administrator', 'planner', 'support')),
    CONSTRAINT ck_company_users_deactivation
        CHECK ((is_active AND deactivated_at IS NULL) OR (NOT is_active AND deactivated_at IS NOT NULL))
);

CREATE INDEX ix_company_users_organization
    ON public.company_users (organization_id, is_active, email);

CREATE TABLE public.company_invitations (
    id uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    email text NOT NULL,
    role text NOT NULL,
    token_hash text NOT NULL,
    invited_by_user_id uuid NOT NULL,
    expires_at timestamptz NOT NULL,
    accepted_at timestamptz,
    revoked_at timestamptz,
    created_at timestamptz NOT NULL,
    CONSTRAINT uq_company_invitations_token_hash UNIQUE (token_hash),
    CONSTRAINT fk_company_invitations_inviter
        FOREIGN KEY (organization_id, invited_by_user_id)
        REFERENCES public.company_users (organization_id, id),
    CONSTRAINT ck_company_invitations_email_normalized CHECK (email = lower(trim(email))),
    CONSTRAINT ck_company_invitations_role
        CHECK (role IN ('owner', 'administrator', 'planner', 'support')),
    CONSTRAINT ck_company_invitations_expiry CHECK (expires_at > created_at)
);

CREATE UNIQUE INDEX uq_company_invitations_active_email
    ON public.company_invitations (email)
    WHERE accepted_at IS NULL AND revoked_at IS NULL;

CREATE INDEX ix_company_invitations_organization_created
    ON public.company_invitations (organization_id, created_at DESC);

CREATE TABLE public.company_customers (
    id uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    name text NOT NULL,
    email text,
    phone text,
    external_reference text,
    notes text,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    deleted_at timestamptz,
    CONSTRAINT uq_company_customers_organization_id_id UNIQUE (organization_id, id),
    CONSTRAINT ck_company_customers_name CHECK (length(trim(name)) BETWEEN 1 AND 300),
    CONSTRAINT ck_company_customers_email
        CHECK (email IS NULL OR email = lower(trim(email))),
    CONSTRAINT ck_company_customers_deletion
        CHECK ((is_active AND deleted_at IS NULL) OR (NOT is_active AND deleted_at IS NOT NULL))
);

CREATE UNIQUE INDEX uq_company_customers_external_reference
    ON public.company_customers (organization_id, external_reference)
    WHERE external_reference IS NOT NULL AND is_active;

CREATE INDEX ix_company_customers_search
    ON public.company_customers (organization_id, lower(name), email)
    WHERE is_active;

CREATE TABLE public.company_visits (
    id uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    customer_id uuid NOT NULL,
    park_name text NOT NULL,
    visit_date date NOT NULL,
    time_zone text NOT NULL,
    status text NOT NULL,
    party_size integer NOT NULL,
    instructions text,
    meeting_point text,
    transportation_details text,
    completed_item_count integer NOT NULL DEFAULT 0,
    total_item_count integer NOT NULL DEFAULT 0,
    external_reference text,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    deleted_at timestamptz,
    CONSTRAINT uq_company_visits_organization_id_id UNIQUE (organization_id, id),
    CONSTRAINT fk_company_visits_customer
        FOREIGN KEY (organization_id, customer_id)
        REFERENCES public.company_customers (organization_id, id),
    CONSTRAINT ck_company_visits_park_name CHECK (length(trim(park_name)) BETWEEN 1 AND 200),
    CONSTRAINT ck_company_visits_time_zone CHECK (length(trim(time_zone)) BETWEEN 1 AND 100),
    CONSTRAINT ck_company_visits_status
        CHECK (status IN ('planned', 'active', 'completed', 'cancelled')),
    CONSTRAINT ck_company_visits_party_size CHECK (party_size BETWEEN 1 AND 100),
    CONSTRAINT ck_company_visits_progress
        CHECK (completed_item_count >= 0 AND total_item_count >= 0
            AND completed_item_count <= total_item_count),
    CONSTRAINT ck_company_visits_deletion
        CHECK ((is_active AND deleted_at IS NULL) OR (NOT is_active AND deleted_at IS NOT NULL))
);

CREATE UNIQUE INDEX uq_company_visits_external_reference
    ON public.company_visits (organization_id, external_reference)
    WHERE external_reference IS NOT NULL AND is_active;

CREATE INDEX ix_company_visits_organization_date
    ON public.company_visits (organization_id, visit_date, status)
    WHERE is_active;

CREATE INDEX ix_company_visits_customer
    ON public.company_visits (organization_id, customer_id, visit_date DESC)
    WHERE is_active;

CREATE TABLE public.company_visit_notes (
    id uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    visit_id uuid NOT NULL,
    author_user_id uuid NOT NULL,
    note text NOT NULL,
    created_at timestamptz NOT NULL,
    CONSTRAINT fk_company_visit_notes_visit
        FOREIGN KEY (organization_id, visit_id)
        REFERENCES public.company_visits (organization_id, id),
    CONSTRAINT fk_company_visit_notes_author
        FOREIGN KEY (organization_id, author_user_id)
        REFERENCES public.company_users (organization_id, id),
    CONSTRAINT ck_company_visit_notes_note CHECK (length(trim(note)) BETWEEN 1 AND 10000)
);

CREATE INDEX ix_company_visit_notes_visit
    ON public.company_visit_notes (organization_id, visit_id, created_at DESC);

CREATE TABLE public.company_visit_overrides (
    id uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    visit_id uuid NOT NULL,
    created_by_user_id uuid NOT NULL,
    summary text NOT NULL,
    details_json jsonb NOT NULL,
    created_at timestamptz NOT NULL,
    CONSTRAINT fk_company_visit_overrides_visit
        FOREIGN KEY (organization_id, visit_id)
        REFERENCES public.company_visits (organization_id, id),
    CONSTRAINT fk_company_visit_overrides_creator
        FOREIGN KEY (organization_id, created_by_user_id)
        REFERENCES public.company_users (organization_id, id),
    CONSTRAINT ck_company_visit_overrides_summary CHECK (length(trim(summary)) BETWEEN 1 AND 500)
);

CREATE INDEX ix_company_visit_overrides_visit
    ON public.company_visit_overrides (organization_id, visit_id, created_at DESC);

CREATE TABLE public.company_visitor_access_links (
    id uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    visit_id uuid NOT NULL,
    token_hash text NOT NULL,
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz,
    created_at timestamptz NOT NULL,
    last_accessed_at timestamptz,
    CONSTRAINT uq_company_visitor_access_links_token_hash UNIQUE (token_hash),
    CONSTRAINT fk_company_visitor_access_links_visit
        FOREIGN KEY (organization_id, visit_id)
        REFERENCES public.company_visits (organization_id, id),
    CONSTRAINT ck_company_visitor_access_links_expiry CHECK (expires_at > created_at)
);

CREATE INDEX ix_company_visitor_access_links_visit
    ON public.company_visitor_access_links (organization_id, visit_id, created_at DESC);

CREATE TABLE public.company_credit_ledger (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    amount integer NOT NULL,
    reason text NOT NULL,
    source text NOT NULL,
    source_reference text,
    created_by_user_id uuid,
    created_at timestamptz NOT NULL,
    CONSTRAINT fk_company_credit_ledger_creator
        FOREIGN KEY (organization_id, created_by_user_id)
        REFERENCES public.company_users (organization_id, id),
    CONSTRAINT ck_company_credit_ledger_amount CHECK (amount <> 0),
    CONSTRAINT ck_company_credit_ledger_reason CHECK (length(trim(reason)) BETWEEN 1 AND 500)
);

CREATE UNIQUE INDEX uq_company_credit_ledger_source
    ON public.company_credit_ledger (organization_id, source, source_reference)
    WHERE source_reference IS NOT NULL;

CREATE INDEX ix_company_credit_ledger_organization_created
    ON public.company_credit_ledger (organization_id, created_at DESC);

CREATE FUNCTION public.prevent_company_credit_ledger_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'company_credit_ledger is append-only';
END;
$$;

CREATE TRIGGER company_credit_ledger_append_only
BEFORE UPDATE OR DELETE ON public.company_credit_ledger
FOR EACH ROW EXECUTE FUNCTION public.prevent_company_credit_ledger_mutation();

CREATE TABLE public.company_checkout_sessions (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    provider_session_id text NOT NULL,
    bundle_code text NOT NULL,
    credits integer NOT NULL,
    status text NOT NULL,
    amount_paid bigint,
    currency text,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT uq_company_checkout_sessions_provider UNIQUE (provider_session_id),
    CONSTRAINT ck_company_checkout_sessions_credits CHECK (credits > 0),
    CONSTRAINT ck_company_checkout_sessions_status
        CHECK (status IN ('pending', 'paid', 'failed', 'expired'))
);

CREATE INDEX ix_company_checkout_sessions_organization
    ON public.company_checkout_sessions (organization_id, created_at DESC);

CREATE TABLE public.company_payment_events (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    provider_event_id text NOT NULL,
    provider_session_id text NOT NULL,
    event_type text NOT NULL,
    occurred_at timestamptz NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_company_payment_events_provider UNIQUE (provider_event_id)
);

CREATE TABLE public.company_api_keys (
    id uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    name text NOT NULL,
    key_prefix text NOT NULL,
    secret_hash text NOT NULL,
    created_by_user_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    last_used_at timestamptz,
    revoked_at timestamptz,
    CONSTRAINT uq_company_api_keys_secret_hash UNIQUE (secret_hash),
    CONSTRAINT fk_company_api_keys_creator
        FOREIGN KEY (organization_id, created_by_user_id)
        REFERENCES public.company_users (organization_id, id),
    CONSTRAINT ck_company_api_keys_name CHECK (length(trim(name)) BETWEEN 1 AND 100)
);

CREATE INDEX ix_company_api_keys_organization
    ON public.company_api_keys (organization_id, created_at DESC);

CREATE TABLE public.company_webhook_subscriptions (
    id uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    endpoint_url text NOT NULL,
    event_type text NOT NULL,
    signing_secret_hash text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT uq_company_webhook_subscription
        UNIQUE (organization_id, endpoint_url, event_type)
);

CREATE TABLE public.company_notification_outbox (
    id uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    channel text NOT NULL,
    recipient text NOT NULL,
    template text NOT NULL,
    payload_json jsonb NOT NULL,
    status text NOT NULL,
    attempts integer NOT NULL DEFAULT 0,
    last_error text,
    created_at timestamptz NOT NULL,
    processed_at timestamptz,
    CONSTRAINT ck_company_notification_outbox_channel
        CHECK (channel IN ('email', 'sms')),
    CONSTRAINT ck_company_notification_outbox_status
        CHECK (status IN ('queued', 'processing', 'delivered', 'failed')),
    CONSTRAINT ck_company_notification_outbox_attempts CHECK (attempts >= 0)
);

CREATE INDEX ix_company_notification_outbox_dispatch
    ON public.company_notification_outbox (status, created_at)
    WHERE status IN ('queued', 'failed');

CREATE INDEX ix_company_notification_outbox_organization
    ON public.company_notification_outbox (organization_id, created_at DESC);

CREATE TABLE public.company_audit_logs (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    actor_user_id uuid,
    action text NOT NULL,
    entity_type text NOT NULL,
    entity_id text,
    details_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL,
    CONSTRAINT fk_company_audit_logs_actor
        FOREIGN KEY (organization_id, actor_user_id)
        REFERENCES public.company_users (organization_id, id)
);

CREATE INDEX ix_company_audit_logs_organization_created
    ON public.company_audit_logs (organization_id, created_at DESC);
