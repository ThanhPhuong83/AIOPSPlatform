alter table customer_connectors add column if not exists environment_id uuid references environments(id);
alter table customer_connectors add column if not exists permission_policy_id uuid;
alter table customer_connectors add column if not exists last_health_status varchar(32);
alter table customer_connectors add column if not exists last_run_at timestamptz;

create table if not exists connector_permission_policies (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid references environments(id),
  name varchar(256) not null,
  allow_schema_read boolean not null default true,
  allow_config_read boolean not null default true,
  allow_log_read boolean not null default true,
  allow_source_metadata_read boolean not null default true,
  allow_health_check boolean not null default true,
  allow_test_apply boolean not null default false,
  allow_production_apply_with_approval boolean not null default false,
  masking_profile varchar(64) not null default 'Default',
  status varchar(32) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists connector_runs (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid not null references environments(id),
  connector_id uuid not null references customer_connectors(id),
  run_type varchar(64) not null,
  status varchar(32) not null,
  input_summary text,
  output_summary text,
  masked_preview text,
  error_message text,
  correlation_id varchar(64) not null,
  started_at timestamptz not null,
  completed_at timestamptz,
  duration_ms int,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists environment_snapshots (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid not null references environments(id),
  connector_id uuid not null references customer_connectors(id),
  connector_run_id uuid references connector_runs(id),
  apply_run_id uuid,
  snapshot_no varchar(64) not null,
  kind varchar(64) not null,
  stage varchar(32) not null,
  summary text not null,
  snapshot_json text not null,
  masked_summary text not null,
  captured_at timestamptz not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists snapshot_diffs (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid not null references environments(id),
  from_snapshot_id uuid not null references environment_snapshots(id),
  to_snapshot_id uuid not null references environment_snapshots(id),
  snapshot_kind varchar(64) not null,
  diff_summary text not null,
  diff_json text not null,
  risk_level varchar(32) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists apply_runs (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid not null references environments(id),
  connector_id uuid not null references customer_connectors(id),
  fix_proposal_id uuid references fix_proposals(id),
  change_request_id uuid references change_requests(id),
  approval_request_id uuid references approval_requests(id),
  pre_snapshot_id uuid references environment_snapshots(id),
  post_snapshot_id uuid references environment_snapshots(id),
  snapshot_diff_id uuid references snapshot_diffs(id),
  rollback_plan_id uuid,
  regression_test_run_id uuid,
  release_readiness_report_id uuid,
  apply_run_no varchar(64) not null,
  source_type varchar(64) not null,
  source_id uuid not null,
  risk_level varchar(32) not null,
  status varchar(64) not null,
  requires_approval boolean not null,
  requested_by varchar(128) not null,
  rollback_recommendation text not null,
  summary text not null,
  started_at timestamptz not null,
  completed_at timestamptz,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists apply_steps (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid not null references environments(id),
  apply_run_id uuid not null references apply_runs(id),
  step_order int not null,
  name varchar(256) not null,
  status varchar(32) not null,
  details text not null,
  started_at timestamptz,
  completed_at timestamptz,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists apply_logs (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid not null references environments(id),
  apply_run_id uuid not null references apply_runs(id),
  level varchar(32) not null,
  message text not null,
  masked_payload text,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists rollback_plans (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid not null references environments(id),
  apply_run_id uuid not null references apply_runs(id),
  plan_no varchar(64) not null,
  strategy text not null,
  steps text not null,
  validation_checklist text not null,
  status varchar(32) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists regression_test_runs (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid not null references environments(id),
  apply_run_id uuid not null references apply_runs(id),
  regression_test_plan_id uuid references regression_test_plans(id),
  run_no varchar(64) not null,
  status varchar(32) not null,
  total_tests int not null,
  passed_tests int not null,
  failed_tests int not null,
  summary text not null,
  started_at timestamptz not null,
  completed_at timestamptz,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists release_readiness_reports (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid not null references environments(id),
  apply_run_id uuid not null references apply_runs(id),
  snapshot_diff_id uuid references snapshot_diffs(id),
  regression_test_run_id uuid references regression_test_runs(id),
  report_no varchar(64) not null,
  status varchar(32) not null,
  summary text not null,
  blockers text not null,
  generated_at timestamptz not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create index if not exists ix_apply_runs_customer_project_env on apply_runs(customer_id, project_id, environment_id, created_at desc);
create index if not exists ix_apply_steps_run on apply_steps(customer_id, project_id, apply_run_id, step_order);
create index if not exists ix_apply_logs_run on apply_logs(customer_id, project_id, apply_run_id, created_at);
create index if not exists ix_environment_snapshots_apply on environment_snapshots(customer_id, project_id, apply_run_id, captured_at desc);
create index if not exists ix_connector_runs_env on connector_runs(customer_id, project_id, environment_id, started_at desc);
create index if not exists ix_release_readiness_apply on release_readiness_reports(customer_id, project_id, apply_run_id);
