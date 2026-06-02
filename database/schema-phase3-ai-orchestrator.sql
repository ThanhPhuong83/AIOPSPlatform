alter table ai_runs add column if not exists prompt_template_key varchar(128);
alter table ai_runs add column if not exists prompt_version int;
alter table ai_runs add column if not exists masked_input_preview text;
alter table ai_runs add column if not exists raw_output_json text;
alter table ai_runs add column if not exists validation_errors_json text;

alter table audit_logs alter column customer_id drop not null;

create table if not exists ai_prompt_templates (
  id uuid primary key,
  key varchar(128) not null unique,
  name varchar(256) not null,
  task_type varchar(64) not null,
  description text not null,
  is_active boolean not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists ai_prompt_template_versions (
  id uuid primary key,
  template_id uuid not null references ai_prompt_templates(id),
  template_key varchar(128) not null,
  version int not null,
  system_prompt text not null,
  user_prompt_template text not null,
  output_json_schema text not null,
  created_by varchar(128) not null,
  is_active boolean not null,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  unique(template_id, version)
);

create table if not exists ai_proposals (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  ai_run_id uuid not null references ai_runs(id),
  task_type varchar(64) not null,
  source_entity_type varchar(128) not null,
  source_entity_id uuid not null,
  target_entity_type varchar(128) not null,
  target_entity_id uuid,
  status varchar(64) not null,
  title varchar(512) not null,
  proposed_content text not null,
  structured_output_json text not null,
  validation_errors_json text not null,
  reviewed_by varchar(128),
  review_comment text,
  reviewed_at timestamptz,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create index if not exists ix_ai_proposals_customer_project on ai_proposals(customer_id, project_id, created_at desc);
create index if not exists ix_ai_proposals_source on ai_proposals(customer_id, project_id, source_entity_type, source_entity_id);
create index if not exists ix_ai_runs_prompt on ai_runs(prompt_template_key, prompt_version);
