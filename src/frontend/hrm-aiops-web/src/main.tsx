import React from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { Phase7ProductionReleases } from './Phase7ProductionReleases';
import { Phase8GovernanceAnalytics } from './Phase8GovernanceAnalytics';
import { Phase9SecurityGovernance } from './Phase9SecurityGovernance';
import { Phase10CustomerPortal } from './Phase10CustomerPortal';
import { Phase11CustomerPortal } from './Phase11CustomerPortal';
import { Phase12CollaborationHub } from './Phase12CollaborationHub';
import { Phase13ReportingDashboards } from './Phase13ReportingDashboards';
import { Phase14IntegrationHub } from './Phase14IntegrationHub';
import { Phase15DevOpsGovernance } from './Phase15DevOpsGovernance';
import { Phase16Observability } from './Phase16Observability';
import { Phase17DataMigration } from './Phase17DataMigration';
import './styles.css';

const root = window.location.pathname.startsWith('/production-release')
  ? <Phase7ProductionReleases />
  : window.location.pathname.startsWith('/governance-analytics') || window.location.pathname.startsWith('/knowledge-learning')
    ? <Phase8GovernanceAnalytics />
    : window.location.pathname.startsWith('/security-dashboard') || window.location.pathname.startsWith('/tenant-security') || window.location.pathname.startsWith('/compliance-evidence')
      ? <Phase9SecurityGovernance />
      : window.location.pathname.startsWith('/customer-portal')
        ? <Phase11CustomerPortal />
      : window.location.pathname.startsWith('/collaboration-hub') || window.location.pathname.startsWith('/workflow-automation') || window.location.pathname.startsWith('/notification-center')
        ? <Phase12CollaborationHub />
      : window.location.pathname.startsWith('/executive-dashboard') || window.location.pathname.startsWith('/project-dashboard') || window.location.pathname.startsWith('/customer-health-dashboard') || window.location.pathname.startsWith('/reporting-exports')
        ? <Phase13ReportingDashboards />
      : window.location.pathname.startsWith('/integration-hub') || window.location.pathname.startsWith('/integration-providers') || window.location.pathname.startsWith('/webhooks') || window.location.pathname.startsWith('/api-gateway') || window.location.pathname.startsWith('/automation-triggers') || window.location.pathname.startsWith('/integration-runs')
        ? <Phase14IntegrationHub />
      : window.location.pathname.startsWith('/devops-dashboard') || window.location.pathname.startsWith('/source-repositories') || window.location.pathname.startsWith('/pull-requests') || window.location.pathname.startsWith('/ci-cd-pipelines') || window.location.pathname.startsWith('/release-packages') || window.location.pathname.startsWith('/ai-code-governance')
        ? <Phase15DevOpsGovernance />
      : window.location.pathname.startsWith('/observability-dashboard') || window.location.pathname.startsWith('/runtime-telemetry') || window.location.pathname.startsWith('/monitoring-rules') || window.location.pathname.startsWith('/alerts') || window.location.pathname.startsWith('/incidents') || window.location.pathname.startsWith('/incident-ai') || window.location.pathname.startsWith('/post-incident-review')
        ? <Phase16Observability />
      : window.location.pathname.startsWith('/data-migration-dashboard') || window.location.pathname.startsWith('/import-templates') || window.location.pathname.startsWith('/import-files') || window.location.pathname.startsWith('/data-mappings') || window.location.pathname.startsWith('/validation-rules') || window.location.pathname.startsWith('/import-batches') || window.location.pathname.startsWith('/reconciliation-reports') || window.location.pathname.startsWith('/data-signoff')
        ? <Phase17DataMigration />
      : window.location.pathname.startsWith('/billing-dashboard') || window.location.pathname.startsWith('/sla-dashboard')
        ? <Phase10CustomerPortal />
    : <App />;

createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    {root}
  </React.StrictMode>
);
