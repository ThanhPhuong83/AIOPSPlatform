-- Phase 17: Data Import, Migration, Master Data Governance & Validation
-- Files are stored as FileRef metadata only. No uploaded binary or unmasked sensitive preview is stored here.

create table if not exists data_import_templates (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    template_key text not null,
    name text not null,
    description text not null default '',
    data_domain text not null,
    status text not null,
    current_version integer not null default 1,
    created_by text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint uq_data_import_templates_scope unique (customer_id, project_id, template_key),
    constraint ck_data_import_template_version check (current_version > 0)
);

create table if not exists data_import_template_versions (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    template_id uuid not null references data_import_templates(id),
    version integer not null,
    schema_json jsonb not null default '{}'::jsonb,
    required_columns_json jsonb not null default '[]'::jsonb,
    data_classification_json jsonb not null default '{}'::jsonb,
    change_summary text not null default '',
    created_by text not null,
    created_at timestamptz not null default now(),
    constraint uq_data_import_template_versions unique (template_id, version),
    constraint ck_data_import_template_versions_version check (version > 0)
);

create table if not exists data_import_files (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    environment_id uuid not null,
    template_id uuid not null references data_import_templates(id),
    file_ref text not null,
    file_name text not null,
    file_type text not null,
    size_bytes bigint not null,
    row_count integer not null,
    classification text not null,
    masked_preview_json jsonb not null default '[]'::jsonb,
    uploaded_by text not null,
    uploaded_at timestamptz not null default now(),
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint ck_data_import_files_no_empty_ref check (length(file_ref) > 0),
    constraint ck_data_import_files_size check (size_bytes >= 0),
    constraint ck_data_import_files_rows check (row_count >= 0)
);

create table if not exists data_column_mappings (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    template_id uuid not null references data_import_templates(id),
    template_version integer not null,
    mapping_key text not null,
    version integer not null,
    status text not null,
    source_column text not null,
    target_field text not null,
    transform_expression text not null default '',
    data_classification text not null,
    sensitive boolean not null default false,
    created_by text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint uq_data_column_mapping_version unique (customer_id, project_id, template_id, mapping_key, version),
    constraint ck_data_column_mapping_versions check (template_version > 0 and version > 0)
);

create table if not exists data_validation_rules (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    template_id uuid not null references data_import_templates(id),
    rule_key text not null,
    name text not null,
    rule_type text not null,
    severity text not null,
    target_field text not null,
    config_json jsonb not null default '{}'::jsonb,
    active boolean not null default true,
    created_by text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint uq_data_validation_rules_scope unique (customer_id, project_id, template_id, rule_key)
);

create table if not exists data_import_batches (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    environment_id uuid not null,
    connector_id uuid null,
    template_id uuid not null references data_import_templates(id),
    template_version integer not null,
    import_file_id uuid not null references data_import_files(id),
    batch_no text not null,
    data_domain text not null,
    status text not null,
    dry_run_required boolean not null default true,
    dry_run_passed boolean not null default false,
    pre_import_snapshot_id uuid null,
    created_by text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint uq_data_import_batches_scope unique (customer_id, project_id, batch_no),
    constraint ck_data_import_batches_template_version check (template_version > 0)
);

create table if not exists data_import_runs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    batch_id uuid not null references data_import_batches(id),
    run_no text not null,
    run_type text not null,
    status text not null,
    processor_name text not null default 'MockImportProcessor',
    queued_at timestamptz not null default now(),
    started_at timestamptz null,
    completed_at timestamptz null,
    total_rows integer not null default 0,
    success_rows integer not null default 0,
    error_rows integer not null default 0,
    warning_rows integer not null default 0,
    result_summary_json jsonb not null default '{}'::jsonb,
    correlation_id text not null,
    trace_id text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint uq_data_import_runs_scope unique (customer_id, project_id, run_no),
    constraint ck_data_import_runs_rows check (total_rows >= 0 and success_rows >= 0 and error_rows >= 0 and warning_rows >= 0)
);

create table if not exists data_validation_issues (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    batch_id uuid not null references data_import_batches(id),
    run_id uuid null references data_import_runs(id),
    row_number integer null,
    source_column text not null default '',
    target_field text not null default '',
    issue_code text not null,
    severity text not null,
    message text not null,
    masked_value text not null default '',
    duplicate_key text not null default '',
    created_at timestamptz not null default now()
);

create table if not exists data_reconciliation_reports (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    batch_id uuid not null references data_import_batches(id),
    report_no text not null,
    status text not null,
    source_rows integer not null,
    target_rows integer not null,
    matched_rows integer not null,
    mismatch_rows integer not null,
    summary_json jsonb not null default '{}'::jsonb,
    file_ref text not null default '',
    generated_by text not null,
    generated_at timestamptz not null default now(),
    created_at timestamptz not null default now(),
    constraint uq_data_reconciliation_reports_scope unique (customer_id, project_id, report_no)
);

create table if not exists data_migration_reports (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    batch_id uuid not null references data_import_batches(id),
    reconciliation_report_id uuid null references data_reconciliation_reports(id),
    report_no text not null,
    report_type text not null,
    status text not null,
    summary_json jsonb not null default '{}'::jsonb,
    file_ref text not null default '',
    generated_by text not null,
    generated_at timestamptz not null default now(),
    created_at timestamptz not null default now(),
    constraint uq_data_migration_reports_scope unique (customer_id, project_id, report_no)
);

create table if not exists data_sign_offs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    batch_id uuid not null references data_import_batches(id),
    reconciliation_report_id uuid not null references data_reconciliation_reports(id),
    status text not null,
    signed_by text not null,
    signed_role text not null,
    comment text not null default '',
    signed_at timestamptz not null default now(),
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint uq_data_sign_off_batch unique (customer_id, project_id, batch_id)
);

create table if not exists ai_data_migration_assistances (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    batch_id uuid null references data_import_batches(id),
    template_id uuid null references data_import_templates(id),
    ai_run_id uuid not null,
    assistance_type text not null,
    summary text not null,
    suggestion_json jsonb not null default '{}'::jsonb,
    created_by text not null,
    created_at timestamptz not null default now()
);

create index if not exists ix_data_import_templates_customer_project on data_import_templates(customer_id, project_id, status);
create index if not exists ix_data_import_files_customer_project on data_import_files(customer_id, project_id, template_id, environment_id);
create index if not exists ix_data_column_mappings_customer_project on data_column_mappings(customer_id, project_id, template_id, status);
create index if not exists ix_data_validation_rules_customer_project on data_validation_rules(customer_id, project_id, template_id, active);
create index if not exists ix_data_import_batches_customer_project on data_import_batches(customer_id, project_id, status, data_domain);
create index if not exists ix_data_import_runs_batch on data_import_runs(customer_id, project_id, batch_id, run_type, status);
create index if not exists ix_data_validation_issues_batch on data_validation_issues(customer_id, project_id, batch_id, severity);
create index if not exists ix_data_reconciliation_reports_batch on data_reconciliation_reports(customer_id, project_id, batch_id, status);
create index if not exists ix_data_sign_offs_customer_project on data_sign_offs(customer_id, project_id, status);
create index if not exists ix_ai_data_migration_assistances_scope on ai_data_migration_assistances(customer_id, project_id, assistance_type);
