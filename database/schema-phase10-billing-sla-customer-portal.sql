-- Phase 10: Billing, Subscription, SLA & Customer Portal
-- Vendor-neutral DDL notes:
-- - Store enum values as varchar for portability across PostgreSQL/SQL Server.
-- - All commercial and customer service records include customer_id.
-- - No payment card, bank account, API token, or secret values are stored.
-- - Invoice drafts trace to subscription, billing draft, usage, contract, and service items through trace_json and line item sources.

create table service_plans (
    id uuid primary key,
    plan_code varchar(120) not null,
    name varchar(200) not null,
    description text not null,
    base_monthly_price decimal(18, 2) not null,
    currency varchar(10) not null,
    max_projects int not null,
    max_connectors int not null,
    max_ai_runs_per_month int not null,
    max_tickets_per_month int not null,
    included_support_hours int not null,
    sla_response_hours int not null,
    sla_resolution_hours int not null,
    enabled_modules_csv text not null,
    quota_enforcement_mode varchar(50) not null,
    active boolean not null default true,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table customer_contracts (
    id uuid primary key,
    customer_id uuid not null,
    contract_no varchar(80) not null,
    title varchar(300) not null,
    status varchar(50) not null,
    starts_at timestamptz not null,
    ends_at timestamptz not null,
    currency varchar(10) not null,
    contract_value decimal(18, 2) not null,
    terms_summary text not null,
    billing_contact_ref varchar(500) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table subscriptions (
    id uuid primary key,
    customer_id uuid not null,
    service_plan_id uuid not null,
    contract_id uuid null,
    subscription_no varchar(80) not null,
    status varchar(50) not null,
    billing_cycle varchar(50) not null,
    starts_at timestamptz not null,
    ends_at timestamptz null,
    current_period_start timestamptz not null,
    current_period_end timestamptz not null,
    unit_price decimal(18, 2) not null,
    currency varchar(10) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table support_entitlements (
    id uuid primary key,
    customer_id uuid not null,
    subscription_id uuid not null,
    entitlement_code varchar(120) not null,
    name varchar(200) not null,
    max_tickets_per_month int not null,
    max_ai_runs_per_month int not null,
    max_connectors int not null,
    max_projects int not null,
    included_support_hours int not null,
    enabled_modules_csv text not null,
    quota_enforcement_mode varchar(50) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table sla_policies (
    id uuid primary key,
    customer_id uuid not null,
    subscription_id uuid null,
    policy_no varchar(80) not null,
    name varchar(200) not null,
    severity varchar(50) not null,
    response_hours int not null,
    resolution_hours int not null,
    warning_before_hours int not null,
    business_hours_only boolean not null default false,
    timezone varchar(80) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table customer_portal_tickets (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    issue_id uuid null,
    sla_policy_id uuid null,
    ticket_no varchar(80) not null,
    title varchar(300) not null,
    description text not null,
    severity varchar(50) not null,
    status varchar(50) not null,
    requested_by varchar(200) not null,
    submitted_at timestamptz not null,
    first_response_at timestamptz null,
    resolved_at timestamptz null,
    sla_status varchar(50) not null,
    response_due_at timestamptz null,
    resolution_due_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table service_requests (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    portal_ticket_id uuid null,
    request_no varchar(80) not null,
    request_type varchar(120) not null,
    title varchar(300) not null,
    description text not null,
    status varchar(50) not null,
    risk_level varchar(50) not null,
    estimated_hours decimal(18, 2) not null,
    requested_by varchar(200) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table usage_records (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid null,
    subscription_id uuid null,
    metric_type varchar(80) not null,
    source_entity_type varchar(160) not null,
    source_entity_id uuid null,
    quantity decimal(18, 4) not null,
    unit varchar(40) not null,
    usage_date timestamptz not null,
    notes text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table usage_quota_snapshots (
    id uuid primary key,
    customer_id uuid not null,
    subscription_id uuid not null,
    metric_type varchar(80) not null,
    used_quantity decimal(18, 4) not null,
    included_quantity decimal(18, 4) not null,
    overage_quantity decimal(18, 4) not null,
    enforcement_mode varchar(50) not null,
    blocked boolean not null default false,
    warning_message text not null,
    period_start timestamptz not null,
    period_end timestamptz not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table billing_drafts (
    id uuid primary key,
    customer_id uuid not null,
    subscription_id uuid not null,
    contract_id uuid null,
    billing_draft_no varchar(80) not null,
    status varchar(50) not null,
    period_start timestamptz not null,
    period_end timestamptz not null,
    subtotal decimal(18, 2) not null,
    overage_amount decimal(18, 2) not null,
    tax_amount decimal(18, 2) not null,
    total_amount decimal(18, 2) not null,
    currency varchar(10) not null,
    trace_json text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table billing_line_items (
    id uuid primary key,
    customer_id uuid not null,
    billing_draft_id uuid not null,
    item_type varchar(120) not null,
    description text not null,
    quantity decimal(18, 4) not null,
    unit_price decimal(18, 2) not null,
    amount decimal(18, 2) not null,
    source_entity_type varchar(160) not null,
    source_entity_id uuid null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table invoice_drafts (
    id uuid primary key,
    customer_id uuid not null,
    billing_draft_id uuid not null,
    subscription_id uuid not null,
    invoice_no varchar(80) not null,
    status varchar(50) not null,
    issue_date timestamptz not null,
    due_date timestamptz not null,
    total_amount decimal(18, 2) not null,
    currency varchar(10) not null,
    trace_json text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table payment_tracking_records (
    id uuid primary key,
    customer_id uuid not null,
    invoice_draft_id uuid not null,
    payment_ref varchar(500) not null,
    status varchar(50) not null,
    amount decimal(18, 2) not null,
    currency varchar(10) not null,
    recorded_at timestamptz null,
    notes text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table customer_service_reports (
    id uuid primary key,
    customer_id uuid not null,
    subscription_id uuid null,
    report_no varchar(80) not null,
    period_start timestamptz not null,
    period_end timestamptz not null,
    issue_count int not null,
    sla_met_count int not null,
    sla_breached_count int not null,
    release_count int not null,
    ai_run_count int not null,
    connector_run_count int not null,
    health_score decimal(5, 2) not null,
    summary text not null,
    trace_json text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create unique index ux_service_plans_code on service_plans(plan_code);
create index ix_customer_contracts_scope on customer_contracts(customer_id, status, starts_at, ends_at);
create index ix_subscriptions_scope on subscriptions(customer_id, status, service_plan_id, current_period_start, current_period_end);
create index ix_support_entitlements_scope on support_entitlements(customer_id, subscription_id);
create index ix_sla_policies_scope on sla_policies(customer_id, subscription_id, severity);
create index ix_customer_portal_tickets_scope on customer_portal_tickets(customer_id, project_id, status, severity, sla_status);
create index ix_customer_portal_tickets_sla_due on customer_portal_tickets(customer_id, response_due_at, resolution_due_at);
create index ix_service_requests_scope on service_requests(customer_id, project_id, status, risk_level);
create index ix_usage_records_scope on usage_records(customer_id, subscription_id, metric_type, usage_date);
create index ix_usage_quota_snapshots_scope on usage_quota_snapshots(customer_id, subscription_id, metric_type, period_start, period_end);
create index ix_billing_drafts_scope on billing_drafts(customer_id, subscription_id, status, period_start, period_end);
create index ix_billing_line_items_scope on billing_line_items(customer_id, billing_draft_id, item_type);
create index ix_invoice_drafts_scope on invoice_drafts(customer_id, subscription_id, billing_draft_id, status, issue_date);
create index ix_payment_tracking_scope on payment_tracking_records(customer_id, invoice_draft_id, status);
create index ix_customer_service_reports_scope on customer_service_reports(customer_id, subscription_id, period_start, period_end);
