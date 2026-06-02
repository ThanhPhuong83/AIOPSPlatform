# HRM AI Ops Platform

Phase 1 scaffold for a central SaaS console that manages customers, HRM projects, environments, repositories, database profiles, requirements, issues, AI runs, audit logs, and traceability.

Phase 2 adds Document Lifecycle Core:

- Requirement Intake -> URS -> Blueprint -> Config Specification
- document version chain
- sign-off history
- approved document lock rule
- traceability view
- HRM module risk defaults
- Leave Management sample data

Phase 3 adds AI Orchestrator Core:

- provider-agnostic structured AI execution
- prompt template registry and versioning
- customer/project scoped context builder
- data masking before prompt execution
- JSON validation before proposal persistence
- AI proposal review with Accept / Reject / Apply flow

Phase 4 adds Operation Management & AI Diagnosis Core:

- issue intake with category, severity, risk and document/config linking
- AI issue classification, root cause analysis and fix proposal drafts
- AI change request, regression test plan, release draft and knowledge update proposals
- high-risk change requests create approval requests
- accepted knowledge proposals create customer-scoped knowledge articles
- issue-linked traceability and audit logging across proposal review actions

Phase 6 adds Controlled Test Apply & Regression Automation:

- Test/UAT-only controlled apply flow, with Production blocked
- mock connector TestApply permission policy for local execution
- dry run before apply
- rollback plan before apply
- pre-apply snapshot, post-apply snapshot, and snapshot diff
- apply run, apply steps, apply logs, connector runs, and audit trail
- regression automation and release readiness report
- no production database write, source merge, pull request, production release, or rollback execution

Phase 13 adds Reporting, Export, Document Generation & Executive Dashboards:

- versioned report templates for URS, Blueprint, Config Spec, UAT, Release, SLA, Billing, Audit, Security and Knowledge reports
- queue-ready report generation jobs with file references instead of database binaries
- Word/PDF/Excel export metadata with CustomerId/ProjectId scoping, audit logs and masking flags
- Executive, Project and Customer Health dashboards with date range filters
- AI-generated executive summaries recorded as AiRun and DashboardSnapshot
- customer portal shared/published report access only

Phase 14 adds Integration Hub, Webhook, API Gateway & External System Automation:

- integration providers/endpoints for customer HRM API, Git providers, DevOps, Jira, Teams/Slack/Email, n8n, ERP, Accounting, Ticket System and generic webhook
- secret_ref-only authentication metadata, no raw tokens/API keys/passwords in the database
- inbound webhook signature verification for providers that support it
- outbound webhook retry policy, timeout, correlationId and traceId
- API Gateway routes with access policy and token secret_ref
- integration run history and masked run logs
- integration failure automation that can create notification/task records

Phase 15 adds DevOps, CI/CD, Source Code Automation & AI Code Assistant Governance:

- customer/project-scoped DevOps repositories with GitHub/GitLab/Azure DevOps/mock provider abstraction metadata
- secret_ref-only Git credentials and no raw token storage
- pull request, review, approval, build, test, code scan and deployment package gates
- AI code analysis and AI patch proposal flows recorded as AiRun
- DevOpsRun and AuditLog records for repository, PR, CI/CD, package and merge actions
- high-risk scan findings block merge; failed tests block release package readiness
- AI cannot merge main/master or deploy production directly
- source snapshots store metadata and masked diff previews instead of full source

Phase 16 adds Observability, Monitoring, Runtime Telemetry & Incident Automation:

- customer/project-scoped telemetry sources for platform API, customer HRM, database, connector, integration and deployment health
- masked runtime telemetry samples, log summaries, health checks, uptime, API latency and deployment health
- configurable monitoring rules and alert rules
- alerts can auto-create incidents, attach SLA, notify and escalate critical incidents
- incidents can convert to Issue, run AI diagnosis as AiRun, create post-incident review and draft knowledge article
- AI only analyzes and recommends; it does not execute production fixes
- mock telemetry provider for local execution

Phase 17 adds Data Import, Migration, Master Data Governance & Validation:

- customer/project-scoped import templates, FileRef uploads, mappings, validation rules, batches and sign-offs
- Excel/CSV metadata support with masked preview for payroll, bank, national ID and personal data
- versioned import templates and column mappings with configurable validation rules
- Dry Run is required before Apply; Phase 17 only allows Test/UAT import, never Production import
- optional pre-import snapshot when a connector is attached, plus reconciliation and migration reports after apply
- AI mapping/validation/error assistance is recorded as AiRun and never applies data without user confirmation
- all import/apply/reconcile/sign-off actions write AuditLog records and use the mock import processor locally

## Current Runtime

This workspace has .NET SDK 9 installed, so the projects target `net9.0` for a buildable baseline. The structure is ready to move to `.NET 10` and EF Core 10 when the SDK/packages are available.

## Backend

```powershell
dotnet run --project src\HrmAiOps.Api\HrmAiOps.Api.csproj
```

API URL:

```text
http://localhost:5000
```

Health check:

```text
GET http://localhost:5000/health
```

The API uses an in-memory store in Phase 1 and seeds one demo customer/project. Important rules are already enforced:

- all major records carry `customerId`
- projects belong to customers
- environments belong to projects
- repositories/database profiles require `secretRef`
- AI actions create `aiRuns`
- key mutations write `auditLogs`
- generated artifacts create `traceLinks`
- production release requires approved change request

## Frontend

```powershell
cd src\frontend\hrm-aiops-web
npm install
npm run dev
```

Frontend URL:

```text
http://localhost:5173
```

Phase 13 UI routes:

```text
/executive-dashboard
/project-dashboard
/customer-health-dashboard
/reporting-exports
```

Phase 14 UI routes:

```text
/integration-hub
/integration-providers
/webhooks
/api-gateway
/automation-triggers
/integration-runs
```

Phase 15 UI routes:

```text
/devops-dashboard
/source-repositories
/pull-requests
/ci-cd-pipelines
/release-packages
/ai-code-governance
```

Phase 16 UI routes:

```text
/observability-dashboard
/runtime-telemetry
/monitoring-rules
/alerts
/incidents
/incident-ai
/post-incident-review
```

Phase 17 UI routes:

```text
/data-migration-dashboard
/import-templates
/import-files
/data-mappings
/validation-rules
/import-batches
/reconciliation-reports
/data-signoff
```

## Database

Phase 1 database schema is in:

```text
database/schema-phase1.sql
```

Phase 2 document lifecycle migration is in:

```text
database/schema-phase2-document-lifecycle.sql
```

Phase 3 AI Orchestrator migration is in:

```text
database/schema-phase3-ai-orchestrator.sql
```

Phase 4 operation management migration is in:

```text
database/schema-phase4-operations-ai-diagnosis.sql
```

Phase 6 controlled Test/UAT apply migration is in:

```text
database/schema-phase6-controlled-test-apply.sql
```

Phase 13 reporting/export/dashboard migration is in:

```text
database/schema-phase13-reporting-export-dashboard.sql
```

Phase 14 integration/webhook/API gateway migration is in:

```text
database/schema-phase14-integration-hub-webhook-api-gateway.sql
```

Phase 15 DevOps/CI/CD/AI code governance migration is in:

```text
database/schema-phase15-devops-cicd-ai-code-governance.sql
```

Phase 16 observability/monitoring/incident automation migration is in:

```text
database/schema-phase16-observability-monitoring-incident-automation.sql
```

Phase 17 data import/migration/master data governance migration is in:

```text
database/schema-phase17-data-import-migration-governance.sql
```

Phase 3 generation flow:

```text
Generate button
-> AI Context Builder loads customer/project scoped source data
-> Data Masking Service masks sensitive values
-> Prompt template + active version are selected
-> Provider-agnostic structured AI provider returns JSON
-> Structured Output Validator validates JSON shape
-> AI proposal is stored for review
-> Consultant Accept creates draft document, Reject keeps audit trail
```

Phase 4 diagnosis flow:

```text
Issue
-> AI Classify / RCA / Fix / CR / Test / Release / KB proposal
-> JSON validation and AI run logging
-> Consultant Accept or Reject
-> Accepted drafts are traced to the issue and audited
```

Phase 6 controlled apply flow:

```text
Accepted FixProposal or ChangeRequest
-> Dry Run
-> Approval gate when risk is High/Critical
-> Rollback Plan
-> Pre-Apply Snapshot
-> Mock Test/UAT Apply
-> Post-Apply Snapshot
-> Snapshot Diff
-> Regression Test Run
-> Release Readiness Report when regression passes
```

## Docker

```powershell
docker compose up --build
```
