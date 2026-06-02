-- Phase 16: Observability, Monitoring, Runtime Telemetry & Incident Automation
-- Raw sensitive logs should not be stored; telemetry/log payload columns are masked summaries/previews.

create table if not exists telemetry_sources (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    environment_id uuid null,
    connector_id uuid null,
    production_release_package_id uuid null,
    production_deployment_run_id uuid null,
    source_key text not null,
    name text not null,
    source_type text not null,
    endpoint_ref text not null,
    provider text not null default 'MockTelemetryProvider',
    poll_interval_seconds integer not null,
    timeout_seconds integer not null,
    mask_logs boolean not null default true,
    active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint ck_telemetry_source_poll check (poll_interval_seconds > 0),
    constraint ck_telemetry_source_timeout check (timeout_seconds > 0)
);

create table if not exists runtime_telemetry_samples (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    telemetry_source_id uuid not null references telemetry_sources(id),
    environment_id uuid null,
    connector_id uuid null,
    production_release_package_id uuid null,
    production_deployment_run_id uuid null,
    signal_type text not null,
    health_status text not null,
    metric_name text not null,
    metric_value numeric null,
    unit text not null,
    api_latency_ms integer null,
    uptime_percent numeric null,
    summary text not null,
    masked_payload_json jsonb not null default '{}'::jsonb,
    correlation_id text not null,
    trace_id text not null,
    observed_at timestamptz not null default now(),
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists telemetry_log_summaries (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    telemetry_source_id uuid not null references telemetry_sources(id),
    log_window text not null default '5m',
    total_lines integer not null,
    error_count integer not null,
    warning_count integer not null,
    masked_summary text not null,
    top_errors_json jsonb not null default '[]'::jsonb,
    observed_at timestamptz not null default now(),
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists monitoring_rules (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    telemetry_source_id uuid null references telemetry_sources(id),
    rule_key text not null,
    name text not null,
    signal_type text not null,
    metric_name text not null,
    operator text not null,
    threshold_value numeric null,
    match_text text not null default '',
    severity text not null,
    auto_create_incident boolean not null default true,
    auto_create_issue boolean not null default false,
    active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists alert_rules (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    monitoring_rule_id uuid null references monitoring_rules(id),
    alert_key text not null,
    minimum_severity text not null,
    channel text not null,
    recipient_ref text not null,
    create_notification boolean not null default true,
    create_escalation_for_critical boolean not null default true,
    active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists alert_events (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    monitoring_rule_id uuid null references monitoring_rules(id),
    alert_rule_id uuid null references alert_rules(id),
    telemetry_sample_id uuid null references runtime_telemetry_samples(id),
    incident_id uuid null,
    severity text not null,
    status text not null,
    title text not null,
    message text not null,
    correlation_id text not null,
    trace_id text not null,
    masked_payload_json jsonb not null default '{}'::jsonb,
    triggered_at timestamptz not null default now(),
    acknowledged_at timestamptz null,
    acknowledged_by text null,
    resolved_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists incident_records (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    environment_id uuid null,
    connector_id uuid null,
    production_release_package_id uuid null,
    production_deployment_run_id uuid null,
    issue_id uuid null,
    sla_policy_id uuid null,
    alert_event_id uuid null references alert_events(id),
    incident_no text not null,
    title text not null,
    description text not null,
    status text not null,
    priority text not null,
    severity text not null,
    impact_summary text not null,
    current_mitigation text not null default '',
    ai_run_id uuid null,
    detected_at timestamptz not null default now(),
    mitigated_at timestamptz null,
    resolved_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists incident_actions (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    incident_id uuid not null references incident_records(id),
    action_type text not null,
    actor_user_id text not null,
    summary text not null,
    result_json jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists incident_sla_bindings (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    incident_id uuid not null references incident_records(id),
    sla_policy_id uuid not null,
    status text not null,
    response_due_at timestamptz not null,
    resolution_due_at timestamptz not null,
    responded_at timestamptz null,
    resolved_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists ai_incident_diagnoses (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    incident_id uuid not null references incident_records(id),
    ai_run_id uuid not null,
    root_cause_hypothesis text not null,
    recommended_actions text not null,
    evidence_summary text not null,
    confidence_score numeric not null,
    production_fix_executed boolean not null default false,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists post_incident_reviews (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    incident_id uuid not null references incident_records(id),
    ai_run_id uuid null,
    knowledge_article_id uuid null,
    review_no text not null,
    summary text not null,
    timeline_json jsonb not null default '[]'::jsonb,
    preventive_actions text not null,
    status text not null,
    created_by text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create index if not exists ix_telemetry_sources_customer_project on telemetry_sources(customer_id, project_id, active);
create index if not exists ix_runtime_telemetry_customer_project_source on runtime_telemetry_samples(customer_id, project_id, telemetry_source_id, observed_at desc);
create index if not exists ix_monitoring_rules_customer_project on monitoring_rules(customer_id, project_id, active);
create index if not exists ix_alert_events_customer_project_status on alert_events(customer_id, project_id, status, triggered_at desc);
create index if not exists ix_incident_records_customer_project_status on incident_records(customer_id, project_id, status, detected_at desc);
create index if not exists ix_incident_actions_customer_incident on incident_actions(customer_id, incident_id, created_at desc);
