-- Phase 13: Reporting, Export, Document Generation & Executive Dashboards
-- Vendor-neutral DDL notes:
-- - Store enum values as varchar for portability across PostgreSQL/SQL Server.
-- - Every report job/export includes customer_id and project_id for tenant/project scoping.
-- - Exported document binaries are not stored in the database; only storage_ref metadata is stored.
-- - Template layout is versioned and resolved outside API controllers.
-- - Portal access is limited to shared/published report export files.

create table report_templates (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    template_key varchar(180) not null,
    name varchar(240) not null,
    report_type varchar(80) not null,
    default_format varchar(40) not null,
    max_classification varchar(50) not null,
    requires_permission boolean not null default false,
    required_permission varchar(160) null,
    apply_masking_for_external_export boolean not null default true,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table report_template_versions (
    id uuid primary key,
    customer_id uuid not null,
    template_id uuid not null,
    version int not null,
    layout_definition_json text not null,
    content_schema_json text not null,
    created_by varchar(200) not null,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table report_generation_jobs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    template_id uuid not null,
    template_version int not null,
    report_type varchar(80) not null,
    output_format varchar(40) not null,
    status varchar(50) not null,
    visibility varchar(50) not null,
    requested_by varchar(200) not null,
    date_from timestamptz not null,
    date_to timestamptz not null,
    filter_json text not null,
    masking_applied boolean not null default false,
    queue_provider varchar(120) not null,
    ai_run_id uuid null,
    started_at timestamptz null,
    completed_at timestamptz null,
    error_message text null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table report_export_files (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    report_job_id uuid not null,
    template_id uuid not null,
    template_version int not null,
    report_type varchar(80) not null,
    output_format varchar(40) not null,
    file_name varchar(260) not null,
    content_type varchar(180) not null,
    storage_ref varchar(600) not null,
    size_bytes bigint not null,
    checksum varchar(160) not null,
    visibility varchar(50) not null,
    masking_applied boolean not null default false,
    contains_sensitive_data boolean not null default false,
    published_at timestamptz null,
    shared_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_report_shares (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    report_export_file_id uuid not null,
    shared_with_portal_user_id uuid null,
    visibility varchar(50) not null,
    status varchar(50) not null,
    shared_at timestamptz not null,
    expires_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table dashboard_snapshots (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    snapshot_type varchar(50) not null,
    date_from timestamptz not null,
    date_to timestamptz not null,
    health_score numeric(9, 2) not null,
    delivery_score numeric(9, 2) not null,
    sla_score numeric(9, 2) not null,
    risk_score numeric(9, 2) not null,
    ai_run_id uuid null,
    ai_summary text not null,
    snapshot_json text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create index ix_report_templates_scope on report_templates(customer_id, project_id, report_type, active);
create unique index ux_report_templates_key on report_templates(customer_id, project_id, template_key);
create index ix_report_template_versions_scope on report_template_versions(customer_id, template_id, version, active);
create index ix_report_jobs_scope on report_generation_jobs(customer_id, project_id, report_type, status, created_at);
create index ix_report_jobs_queue on report_generation_jobs(status, created_at);
create index ix_report_files_scope on report_export_files(customer_id, project_id, report_type, visibility, created_at);
create index ix_portal_report_shares_scope on portal_report_shares(customer_id, project_id, report_export_file_id, status, expires_at);
create index ix_dashboard_snapshots_scope on dashboard_snapshots(customer_id, project_id, snapshot_type, date_from, date_to);
