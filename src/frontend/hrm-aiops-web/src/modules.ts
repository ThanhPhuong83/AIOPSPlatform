// Shared registry of top-level modules.
// `/` is the main console (App, Phase 1–6); the rest are the standalone phase roots
// wired up in main.tsx. Used by both the console sidebar and the phase chrome bar
// so navigation lives in one place.
export type ModuleLink = { path: string; label: string; group: string };

export const MODULE_LINKS: ModuleLink[] = [
  { path: '/',                          label: 'Console (Phase 1–6)',      group: 'Core' },
  { path: '/production-release',        label: 'Production Releases',      group: 'Delivery' },
  { path: '/governance-analytics',      label: 'Governance & Analytics',   group: 'Governance' },
  { path: '/security-dashboard',        label: 'Security Governance',      group: 'Governance' },
  { path: '/customer-portal',           label: 'Customer Portal',          group: 'Customer' },
  { path: '/billing-dashboard',         label: 'Billing & SLA',            group: 'Customer' },
  { path: '/collaboration-hub',         label: 'Collaboration Hub',        group: 'Customer' },
  { path: '/executive-dashboard',       label: 'Reporting & Dashboards',   group: 'Insights' },
  { path: '/integration-hub',           label: 'Integration Hub',          group: 'Platform' },
  { path: '/devops-dashboard',          label: 'DevOps & CI/CD',           group: 'Platform' },
  { path: '/observability-dashboard',   label: 'Observability',            group: 'Platform' },
  { path: '/data-migration-dashboard',  label: 'Data Migration',           group: 'Platform' }
];

// The module whose page is currently shown, matched by URL prefix.
export function currentModulePath(pathname: string): string {
  const match = MODULE_LINKS.find(m => m.path !== '/' && pathname.startsWith(m.path));
  return match ? match.path : '/';
}
