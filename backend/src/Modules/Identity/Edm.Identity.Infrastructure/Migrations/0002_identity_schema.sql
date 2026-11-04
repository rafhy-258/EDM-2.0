-- Phase 0 identity schema: local accounts (ADR-0006). Lockout, password policy and role rules are in C#, not here.
CREATE TABLE IF NOT EXISTS identity_users (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    username text NOT NULL,
    display_name text NOT NULL,
    department text NULL,
    password_hash text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    failed_sign_ins integer NOT NULL DEFAULT 0,
    locked_until_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT identity_users_username_unique UNIQUE (tenant_id, username),
    CONSTRAINT identity_users_username_lower CHECK (username = lower(username)),
    CONSTRAINT identity_users_failed_non_negative CHECK (failed_sign_ins >= 0)
);

CREATE TABLE IF NOT EXISTS identity_user_roles (
    tenant_id uuid NOT NULL,
    user_id uuid NOT NULL REFERENCES identity_users (id),
    role_key text NOT NULL,
    PRIMARY KEY (user_id, role_key)
);
