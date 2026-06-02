alter table issues add column if not exists linked_entity_type varchar(128);
alter table issues add column if not exists linked_entity_id uuid;
alter table issues add column if not exists category varchar(64) not null default 'Other';
alter table issues add column if not exists risk_level varchar(32) not null default 'Medium';

create table if not exists regression_test_plans (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  issue_id uuid not null references issues(id),
  change_request_id uuid references change_requests(id),
  ai_run_id uuid references ai_runs(id),
  test_plan_no varchar(64) not null,
  title varchar(512) not null,
  content text not null,
  risk_level varchar(32) not null,
  status varchar(32) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists release_drafts (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  issue_id uuid not null references issues(id),
  change_request_id uuid references change_requests(id),
  ai_run_id uuid references ai_runs(id),
  release_draft_no varchar(64) not null,
  title varchar(512) not null,
  release_notes text not null,
  deployment_plan text not null,
  risk_level varchar(32) not null,
  status varchar(32) not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create index if not exists ix_issues_linked_entity on issues(customer_id, project_id, linked_entity_type, linked_entity_id);
create index if not exists ix_issues_risk_status on issues(customer_id, project_id, risk_level, status);
create index if not exists ix_issue_analyses_issue on issue_analyses(customer_id, project_id, issue_id, created_at desc);
create index if not exists ix_fix_proposals_issue on fix_proposals(customer_id, project_id, issue_id, created_at desc);
create index if not exists ix_change_requests_issue on change_requests(customer_id, project_id, issue_id, created_at desc);
create index if not exists ix_regression_test_plans_issue on regression_test_plans(customer_id, project_id, issue_id, created_at desc);
create index if not exists ix_release_drafts_issue on release_drafts(customer_id, project_id, issue_id, created_at desc);
create index if not exists ix_knowledge_articles_issue on knowledge_articles(customer_id, project_id, issue_id, created_at desc);
