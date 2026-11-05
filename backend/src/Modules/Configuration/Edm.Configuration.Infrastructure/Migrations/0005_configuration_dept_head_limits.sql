-- Configuration: department-head limits, OPEX and CAPEX (data-model design note, sections 3.1 and 4, M1A-04 and M1A-05, part).
-- One table per nature, with the same shape. Each change closes the active row and inserts a new one, so history is kept.
-- Rows are never deleted. The active row is the limit going forward: it has no end date.
-- The seeded default (DefaultDeptHeadLimitPkr, 20,000) applies while no row is active. It lives in code until settings exist.

CREATE TABLE IF NOT EXISTS opex_dept_head_limits (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    amount_pkr numeric(18,2) NOT NULL,
    description varchar(500) NULL,
    effective_from timestamptz NOT NULL,
    effective_to timestamptz NULL,
    is_active boolean NOT NULL,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL,
    CONSTRAINT opex_dept_head_limits_amount_positive CHECK (amount_pkr > 0),
    CONSTRAINT opex_dept_head_limits_window CHECK (effective_to IS NULL OR effective_to >= effective_from),
    CONSTRAINT opex_dept_head_limits_active_open CHECK (NOT is_active OR effective_to IS NULL),
    CONSTRAINT opex_dept_head_limits_row_version_positive CHECK (row_version >= 1)
);

CREATE UNIQUE INDEX IF NOT EXISTS opex_dept_head_limits_one_active
    ON opex_dept_head_limits (tenant_id)
    WHERE is_active;

CREATE INDEX IF NOT EXISTS opex_dept_head_limits_history
    ON opex_dept_head_limits (tenant_id, effective_from);

CREATE TABLE IF NOT EXISTS capex_dept_head_limits (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    amount_pkr numeric(18,2) NOT NULL,
    description varchar(500) NULL,
    effective_from timestamptz NOT NULL,
    effective_to timestamptz NULL,
    is_active boolean NOT NULL,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL,
    CONSTRAINT capex_dept_head_limits_amount_positive CHECK (amount_pkr > 0),
    CONSTRAINT capex_dept_head_limits_window CHECK (effective_to IS NULL OR effective_to >= effective_from),
    CONSTRAINT capex_dept_head_limits_active_open CHECK (NOT is_active OR effective_to IS NULL),
    CONSTRAINT capex_dept_head_limits_row_version_positive CHECK (row_version >= 1)
);

CREATE UNIQUE INDEX IF NOT EXISTS capex_dept_head_limits_one_active
    ON capex_dept_head_limits (tenant_id)
    WHERE is_active;

CREATE INDEX IF NOT EXISTS capex_dept_head_limits_history
    ON capex_dept_head_limits (tenant_id, effective_from);
