alter table requirements add column if not exists version_group_id uuid;
alter table requirements add column if not exists supersedes_document_id uuid;
alter table requirements add column if not exists is_latest boolean not null default true;
alter table requirements add column if not exists approved_by varchar(128);
alter table requirements add column if not exists approved_at timestamptz;

alter table urs_documents add column if not exists version_group_id uuid;
alter table urs_documents add column if not exists supersedes_document_id uuid;
alter table urs_documents add column if not exists is_latest boolean not null default true;
alter table urs_documents add column if not exists approved_by varchar(128);
alter table urs_documents add column if not exists approved_at timestamptz;

alter table blueprints add column if not exists version_group_id uuid;
alter table blueprints add column if not exists supersedes_document_id uuid;
alter table blueprints add column if not exists is_latest boolean not null default true;
alter table blueprints add column if not exists approved_by varchar(128);
alter table blueprints add column if not exists approved_at timestamptz;

alter table config_specs add column if not exists version_group_id uuid;
alter table config_specs add column if not exists supersedes_document_id uuid;
alter table config_specs add column if not exists is_latest boolean not null default true;
alter table config_specs add column if not exists risk_level varchar(32) not null default 'Medium';
alter table config_specs add column if not exists approved_by varchar(128);
alter table config_specs add column if not exists approved_at timestamptz;

create table if not exists document_signoffs (
  id uuid primary key,
  customer_id uuid not null references customers(id),
  project_id uuid not null references projects(id),
  document_kind varchar(32) not null,
  document_id uuid not null,
  version int not null,
  signed_off_by varchar(128) not null,
  role varchar(128),
  comment text,
  signed_off_at timestamptz not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists hrm_module_definitions (
  id uuid primary key,
  code varchar(64) not null unique,
  name varchar(256) not null,
  default_risk_level varchar(32) not null,
  description text not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create index if not exists ix_document_signoffs_customer_project on document_signoffs(customer_id, project_id);
create index if not exists ix_requirements_version_group on requirements(customer_id, project_id, version_group_id);
create index if not exists ix_urs_version_group on urs_documents(customer_id, project_id, version_group_id);
create index if not exists ix_blueprints_version_group on blueprints(customer_id, project_id, version_group_id);
create index if not exists ix_config_specs_version_group on config_specs(customer_id, project_id, version_group_id);

insert into hrm_module_definitions (id, code, name, default_risk_level, description, created_at, updated_at)
values
  (gen_random_uuid(), 'LEAVE', 'Leave Management', 'Medium', 'Leave policy, leave request, approval and balance tracking.', now(), now()),
  (gen_random_uuid(), 'PAYROLL', 'Payroll', 'Critical', 'Salary calculation, statutory deduction and payroll posting.', now(), now()),
  (gen_random_uuid(), 'PERMISSION', 'Permission', 'Critical', 'Role, permission and access control matrix.', now(), now()),
  (gen_random_uuid(), 'SECURITY', 'Security', 'Critical', 'Authentication, authorization, audit and data protection controls.', now(), now()),
  (gen_random_uuid(), 'INTEGRATION', 'Integration', 'High', 'Integration with attendance, payroll, SSO and external systems.', now(), now())
on conflict (code) do update
set name = excluded.name,
    default_risk_level = excluded.default_risk_level,
    description = excluded.description,
    updated_at = now();
