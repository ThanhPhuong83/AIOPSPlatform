-- Phase 15: DevOps, CI/CD, Source Code Automation & AI Code Assistant Governance
-- File references, secret_ref values and diff previews are stored as metadata only.

create table if not exists devops_repositories (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    provider text not null,
    provider_repository_id text not null,
    name text not null,
    repo_url text not null,
    default_branch text not null default 'main',
    secret_ref text not null,
    protect_main_branch boolean not null default true,
    require_pull_request_review boolean not null default true,
    require_ci_before_merge boolean not null default true,
    status text not null default 'Active',
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint ck_devops_repo_secret_ref check (secret_ref like 'secret://%')
);

create table if not exists devops_branches (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    repository_id uuid not null references devops_repositories(id),
    branch_name text not null,
    source_branch text not null default 'main',
    created_by text not null,
    created_by_ai boolean not null default false,
    status text not null default 'Active',
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists devops_pull_requests (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    repository_id uuid not null references devops_repositories(id),
    external_pr_ref text not null,
    source_branch text not null,
    target_branch text not null,
    title text not null,
    description text not null,
    status text not null,
    risk_level text not null,
    change_areas_csv text not null,
    ai_run_id uuid null,
    approval_request_id uuid null,
    build_run_id uuid null,
    test_run_id uuid null,
    code_scan_run_id uuid null,
    merge_commit_ref text null,
    created_by text not null,
    created_by_ai boolean not null default false,
    merged_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists code_review_records (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    pull_request_id uuid not null references devops_pull_requests(id),
    reviewer_user_id text not null,
    decision text not null,
    comments text not null,
    risk_level text not null,
    requires_special_approval boolean not null default false,
    created_by_ai boolean not null default false,
    ai_run_id uuid null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists ai_code_analyses (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    repository_id uuid not null references devops_repositories(id),
    pull_request_id uuid null references devops_pull_requests(id),
    ai_run_id uuid not null,
    branch_name text not null,
    risk_level text not null,
    change_areas_csv text not null,
    summary text not null,
    findings_json jsonb not null default '[]'::jsonb,
    patch_proposal_id uuid null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists ai_patch_proposals (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    repository_id uuid not null references devops_repositories(id),
    pull_request_id uuid null references devops_pull_requests(id),
    ai_run_id uuid not null,
    branch_name text not null,
    title text not null,
    diff_text text not null,
    diff_size_bytes integer not null,
    risk_level text not null,
    change_areas_csv text not null,
    status text not null default 'Proposed',
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint ck_ai_patch_diff_size check (diff_size_bytes <= 12000)
);

create table if not exists cicd_pipelines (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    repository_id uuid not null references devops_repositories(id),
    pipeline_key text not null,
    name text not null,
    provider text not null,
    config_path text not null,
    timeout_seconds integer not null,
    active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint ck_cicd_pipeline_timeout check (timeout_seconds > 0)
);

create table if not exists pipeline_runs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    repository_id uuid not null references devops_repositories(id),
    pipeline_id uuid not null references cicd_pipelines(id),
    pull_request_id uuid null references devops_pull_requests(id),
    run_type text not null,
    status text not null,
    summary text not null,
    logs_ref text null,
    artifact_ref text null,
    error_message text null,
    started_at timestamptz not null default now(),
    completed_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists deployment_packages (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    repository_id uuid not null references devops_repositories(id),
    pull_request_id uuid not null references devops_pull_requests(id),
    build_run_id uuid null,
    test_run_id uuid null,
    code_scan_run_id uuid null,
    package_no text not null,
    version text not null,
    status text not null,
    risk_level text not null,
    artifact_ref text not null,
    diff_summary text not null,
    approval_request_id uuid null,
    ready_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists source_code_snapshots (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    repository_id uuid not null references devops_repositories(id),
    branch_name text not null,
    commit_sha text not null,
    snapshot_no text not null,
    metadata_json jsonb not null default '{}'::jsonb,
    diff_summary text not null,
    diff_text_preview text not null,
    diff_size_bytes integer not null,
    captured_at timestamptz not null default now(),
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists devops_runs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    repository_id uuid null,
    pull_request_id uuid null,
    pipeline_run_id uuid null,
    run_type text not null,
    status text not null,
    actor_user_id text not null,
    correlation_id text not null,
    trace_id text not null,
    masked_input text not null,
    summary text not null,
    error_message text null,
    started_at timestamptz not null default now(),
    completed_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists devops_run_logs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    devops_run_id uuid not null references devops_runs(id),
    level text not null,
    message text not null,
    masked_payload text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists ai_code_governance_policies (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    repository_id uuid null references devops_repositories(id),
    policy_key text not null,
    protected_branches_csv text not null,
    require_human_review boolean not null default true,
    block_direct_main_merge boolean not null default true,
    block_ai_production_deploy boolean not null default true,
    high_risk_requires_approval boolean not null default true,
    special_approval_areas_csv text not null,
    max_diff_bytes integer not null default 12000,
    active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create index if not exists ix_devops_repositories_customer_project on devops_repositories(customer_id, project_id);
create index if not exists ix_devops_pull_requests_customer_project_repo on devops_pull_requests(customer_id, project_id, repository_id, status);
create index if not exists ix_pipeline_runs_customer_project_pr on pipeline_runs(customer_id, project_id, pull_request_id, status);
create index if not exists ix_deployment_packages_customer_project_status on deployment_packages(customer_id, project_id, status);
create index if not exists ix_devops_runs_customer_project on devops_runs(customer_id, project_id, started_at desc);
