-- Phase 8: Continuous Learning, Knowledge Automation & Governance Analytics
-- Vendor-neutral DDL notes:
-- - Store enum values as varchar for PostgreSQL/SQL Server portability.
-- - Every table is scoped by customer_id + project_id.
-- - Knowledge items are proposals until human review unless low-risk auto approval is explicitly enabled.

create table knowledge_learning_items (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    ai_run_id uuid null,
    source_type varchar(80) not null,
    source_entity_type varchar(120) not null,
    source_entity_id uuid not null,
    source_summary text not null,
    knowledge_no varchar(50) not null,
    title varchar(300) not null,
    category varchar(100) not null,
    module_name varchar(150) not null,
    content text not null,
    lessons_learned text not null,
    risk_level varchar(50) not null,
    status varchar(50) not null,
    version int not null,
    version_group_id uuid not null,
    supersedes_knowledge_item_id uuid null,
    low_risk_auto_approved boolean not null default false,
    reviewed_by varchar(200) null,
    reviewed_at timestamptz null,
    expires_at timestamptz null,
    explainability_json text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table repeated_issue_patterns (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    ai_run_id uuid null,
    pattern_key varchar(250) not null,
    module_name varchar(150) not null,
    category varchar(80) not null,
    issue_count int not null,
    risk_level varchar(50) not null,
    summary text not null,
    recommendation text not null,
    source_issue_ids_json text not null,
    first_seen_at timestamptz not null,
    last_seen_at timestamptz not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table governance_score_snapshots (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    ai_run_id uuid null,
    score_type varchar(80) not null,
    module_name varchar(150) null,
    config_spec_id uuid null,
    score decimal(8, 2) not null,
    trend varchar(50) not null,
    formula text not null,
    explanation text not null,
    inputs_json text not null,
    calculated_at timestamptz not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table governance_insights (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    ai_run_id uuid null,
    insight_type varchar(80) not null,
    module_name varchar(150) not null,
    risk_level varchar(50) not null,
    title varchar(300) not null,
    summary text not null,
    recommendation text not null,
    source_refs_json text not null,
    status varchar(50) not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create table ai_performance_metrics (
    id uuid primary key,
    customer_id uuid not null,
    project_id uuid not null,
    prompt_template_key varchar(200) null,
    task_type varchar(100) null,
    total_runs int not null,
    completed_runs int not null,
    failed_runs int not null,
    accepted_outputs int not null,
    rejected_outputs int not null,
    failed_validation_outputs int not null,
    quality_score decimal(8, 2) not null,
    formula text not null,
    explanation text not null,
    calculated_at timestamptz not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);

create index ix_knowledge_learning_scope on knowledge_learning_items(customer_id, project_id, module_name, status);
create index ix_knowledge_learning_source on knowledge_learning_items(customer_id, project_id, source_entity_type, source_entity_id);
create index ix_knowledge_learning_version on knowledge_learning_items(customer_id, project_id, version_group_id, version);
create index ix_repeated_issue_patterns_scope on repeated_issue_patterns(customer_id, project_id, module_name, category);
create index ix_governance_scores_scope on governance_score_snapshots(customer_id, project_id, score_type, module_name, calculated_at);
create index ix_governance_insights_scope on governance_insights(customer_id, project_id, insight_type, module_name);
create index ix_ai_performance_metrics_scope on ai_performance_metrics(customer_id, project_id, calculated_at);
