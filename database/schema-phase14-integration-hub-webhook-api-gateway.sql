-- Phase 14: Integration Hub, Webhook, API Gateway & External System Automation
-- Vendor-neutral DDL notes:
-- - Store enum values as varchar for portability across PostgreSQL/SQL Server.
-- - Customer-related integrations include customer_id and optional project_id.
-- - Secrets/tokens/passwords are never stored; only secret_ref values are stored.
-- - Payload history stores masked_payload only.
-- - API gateway routes include an access policy and token_secret_ref.

create table integration_providers (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    provider_key varchar(160) not null,
    name varchar(240) not null,
    category varchar(80) not null,
    base_url varchar(600) not null,
    documentation_url varchar(600) not null,
    supports_inbound_webhook boolean not null default false,
    supports_outbound_webhook boolean not null default true,
    supports_signature_verification boolean not null default false,
    supports_retry boolean not null default true,
    default_timeout_seconds int not null,
    config_json text not null,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table integration_endpoints (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    provider_id uuid not null,
    endpoint_key varchar(180) not null,
    name varchar(240) not null,
    direction varchar(40) not null,
    http_method varchar(20) not null,
    path_or_url varchar(800) not null,
    auth_type varchar(80) not null,
    secret_ref varchar(400) null,
    timeout_seconds int not null,
    max_data_classification varchar(50) not null,
    mask_outbound_payloads boolean not null default true,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table integration_payload_mappings (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    provider_id uuid not null,
    endpoint_id uuid null,
    mapping_key varchar(180) not null,
    source_system varchar(160) not null,
    target_system varchar(160) not null,
    event_type varchar(80) not null,
    mapping_json text not null,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table integration_event_subscriptions (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    provider_id uuid not null,
    endpoint_id uuid null,
    event_type varchar(80) not null,
    subscription_key varchar(180) not null,
    filter_json text not null,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table webhook_outbound_subscriptions (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    provider_id uuid not null,
    endpoint_id uuid null,
    event_type varchar(80) not null,
    target_url varchar(800) not null,
    secret_ref varchar(400) null,
    signature_mode varchar(80) not null,
    max_retry_attempts int not null,
    retry_backoff_seconds int not null,
    timeout_seconds int not null,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table api_gateway_routes (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    route_key varchar(180) not null,
    public_path varchar(500) not null,
    internal_target varchar(800) not null,
    http_method varchar(20) not null,
    allowed_external_system varchar(180) not null,
    required_permission varchar(180) not null,
    token_secret_ref varchar(400) not null,
    timeout_seconds int not null,
    max_data_classification varchar(50) not null,
    access_policy_json text not null,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table integration_automation_triggers (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    provider_id uuid null,
    trigger_key varchar(180) not null,
    event_type varchar(80) not null,
    action_type varchar(80) not null,
    condition_json text not null,
    action_json text not null,
    create_on_failure_only boolean not null default true,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table integration_runs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    provider_id uuid null,
    endpoint_id uuid null,
    event_subscription_id uuid null,
    webhook_subscription_id uuid null,
    api_gateway_route_id uuid null,
    direction varchar(40) not null,
    event_type varchar(80) not null,
    status varchar(50) not null,
    correlation_id varchar(80) not null,
    trace_id varchar(80) not null,
    attempt int not null,
    max_attempts int not null,
    timeout_seconds int not null,
    request_summary text not null,
    masked_payload text not null,
    response_summary text not null,
    error_message text null,
    started_at timestamptz not null,
    completed_at timestamptz null,
    next_retry_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table integration_run_logs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    integration_run_id uuid not null,
    level varchar(40) not null,
    message text not null,
    masked_payload text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create index ix_integration_providers_scope on integration_providers(customer_id, project_id, category, active);
create unique index ux_integration_provider_key on integration_providers(customer_id, project_id, provider_key);
create index ix_integration_endpoints_scope on integration_endpoints(customer_id, project_id, provider_id, direction, active);
create unique index ux_integration_endpoint_key on integration_endpoints(customer_id, provider_id, endpoint_key);
create index ix_integration_mappings_scope on integration_payload_mappings(customer_id, project_id, provider_id, event_type, active);
create index ix_integration_event_subscriptions_scope on integration_event_subscriptions(customer_id, project_id, provider_id, event_type, active);
create index ix_webhook_outbound_scope on webhook_outbound_subscriptions(customer_id, project_id, provider_id, event_type, active);
create index ix_api_gateway_routes_scope on api_gateway_routes(customer_id, project_id, route_key, active);
create index ix_integration_automation_scope on integration_automation_triggers(customer_id, project_id, provider_id, event_type, active);
create index ix_integration_runs_scope on integration_runs(customer_id, project_id, provider_id, status, started_at);
create index ix_integration_runs_retry on integration_runs(status, next_retry_at);
create index ix_integration_runs_trace on integration_runs(customer_id, correlation_id, trace_id);
create index ix_integration_run_logs_scope on integration_run_logs(customer_id, project_id, integration_run_id, created_at);
