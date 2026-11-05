-- Currency master and effective-dated PKR conversion rates (Master Prompt Part 5.6).
CREATE TABLE IF NOT EXISTS currencies (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    code char(3) NOT NULL,
    name varchar(100) NOT NULL,
    is_base boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL,
    deleted_at timestamptz NULL,
    deleted_by uuid NULL,
    CONSTRAINT currencies_code_uppercase CHECK (code = upper(code) AND code ~ '^[A-Z]{3}$'),
    CONSTRAINT currencies_name_not_blank CHECK (length(btrim(name)) > 0),
    CONSTRAINT currencies_row_version_positive CHECK (row_version >= 1),
    CONSTRAINT currencies_base_protected CHECK (
        NOT is_base OR (code = 'PKR' AND is_active AND deleted_at IS NULL)
    ),
    CONSTRAINT currencies_id_tenant_unique UNIQUE (id, tenant_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS currencies_tenant_code_unique
    ON currencies (tenant_id, code)
    WHERE deleted_at IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS currencies_one_base_per_tenant
    ON currencies (tenant_id)
    WHERE is_base AND deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS currency_rates (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    currency_id uuid NOT NULL,
    rate numeric(18,4) NOT NULL,
    effective_from timestamptz NOT NULL,
    effective_to timestamptz NULL,
    is_active boolean NOT NULL,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL,
    CONSTRAINT currency_rates_currency_fk FOREIGN KEY (currency_id, tenant_id)
        REFERENCES currencies (id, tenant_id),
    CONSTRAINT currency_rates_rate_positive CHECK (rate > 0),
    CONSTRAINT currency_rates_window CHECK (effective_to IS NULL OR effective_to >= effective_from),
    CONSTRAINT currency_rates_active_open CHECK (NOT is_active OR effective_to IS NULL),
    CONSTRAINT currency_rates_row_version_positive CHECK (row_version >= 1)
);

CREATE UNIQUE INDEX IF NOT EXISTS currency_rates_one_active
    ON currency_rates (tenant_id, currency_id)
    WHERE is_active;

CREATE INDEX IF NOT EXISTS currency_rates_history
    ON currency_rates (tenant_id, currency_id, effective_from DESC);

-- Seed the first deployment's tenant. The application also ensures this pair for its configured tenant before currency
-- lists and creates, so a custom first-tenant id is seeded before an administrator can use the module.
INSERT INTO currencies
    (id, tenant_id, code, name, is_base, is_active, row_version, created_at, updated_at)
VALUES
    ('00000000-0000-7000-8000-000000000101', '00000000-0000-0000-0000-000000000001', 'PKR', 'Pakistani Rupee', true, true, 1, now(), now())
ON CONFLICT (tenant_id, code) WHERE deleted_at IS NULL DO NOTHING;

INSERT INTO currency_rates
    (id, tenant_id, currency_id, rate, effective_from, is_active, row_version, created_at, updated_at)
SELECT '00000000-0000-7000-8000-000000000102',
       '00000000-0000-0000-0000-000000000001',
       id,
       1.0000,
       now(),
       true,
       1,
       now(),
       now()
FROM currencies
WHERE tenant_id = '00000000-0000-0000-0000-000000000001' AND code = 'PKR' AND deleted_at IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM currency_rates r
      WHERE r.tenant_id = '00000000-0000-0000-0000-000000000001'
        AND r.currency_id = currencies.id
        AND r.is_active
  );
