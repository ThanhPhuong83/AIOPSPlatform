import React, { lazy, Suspense, ComponentType } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { PhaseChrome } from './PhaseChrome';
import { restoreSession } from './session';
import './styles.css';

// Apply the saved theme before first paint so every page (console + phase roots)
// opens in the user's chosen light/dark mode, and re-arm the api auth header from
// the persisted login so phase roots can call the API too.
document.documentElement.setAttribute(
  'data-theme', localStorage.getItem('theme') === 'dark' ? 'dark' : 'light'
);
const session = restoreSession();

// Each phase is its own standalone page; lazy-load so a given page only ships its
// own chunk instead of bundling all 17 phases together.
const lazyPhase = (load: () => Promise<{ [k: string]: ComponentType }>, name: string) =>
  lazy(async () => ({ default: (await load())[name] }));

type Route = { prefixes: string[]; component: ComponentType };
const PHASE_ROUTES: Route[] = [
  { prefixes: ['/production-release'], component: lazyPhase(() => import('./Phase7ProductionReleases'), 'Phase7ProductionReleases') },
  { prefixes: ['/governance-analytics', '/knowledge-learning'], component: lazyPhase(() => import('./Phase8GovernanceAnalytics'), 'Phase8GovernanceAnalytics') },
  { prefixes: ['/security-dashboard', '/tenant-security', '/compliance-evidence'], component: lazyPhase(() => import('./Phase9SecurityGovernance'), 'Phase9SecurityGovernance') },
  { prefixes: ['/customer-portal'], component: lazyPhase(() => import('./Phase11CustomerPortal'), 'Phase11CustomerPortal') },
  { prefixes: ['/billing-dashboard', '/sla-dashboard'], component: lazyPhase(() => import('./Phase10CustomerPortal'), 'Phase10CustomerPortal') },
  { prefixes: ['/collaboration-hub', '/workflow-automation', '/notification-center'], component: lazyPhase(() => import('./Phase12CollaborationHub'), 'Phase12CollaborationHub') },
  { prefixes: ['/executive-dashboard', '/project-dashboard', '/customer-health-dashboard', '/reporting-exports'], component: lazyPhase(() => import('./Phase13ReportingDashboards'), 'Phase13ReportingDashboards') },
  { prefixes: ['/integration-hub', '/integration-providers', '/webhooks', '/api-gateway', '/automation-triggers', '/integration-runs'], component: lazyPhase(() => import('./Phase14IntegrationHub'), 'Phase14IntegrationHub') },
  { prefixes: ['/devops-dashboard', '/source-repositories', '/pull-requests', '/ci-cd-pipelines', '/release-packages', '/ai-code-governance'], component: lazyPhase(() => import('./Phase15DevOpsGovernance'), 'Phase15DevOpsGovernance') },
  { prefixes: ['/observability-dashboard', '/runtime-telemetry', '/monitoring-rules', '/alerts', '/incidents', '/incident-ai', '/post-incident-review'], component: lazyPhase(() => import('./Phase16Observability'), 'Phase16Observability') },
  { prefixes: ['/data-migration-dashboard', '/import-templates', '/import-files', '/data-mappings', '/validation-rules', '/import-batches', '/reconciliation-reports', '/data-signoff'], component: lazyPhase(() => import('./Phase17DataMigration'), 'Phase17DataMigration') }
];

const path = window.location.pathname;
const match = PHASE_ROUTES.find(r => r.prefixes.some(p => path.startsWith(p)));

// Phase pages require a login; without one, bounce to the console (which shows
// the login form) so they can't be reached by typing the URL directly.
if (match && !session) {
  window.location.replace('/');
}

const Phase = match?.component;

createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    {Phase && session
      ? <Suspense fallback={<div className="route-loading">Đang tải…</div>}>
          <PhaseChrome><Phase /></PhaseChrome>
        </Suspense>
      : <App />}
  </React.StrictMode>
);
