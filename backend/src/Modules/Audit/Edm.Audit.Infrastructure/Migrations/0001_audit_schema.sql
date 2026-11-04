-- 0001_audit_schema.sql
-- Audit store (ADR-0005). Append-only: the trigger rejects UPDATE, DELETE and TRUNCATE for every role,
-- including the owner. The application role is granted INSERT and SELECT only (see the backend runbook).
-- Business rules do not live here. This is integrity only (ADR-0004).

CREATE TABLE audit_events (
    id                  uuid            PRIMARY KEY,
    tenant_id           uuid            NOT NULL,
    occurred_at_utc     timestamptz     NOT NULL,
    actor_user_id       uuid            NULL,
    actor_name          varchar(200)    NOT NULL,
    actor_department    varchar(200)    NULL,
    actor_roles         text[]          NOT NULL DEFAULT '{}',
    source_ip           varchar(64)     NULL,
    user_agent          varchar(512)    NULL,
    correlation_id      varchar(128)    NOT NULL,
    module              varchar(64)     NOT NULL,
    entity_type         varchar(100)    NOT NULL,
    entity_id           varchar(200)    NOT NULL,
    action              varchar(100)    NOT NULL,
    outcome             varchar(16)     NOT NULL CHECK (outcome IN ('Success', 'Failure')),
    failure_reason      varchar(500)    NULL,
    remarks             varchar(1000)   NULL,
    request_reference   varchar(64)     NULL,
    policy_version      integer         NULL CHECK (policy_version IS NULL OR policy_version >= 1),
    before_json         jsonb           NULL,
    after_json          jsonb           NULL,
    changes_json        jsonb           NULL,
    CONSTRAINT ck_audit_events_failure_reason CHECK (outcome = 'Success' OR failure_reason IS NOT NULL)
);

CREATE INDEX ix_audit_events_tenant_time ON audit_events (tenant_id, occurred_at_utc);
CREATE INDEX ix_audit_events_actor_time ON audit_events (tenant_id, actor_user_id, occurred_at_utc);
CREATE INDEX ix_audit_events_module_entity ON audit_events (tenant_id, module, entity_type, entity_id);

CREATE FUNCTION audit_events_block_change() RETURNS trigger
    LANGUAGE plpgsql AS
$$
BEGIN
    RAISE EXCEPTION 'audit_events is append-only: % is not permitted (EDM-AUDIT)', TG_OP
        USING ERRCODE = 'P0001';
END;
$$;

CREATE TRIGGER audit_events_no_update_or_delete
    BEFORE UPDATE OR DELETE ON audit_events
    FOR EACH ROW EXECUTE FUNCTION audit_events_block_change();

CREATE TRIGGER audit_events_no_truncate
    BEFORE TRUNCATE ON audit_events
    FOR EACH STATEMENT EXECUTE FUNCTION audit_events_block_change();
