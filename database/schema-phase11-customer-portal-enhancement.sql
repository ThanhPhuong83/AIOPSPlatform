-- Phase 11: Customer Portal Enhancement, Self-Service Automation & Collaboration
-- Vendor-neutral DDL notes:
-- - Store enum values as varchar for portability across PostgreSQL/SQL Server.
-- - Every customer-facing portal table includes customer_id.
-- - Portal AI context must be built only from approved, published, CustomerVisible data.
-- - Attachments store storage_ref only; no binary payload or secret value is stored here.

create table portal_users (
    id uuid primary key,
    customer_id uuid not null,
    user_id varchar(200) not null,
    display_name varchar(200) not null,
    email varchar(320) not null,
    role_key varchar(120) not null,
    status varchar(50) not null,
    can_view_billing boolean not null default false,
    can_view_reports boolean not null default false,
    can_approve boolean not null default false,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_project_access (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    portal_user_id uuid not null,
    access_level varchar(80) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_requests (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    request_no varchar(80) not null,
    request_type varchar(120) not null,
    title varchar(300) not null,
    description text not null,
    status varchar(50) not null,
    priority varchar(20) not null,
    visibility varchar(50) not null,
    submitted_by_user_id varchar(200) not null,
    converted_issue_id uuid null,
    converted_service_request_id uuid null,
    converted_change_request_id uuid null,
    submitted_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_requirement_intakes (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    portal_request_id uuid null,
    title varchar(300) not null,
    business_context text not null,
    requirement_text text not null,
    status varchar(50) not null,
    created_by_user_id varchar(200) not null,
    converted_requirement_id uuid null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_document_shares (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    document_type varchar(120) not null,
    document_id uuid not null,
    document_version int not null,
    visibility varchar(50) not null,
    shared_by varchar(200) not null,
    shared_at timestamptz not null,
    expires_at timestamptz null,
    status varchar(50) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_document_reviews (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    document_share_id uuid not null,
    reviewer_user_id varchar(200) not null,
    status varchar(50) not null,
    comment text not null,
    reviewed_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_approvals (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    approval_type varchar(80) not null,
    source_entity_type varchar(160) not null,
    source_entity_id uuid not null,
    requested_by varchar(200) not null,
    approver_portal_user_id uuid null,
    status varchar(50) not null,
    comment text not null,
    due_at timestamptz null,
    decided_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_knowledge_articles (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    source_knowledge_article_id uuid null,
    title varchar(300) not null,
    category varchar(120) not null,
    content text not null,
    visibility varchar(50) not null,
    status varchar(50) not null,
    version int not null,
    published_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_training_sections (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    title varchar(300) not null,
    module_name varchar(160) not null,
    content text not null,
    visibility varchar(50) not null,
    status varchar(50) not null,
    version int not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_ai_chat_sessions (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    portal_user_id uuid not null,
    status varchar(50) not null,
    title varchar(300) not null,
    context_policy varchar(120) not null,
    closed_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_ai_chat_messages (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    session_id uuid not null,
    sender_type varchar(40) not null,
    message text not null,
    masked_message text not null,
    ai_run_id uuid null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_notifications (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    portal_user_id uuid null,
    notification_type varchar(80) not null,
    title varchar(300) not null,
    message text not null,
    status varchar(50) not null,
    source_entity_type varchar(160) not null,
    source_entity_id uuid null,
    read_at timestamptz null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_comments (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    source_entity_type varchar(160) not null,
    source_entity_id uuid not null,
    portal_user_id uuid null,
    message text not null,
    visibility varchar(50) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_attachments (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    source_entity_type varchar(160) not null,
    source_entity_id uuid not null,
    uploaded_by_portal_user_id uuid null,
    file_name varchar(300) not null,
    content_type varchar(160) not null,
    storage_ref varchar(500) not null,
    visibility varchar(50) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_service_report_shares (
    id uuid primary key,
    customer_id uuid not null,
    service_report_id uuid not null,
    shared_with_portal_user_id uuid null,
    status varchar(50) not null,
    shared_at timestamptz not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table portal_billing_summary_views (
    id uuid primary key,
    customer_id uuid not null,
    portal_user_id uuid not null,
    invoice_draft_id uuid not null,
    viewed_at timestamptz not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create index ix_portal_users_scope on portal_users(customer_id, user_id, status);
create index ix_portal_project_access_scope on portal_project_access(customer_id, project_id, portal_user_id);
create index ix_portal_requests_scope on portal_requests(customer_id, project_id, status, visibility);
create index ix_portal_requirement_intakes_scope on portal_requirement_intakes(customer_id, project_id, status);
create index ix_portal_document_shares_scope on portal_document_shares(customer_id, project_id, document_type, document_id, visibility, status);
create index ix_portal_document_reviews_scope on portal_document_reviews(customer_id, project_id, document_share_id, status);
create index ix_portal_approvals_scope on portal_approvals(customer_id, project_id, approval_type, status, approver_portal_user_id);
create index ix_portal_knowledge_scope on portal_knowledge_articles(customer_id, project_id, category, visibility, status);
create index ix_portal_training_scope on portal_training_sections(customer_id, project_id, module_name, visibility, status);
create index ix_portal_ai_sessions_scope on portal_ai_chat_sessions(customer_id, project_id, portal_user_id, status);
create index ix_portal_ai_messages_scope on portal_ai_chat_messages(customer_id, project_id, session_id, created_at);
create index ix_portal_notifications_scope on portal_notifications(customer_id, project_id, portal_user_id, status, notification_type);
create index ix_portal_comments_scope on portal_comments(customer_id, project_id, source_entity_type, source_entity_id, visibility);
create index ix_portal_attachments_scope on portal_attachments(customer_id, project_id, source_entity_type, source_entity_id, visibility);
create index ix_portal_report_shares_scope on portal_service_report_shares(customer_id, service_report_id, shared_with_portal_user_id);
create index ix_portal_billing_views_scope on portal_billing_summary_views(customer_id, portal_user_id, invoice_draft_id);
