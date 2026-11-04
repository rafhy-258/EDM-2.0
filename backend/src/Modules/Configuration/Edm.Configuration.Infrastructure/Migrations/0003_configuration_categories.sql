-- Configuration: categories and subcategories (data-model design note, sections 2.3, 2.4 and 10, M1A-02).
-- Soft delete only: deleted_at is set by the application, never a hard DELETE (Master Prompt Part 5.1).
-- Names are unique among active, non-deleted rows of a tenant (categories) or of a category (subcategories).
-- Business rules (usage checks, deactivation warnings) live in the application layer, not here.

CREATE TABLE IF NOT EXISTS categories (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    name varchar(200) NOT NULL,
    description varchar(500) NULL,
    is_others boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL,
    deleted_at timestamptz NULL,
    deleted_by uuid NULL,
    CONSTRAINT categories_name_not_blank CHECK (length(btrim(name)) > 0),
    CONSTRAINT categories_row_version_positive CHECK (row_version >= 1)
);

CREATE UNIQUE INDEX IF NOT EXISTS categories_name_active_unique
    ON categories (tenant_id, lower(name))
    WHERE is_active AND deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS categories_tenant_list
    ON categories (tenant_id, is_active)
    WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS subcategories (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    category_id uuid NOT NULL REFERENCES categories (id),
    name varchar(200) NOT NULL,
    description varchar(500) NULL,
    is_others boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL,
    deleted_at timestamptz NULL,
    deleted_by uuid NULL,
    CONSTRAINT subcategories_name_not_blank CHECK (length(btrim(name)) > 0),
    CONSTRAINT subcategories_row_version_positive CHECK (row_version >= 1)
);

CREATE UNIQUE INDEX IF NOT EXISTS subcategories_name_active_unique
    ON subcategories (category_id, lower(name))
    WHERE is_active AND deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS subcategories_category_list
    ON subcategories (tenant_id, category_id)
    WHERE deleted_at IS NULL;
