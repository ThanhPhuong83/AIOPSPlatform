-- Phase 9: Security, Compliance, Tenant Isolation & Enterprise Governance
-- Vendor-neutral DDL notes:
-- - Store enum values as varchar for portability across PostgreSQL/SQL Server.
-- - All tenant scoped tables include customer_id. Project-scoped policies also include project_id.
-- - Secret values are never stored here. Only secret_ref is persisted.

create table tenant_access_grants (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    user_id varchar(200) not null,
    role_key varchar(120) not null,
    status varchar(50) not null,
    granted_by varchar(200) not null,
    expires_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table security_roles (
    id uuid primary key,
    customer_id uuid not null,
    role_key varchar(120) not null,
    name varchar(200) not null,
    description text not null,
    is_system_role boolean not null default false,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table security_permissions (
    id uuid primary key,
    permission_key varchar(160) not null,
    name varchar(200) not null,
    resource varchar(120) not null,
    action varchar(120) not null,
    sensitive boolean not null default false,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table security_role_permissions (
    id uuid primary key,
    customer_id uuid not null,
    role_key varchar(120) not null,
    permission_key varchar(160) not null,
    effect varchar(20) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table user_role_assignments (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    user_id varchar(200) not null,
    role_key varchar(120) not null,
    status varchar(50) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table security_policy_rules (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    policy_key varchar(160) not null,
    resource varchar(120) not null,
    action varchar(120) not null,
    required_permission varchar(160) not null,
    effect varchar(20) not null,
    enabled boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table secret_vault_references (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    name varchar(200) not null,
    secret_ref varchar(500) not null,
    vault_provider varchar(120) not null,
    classification varchar(50) not null,
    rotation_due_at timestamptz null,
    status varchar(50) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table secret_access_audits (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    user_id varchar(200) not null,
    secret_ref varchar(500) not null,
    purpose text not null,
    status varchar(50) not null,
    reason text not null,
    correlation_id varchar(80) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table data_classification_rules (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    resource_type varchar(120) not null,
    field_name varchar(160) not null,
    classification varchar(50) not null,
    masking_strategy varchar(80) not null,
    apply_to_ai_prompt boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table ai_access_policies (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    task_type varchar(120) not null,
    allowed_roles_csv text not null,
    max_input_classification varchar(50) not null,
    masking_required boolean not null default true,
    requires_approval_for_high_risk boolean not null default true,
    status varchar(50) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table connector_security_policies (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    environment_id uuid null,
    connector_type varchar(160) not null,
    allowed_actions_csv text not null,
    required_permission varchar(160) not null,
    max_data_classification varchar(50) not null,
    read_only_required boolean not null default true,
    allow_test_apply boolean not null default false,
    allow_production_apply_with_approval boolean not null default false,
    status varchar(50) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table approval_governance_rules (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    rule_key varchar(160) not null,
    module_name varchar(160) not null,
    minimum_risk_level varchar(50) null,
    applies_to_production boolean not null default false,
    required_approval_steps int not null,
    approver_roles_csv text not null,
    requires_security_approval boolean not null default false,
    reason text not null,
    enabled boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table compliance_evidence (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    audit_log_id uuid null,
    approval_request_id uuid null,
    evidence_no varchar(50) not null,
    control_id varchar(120) not null,
    title varchar(300) not null,
    summary text not null,
    entity_type varchar(160) not null,
    entity_id uuid null,
    evidence_ref varchar(500) not null,
    status varchar(50) not null,
    trace_json text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create index ix_tenant_access_scope on tenant_access_grants(customer_id, project_id, user_id, status);
create index ix_security_roles_scope on security_roles(customer_id, role_key);
create index ix_security_permissions_key on security_permissions(permission_key);
create index ix_security_role_permissions_scope on security_role_permissions(customer_id, role_key, permission_key);
create index ix_user_role_assignments_scope on user_role_assignments(customer_id, project_id, user_id, role_key);
create index ix_secret_vault_references_scope on secret_vault_references(customer_id, project_id, secret_ref);
create index ix_secret_access_audits_scope on secret_access_audits(customer_id, project_id, user_id, status);
create index ix_data_classification_scope on data_classification_rules(customer_id, project_id, resource_type, field_name);
create index ix_ai_access_policies_scope on ai_access_policies(customer_id, project_id, task_type);
create index ix_connector_security_scope on connector_security_policies(customer_id, project_id, connector_type);
create index ix_approval_governance_scope on approval_governance_rules(customer_id, project_id, module_name, minimum_risk_level);
create index ix_compliance_evidence_scope on compliance_evidence(customer_id, project_id, control_id, status);
