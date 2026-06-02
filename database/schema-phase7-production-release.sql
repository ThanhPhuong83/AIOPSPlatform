-- Phase 7: Controlled Production Release & Rollback Governance
-- Vendor-neutral DDL notes:
-- - Use uuid/uniqueidentifier according to the selected provider.
-- - Enum columns can be stored as varchar for portability.
-- - All tables are customer/project scoped to prevent cross-customer access.

create table production_release_packages (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_environment_id uuid not null,
    release_readiness_report_id uuid not null,
    approval_request_id uuid null,
    release_window_id uuid null,
    deployment_plan_id uuid null,
    pre_snapshot_id uuid null,
    post_snapshot_id uuid null,
    snapshot_diff_id uuid null,
    latest_deployment_run_id uuid null,
    closure_report_id uuid null,
    package_no varchar(50) not null,
    version varchar(50) not null,
    title varchar(300) not null,
    status varchar(50) not null,
    risk_level varchar(50) not null,
    summary text not null,
    rollback_plan_validated boolean not null default false,
    closed_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table release_checklist_items (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    title varchar(300) not null,
    required boolean not null default false,
    completed boolean not null default false,
    evidence_ref varchar(500) null,
    completed_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table release_windows (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    starts_at timestamptz not null,
    ends_at timestamptz not null,
    timezone varchar(100) not null,
    status varchar(50) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table production_deployment_plans (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    plan_no varchar(50) not null,
    validated boolean not null default false,
    validation_errors text not null,
    generated_at timestamptz not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table production_deployment_steps (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    deployment_plan_id uuid not null,
    step_order int not null,
    title varchar(300) not null,
    risk_level varchar(50) not null,
    execution_method varchar(50) not null,
    manual_confirmation_required boolean not null default false,
    confirmed boolean not null default false,
    guard_result text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table production_deployment_runs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    production_environment_id uuid not null,
    deployment_plan_id uuid not null,
    pre_snapshot_id uuid null,
    post_snapshot_id uuid null,
    snapshot_diff_id uuid null,
    run_no varchar(50) not null,
    status varchar(50) not null,
    error_message text null,
    started_at timestamptz not null,
    completed_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table deployment_step_runs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    deployment_run_id uuid not null,
    deployment_step_id uuid not null,
    step_order int not null,
    title varchar(300) not null,
    status varchar(50) not null,
    manual_confirmation_required boolean not null default false,
    confirmed_by varchar(200) null,
    started_at timestamptz null,
    completed_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table production_deployment_logs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    deployment_run_id uuid not null,
    level varchar(50) not null,
    message text not null,
    masked_payload text null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table post_release_validation_checks (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    title varchar(300) not null,
    status varchar(50) not null,
    evidence varchar(500) null,
    completed_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table rollback_decision_requests (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    deployment_run_id uuid null,
    reason text not null,
    impact text not null,
    status varchar(50) not null,
    approved_by varchar(200) null,
    rollback_run_ref varchar(500) null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table release_communications (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    audience varchar(50) not null,
    subject varchar(300) not null,
    content text not null,
    sent boolean not null default false,
    sent_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table post_release_tasks (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    target varchar(100) not null,
    title varchar(300) not null,
    completed boolean not null default false,
    completed_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table release_closure_reports (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    production_release_package_id uuid not null,
    report_no varchar(50) not null,
    deployment_summary text not null,
    validation_summary text not null,
    rollback_summary text not null,
    document_update_summary text not null,
    final_recommendation text not null,
    generated_at timestamptz not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create index ix_production_release_packages_scope on production_release_packages(customer_id, project_id, production_environment_id);
create index ix_production_release_packages_status on production_release_packages(customer_id, project_id, status, risk_level);
create index ix_release_checklist_package on release_checklist_items(customer_id, project_id, production_release_package_id);
create index ix_release_windows_package on release_windows(customer_id, project_id, production_release_package_id);
create index ix_production_deployment_plans_package on production_deployment_plans(customer_id, project_id, production_release_package_id);
create index ix_production_deployment_steps_plan on production_deployment_steps(customer_id, project_id, deployment_plan_id);
create index ix_production_deployment_runs_package on production_deployment_runs(customer_id, project_id, production_release_package_id);
create index ix_deployment_step_runs_run on deployment_step_runs(customer_id, project_id, deployment_run_id);
create index ix_production_deployment_logs_run on production_deployment_logs(customer_id, project_id, deployment_run_id);
create index ix_post_release_validation_package on post_release_validation_checks(customer_id, project_id, production_release_package_id);
create index ix_rollback_decisions_package on rollback_decision_requests(customer_id, project_id, production_release_package_id);
create index ix_release_communications_package on release_communications(customer_id, project_id, production_release_package_id);
create index ix_post_release_tasks_package on post_release_tasks(customer_id, project_id, production_release_package_id);
create index ix_release_closure_reports_package on release_closure_reports(customer_id, project_id, production_release_package_id);
