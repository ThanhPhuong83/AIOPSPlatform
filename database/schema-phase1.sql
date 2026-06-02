create table customers (
  id uuid primary key,
  code varchar(64) not null unique,
  name varchar(256) not null,
  status varchar(32) not null,
  industry varchar(128),
  timezone varchar(64) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table projects (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  code varchar(64) not null,
  name varchar(256) not null,
  description text,
  status varchar(32) not null,
  hrm_product_name varchar(256),
  created_at timestamptz not null,
  updated_at timestamptz not null,
  unique(customer_id, code)
);

create table environments (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  name varchar(64) not null,
  kind varchar(32) not null,
  base_url text,
  status varchar(32) not null,
  requires_approval boolean not null,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  unique(customer_id, project_id, kind)
);

create table source_repositories (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid references environments(id),
  provider varchar(64) not null,
  repo_url text not null,
  default_branch varchar(128) not null,
  secret_ref text not null,
  status varchar(32) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table database_profiles (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid not null references environments(id),
  engine varchar(64) not null,
  host text not null,
  port int not null,
  database_name varchar(256) not null,
  username_ref text,
  secret_ref text not null,
  read_only boolean not null,
  status varchar(32) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table requirements (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  requirement_no varchar(64) not null,
  title varchar(512) not null,
  source_type varchar(32) not null,
  source_file_ref text,
  content_text text not null,
  status varchar(32) not null,
  version int not null,
  created_by varchar(128),
  created_at timestamptz not null,
  updated_at timestamptz not null,
  unique(customer_id, project_id, requirement_no)
);

create table urs_documents (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  requirement_id uuid not null references requirements(id),
  urs_no varchar(64) not null,
  title varchar(512) not null,
  content text not null,
  status varchar(32) not null,
  version int not null,
  ai_run_id uuid,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table blueprints (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  urs_id uuid not null references urs_documents(id),
  blueprint_no varchar(64) not null,
  type varchar(64) not null,
  content text not null,
  status varchar(32) not null,
  version int not null,
  ai_run_id uuid,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table config_specs (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid references environments(id),
  blueprint_id uuid not null references blueprints(id),
  config_no varchar(64) not null,
  module_name varchar(128) not null,
  content text not null,
  status varchar(32) not null,
  version int not null,
  ai_run_id uuid,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table issues (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  environment_id uuid references environments(id),
  issue_no varchar(64) not null,
  title varchar(512) not null,
  description text not null,
  severity varchar(32) not null,
  priority varchar(32) not null,
  status varchar(32) not null,
  reported_by varchar(128),
  assigned_to varchar(128),
  root_cause_summary text,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table ai_runs (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid references projects(id),
  run_type varchar(64) not null,
  provider varchar(64) not null,
  model varchar(128) not null,
  prompt_template_id varchar(128),
  input_ref text,
  input_summary text,
  output_ref text,
  output_summary text,
  status varchar(32) not null,
  token_input int not null,
  token_output int not null,
  cost_estimate numeric(18,6) not null,
  error_message text,
  correlation_id varchar(64) not null,
  started_at timestamptz not null,
  completed_at timestamptz
);

create table issue_analyses (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  issue_id uuid not null references issues(id),
  ai_run_id uuid references ai_runs(id),
  analysis_type varchar(64) not null,
  content text not null,
  confidence_score numeric(5,2) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table fix_proposals (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  issue_id uuid not null references issues(id),
  ai_run_id uuid references ai_runs(id),
  title varchar(512) not null,
  proposed_solution text not null,
  code_change_summary text,
  db_change_summary text,
  risk_level varchar(32) not null,
  status varchar(32) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table change_requests (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  issue_id uuid references issues(id),
  fix_proposal_id uuid references fix_proposals(id),
  cr_no varchar(64) not null,
  title varchar(512) not null,
  description text not null,
  target_environment_id uuid not null references environments(id),
  status varchar(32) not null,
  requires_approval boolean not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table approval_requests (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  entity_type varchar(128) not null,
  entity_id uuid not null,
  target_environment_id uuid references environments(id),
  status varchar(32) not null,
  requested_by varchar(128),
  requested_at timestamptz not null,
  completed_at timestamptz,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table approval_steps (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  approval_request_id uuid not null references approval_requests(id),
  step_order int not null,
  approver_user_id varchar(128) not null,
  status varchar(32) not null,
  comment text,
  acted_at timestamptz,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table releases (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  release_no varchar(64) not null,
  change_request_id uuid references change_requests(id),
  target_environment_id uuid not null references environments(id),
  version varchar(64) not null,
  status varchar(32) not null,
  release_notes text,
  deployment_plan text,
  approved_at timestamptz,
  deployed_at timestamptz,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table rollback_points (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  release_id uuid not null references releases(id),
  environment_id uuid not null references environments(id),
  source_commit varchar(128),
  artifact_ref text,
  database_backup_ref text,
  config_snapshot_ref text,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table knowledge_articles (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid references projects(id),
  issue_id uuid references issues(id),
  title varchar(512) not null,
  category varchar(128) not null,
  content text not null,
  visibility varchar(32) not null,
  status varchar(32) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table customer_connectors (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid references projects(id),
  connector_type varchar(64) not null,
  name varchar(256) not null,
  base_url text,
  secret_ref text not null,
  config_json text not null,
  status varchar(32) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table trace_links (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  from_entity_type varchar(128) not null,
  from_entity_id uuid not null,
  to_entity_type varchar(128) not null,
  to_entity_id uuid not null,
  relation_type varchar(64) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table audit_logs (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid references projects(id),
  actor_user_id varchar(128),
  action varchar(128) not null,
  entity_type varchar(128) not null,
  entity_id uuid not null,
  before_json text,
  after_json text,
  ip_address varchar(64),
  user_agent text,
  correlation_id varchar(64) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create index ix_projects_customer_id on projects(customer_id);
create index ix_environments_customer_project on environments(customer_id, project_id);
create index ix_requirements_customer_project on requirements(customer_id, project_id);
create index ix_issues_customer_project on issues(customer_id, project_id);
create index ix_ai_runs_customer_project on ai_runs(customer_id, project_id);
create index ix_trace_links_customer_project on trace_links(customer_id, project_id);
create index ix_audit_logs_customer_created on audit_logs(customer_id, created_at desc);
