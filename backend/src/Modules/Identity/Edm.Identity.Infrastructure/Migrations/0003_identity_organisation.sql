-- Organisation directory: departments and the user directory (data model section 6, Phase 0 tables built now so the
-- Phase 1A pickers have data). Rows are deactivated, never deleted. Only the columns the Phase 1A screens need are here.
-- Branch, position and line manager columns are added with the Phase 0 organisation screens.

CREATE TABLE IF NOT EXISTS departments (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    name varchar(200) NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL,
    CONSTRAINT departments_name_not_blank CHECK (length(btrim(name)) > 0),
    CONSTRAINT departments_row_version_positive CHECK (row_version >= 1)
);

-- Names are unique among active departments of a tenant. Comparison ignores case.
CREATE UNIQUE INDEX IF NOT EXISTS departments_name_active
    ON departments (tenant_id, lower(name))
    WHERE is_active;

CREATE TABLE IF NOT EXISTS users (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    employee_id varchar(32) NOT NULL,
    name varchar(200) NOT NULL,
    email varchar(254) NULL,
    department_id uuid NOT NULL REFERENCES departments (id),
    is_active boolean NOT NULL DEFAULT true,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL,
    CONSTRAINT users_name_not_blank CHECK (length(btrim(name)) > 0),
    CONSTRAINT users_row_version_positive CHECK (row_version >= 1)
);

-- Employee IDs are unique among all rows of a tenant (active or not). A person is reactivated, not added again.
CREATE UNIQUE INDEX IF NOT EXISTS users_employee_id_unique
    ON users (tenant_id, lower(employee_id));

CREATE INDEX IF NOT EXISTS users_department
    ON users (tenant_id, department_id);
