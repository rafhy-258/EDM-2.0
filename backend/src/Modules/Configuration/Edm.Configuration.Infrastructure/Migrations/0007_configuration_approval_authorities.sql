-- Shared approval-authority configuration (Master Prompt Parts 4.4, 5.3 and 5.4).
CREATE UNIQUE INDEX IF NOT EXISTS departments_id_tenant_unique ON departments (id, tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS users_id_tenant_unique ON users (id, tenant_id);

CREATE TABLE IF NOT EXISTS action_tag_sets (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    name varchar(100) NOT NULL,
    designation varchar(32) NOT NULL,
    allowed_actions text[] NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL,
    CONSTRAINT action_tag_sets_name_not_blank CHECK (length(btrim(name)) > 0),
    CONSTRAINT action_tag_sets_designation CHECK (
        designation IN ('DEPARTMENT_HEAD', 'RECOMMENDING_AUTHORITY', 'EC_MEMBER', 'CEO', 'CFO', 'HEAD_ADMIN_GST')
    ),
    CONSTRAINT action_tag_sets_actions CHECK (
        cardinality(allowed_actions) > 0
        AND allowed_actions <@ ARRAY['APPROVE', 'REJECT', 'REVERT', 'FORWARD']::text[]
        AND 'APPROVE' = ANY(allowed_actions)
    ),
    CONSTRAINT action_tag_sets_forward_phase_one CHECK (
        designation = 'DEPARTMENT_HEAD' OR NOT ('FORWARD' = ANY(allowed_actions))
    ),
    CONSTRAINT action_tag_sets_row_version_positive CHECK (row_version >= 1),
    CONSTRAINT action_tag_sets_id_tenant_unique UNIQUE (id, tenant_id),
    CONSTRAINT action_tag_sets_tenant_designation_unique UNIQUE (tenant_id, designation)
);

CREATE TABLE IF NOT EXISTS ec_members (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    user_id uuid NOT NULL,
    designation varchar(32) NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL,
    deleted_at timestamptz NULL,
    deleted_by uuid NULL,
    CONSTRAINT ec_members_designation CHECK (designation IN ('EC_MEMBER', 'CEO', 'CFO', 'HEAD_ADMIN_GST')),
    CONSTRAINT ec_members_row_version_positive CHECK (row_version >= 1),
    CONSTRAINT ec_members_user_fk FOREIGN KEY (user_id, tenant_id) REFERENCES users (id, tenant_id),
    CONSTRAINT ec_members_id_tenant_unique UNIQUE (id, tenant_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ec_members_user_designation_unique
    ON ec_members (tenant_id, user_id, designation)
    WHERE deleted_at IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ec_members_one_active_holder
    ON ec_members (tenant_id, designation)
    WHERE is_active AND deleted_at IS NULL AND designation <> 'EC_MEMBER';

CREATE INDEX IF NOT EXISTS ec_members_tenant_designation
    ON ec_members (tenant_id, designation, is_active)
    WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS recommending_authorities (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    title varchar(200) NOT NULL,
    department_id uuid NOT NULL,
    user_id uuid NOT NULL,
    default_action_tag_set_id uuid NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL,
    deleted_at timestamptz NULL,
    deleted_by uuid NULL,
    CONSTRAINT recommending_authorities_title_not_blank CHECK (length(btrim(title)) > 0),
    CONSTRAINT recommending_authorities_title_characters CHECK (title ~ '^[A-Za-z0-9 ]+$'),
    CONSTRAINT recommending_authorities_row_version_positive CHECK (row_version >= 1),
    CONSTRAINT recommending_authorities_department_fk FOREIGN KEY (department_id, tenant_id)
        REFERENCES departments (id, tenant_id),
    CONSTRAINT recommending_authorities_user_fk FOREIGN KEY (user_id, tenant_id)
        REFERENCES users (id, tenant_id),
    CONSTRAINT recommending_authorities_action_tag_fk FOREIGN KEY (default_action_tag_set_id, tenant_id)
        REFERENCES action_tag_sets (id, tenant_id),
    CONSTRAINT recommending_authorities_id_tenant_unique UNIQUE (id, tenant_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS recommending_authorities_department_user_active
    ON recommending_authorities (tenant_id, department_id, user_id)
    WHERE is_active AND deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS recommending_authorities_tenant_active
    ON recommending_authorities (tenant_id, is_active)
    WHERE deleted_at IS NULL;

-- A deployment's non-default tenant is initialized by the application, preserving any administrator-edited values.
INSERT INTO action_tag_sets (id, tenant_id, name, designation, allowed_actions)
VALUES
    ('00000000-0000-7000-8000-000000000201', '00000000-0000-0000-0000-000000000001', 'Department Head', 'DEPARTMENT_HEAD', ARRAY['APPROVE', 'FORWARD', 'REJECT', 'REVERT']),
    ('00000000-0000-7000-8000-000000000202', '00000000-0000-0000-0000-000000000001', 'Recommending Authority', 'RECOMMENDING_AUTHORITY', ARRAY['APPROVE', 'REJECT', 'REVERT']),
    ('00000000-0000-7000-8000-000000000203', '00000000-0000-0000-0000-000000000001', 'EC Member', 'EC_MEMBER', ARRAY['APPROVE', 'REJECT', 'REVERT']),
    ('00000000-0000-7000-8000-000000000204', '00000000-0000-0000-0000-000000000001', 'CEO', 'CEO', ARRAY['APPROVE', 'REJECT', 'REVERT']),
    ('00000000-0000-7000-8000-000000000205', '00000000-0000-0000-0000-000000000001', 'CFO', 'CFO', ARRAY['APPROVE', 'REJECT', 'REVERT']),
    ('00000000-0000-7000-8000-000000000206', '00000000-0000-0000-0000-000000000001', 'Head of Admin & GST', 'HEAD_ADMIN_GST', ARRAY['APPROVE', 'REJECT', 'REVERT'])
ON CONFLICT (tenant_id, designation) DO NOTHING;
