CREATE TABLE public.company_visit_entitlements (
    id uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES public.company_organizations(id),
    visit_id uuid NOT NULL,
    credit_ledger_entry_id bigint NOT NULL,
    assigned_by_user_id uuid NOT NULL,
    assigned_at timestamptz NOT NULL,
    CONSTRAINT uq_company_visit_entitlements_visit
        UNIQUE (organization_id, visit_id),
    CONSTRAINT uq_company_visit_entitlements_ledger
        UNIQUE (credit_ledger_entry_id),
    CONSTRAINT fk_company_visit_entitlements_visit
        FOREIGN KEY (organization_id, visit_id)
        REFERENCES public.company_visits (organization_id, id),
    CONSTRAINT fk_company_visit_entitlements_user
        FOREIGN KEY (organization_id, assigned_by_user_id)
        REFERENCES public.company_users (organization_id, id),
    CONSTRAINT fk_company_visit_entitlements_ledger
        FOREIGN KEY (credit_ledger_entry_id)
        REFERENCES public.company_credit_ledger (id)
);

CREATE INDEX ix_company_visit_entitlements_organization
    ON public.company_visit_entitlements (organization_id, assigned_at DESC);
