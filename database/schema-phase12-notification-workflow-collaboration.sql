-- Phase 12: Notification, Workflow Automation & Collaboration Hub
-- Vendor-neutral DDL notes:
-- - Store enum values as varchar for portability across PostgreSQL/SQL Server.
-- - All customer-related notification, workflow and collaboration records include customer_id.
-- - External notification payloads store masked_payload only.
-- - Email delivery is provider-agnostic; Phase 12 uses a mock provider.

create table notification_templates (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    template_key varchar(160) not null,
    name varchar(200) not null,
    notification_type varchar(80) not null,
    channel varchar(50) not null,
    recipient_type varchar(50) not null,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table notification_template_versions (
    id uuid primary key,
    customer_id uuid not null,
    template_id uuid not null,
    version int not null,
    subject_template text not null,
    body_template text not null,
    max_classification varchar(50) not null,
    created_by varchar(200) not null,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table notification_delivery_logs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    notification_id uuid not null,
    template_id uuid null,
    template_version int null,
    channel varchar(50) not null,
    recipient_type varchar(50) not null,
    recipient_ref varchar(300) not null,
    provider varchar(120) not null,
    status varchar(50) not null,
    masked_payload text not null,
    error_message text null,
    delivered_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table workflow_rules (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    rule_key varchar(160) not null,
    name varchar(240) not null,
    trigger_event varchar(80) not null,
    condition_json text not null,
    action_json text not null,
    status varchar(50) not null,
    priority int not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table workflow_runs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    workflow_rule_id uuid not null,
    trigger_event varchar(80) not null,
    source_entity_type varchar(160) not null,
    source_entity_id uuid null,
    status varchar(50) not null,
    started_at timestamptz not null,
    completed_at timestamptz null,
    error_message text null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table workflow_action_logs (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    workflow_run_id uuid not null,
    action_type varchar(80) not null,
    target_entity_type varchar(160) not null,
    target_entity_id uuid null,
    status varchar(50) not null,
    input_json text not null,
    output_json text not null,
    error_message text null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table collaboration_tasks (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    task_no varchar(80) not null,
    title varchar(300) not null,
    description text not null,
    assignee_user_id varchar(200) not null,
    assignee_type varchar(50) not null,
    status varchar(50) not null,
    priority varchar(50) not null,
    due_at timestamptz null,
    source_entity_type varchar(160) not null,
    source_entity_id uuid null,
    completed_at timestamptz null,
    escalated boolean not null default false,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table reminder_schedules (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    task_id uuid null,
    approval_id uuid null,
    source_entity_type varchar(160) not null,
    source_entity_id uuid null,
    reminder_type varchar(120) not null,
    remind_at timestamptz not null,
    status varchar(50) not null,
    sent_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table escalation_events (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    source_entity_type varchar(160) not null,
    source_entity_id uuid not null,
    reason text not null,
    escalated_to_user_id varchar(200) not null,
    status varchar(50) not null,
    escalated_at timestamptz not null,
    resolved_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table activity_timeline_entries (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    item_type varchar(80) not null,
    source_entity_type varchar(160) not null,
    source_entity_id uuid null,
    actor_user_id varchar(200) not null,
    title varchar(300) not null,
    message text not null,
    visibility varchar(50) not null,
    metadata_json text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create index ix_notification_templates_scope on notification_templates(customer_id, project_id, template_key, notification_type, active);
create index ix_notification_template_versions_scope on notification_template_versions(customer_id, template_id, version, active);
create index ix_notification_delivery_scope on notification_delivery_logs(customer_id, project_id, notification_id, status, channel);
create index ix_workflow_rules_scope on workflow_rules(customer_id, project_id, trigger_event, status, priority);
create index ix_workflow_runs_scope on workflow_runs(customer_id, project_id, trigger_event, status, started_at);
create index ix_workflow_action_logs_scope on workflow_action_logs(customer_id, project_id, workflow_run_id, action_type);
create index ix_collaboration_tasks_scope on collaboration_tasks(customer_id, project_id, assignee_user_id, status, due_at);
create index ix_reminder_schedules_scope on reminder_schedules(customer_id, project_id, status, remind_at);
create index ix_escalation_events_scope on escalation_events(customer_id, project_id, status, escalated_at);
create index ix_activity_timeline_scope on activity_timeline_entries(customer_id, project_id, visibility, created_at);
