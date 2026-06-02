import {
  Activity,
  AlertTriangle,
  Bot,
  ClipboardCheck,
  Gauge,
  HeartPulse,
  Radar,
  Siren,
  Stethoscope,
  Timer,
  Workflow
} from 'lucide-react';
import type React from 'react';
import { useEffect, useMemo, useState } from 'react';

type LoadState = 'loading' | 'api' | 'mock';

type ObservabilityState = {
  customerId: string;
  projectId: string;
  customerName: string;
  projectName: string;
  dashboard: any;
  sources: any[];
  telemetry: any[];
  logSummaries: any[];
  monitoringRules: any[];
  alertRules: any[];
  alerts: any[];
  incidents: any[];
  actions: any[];
  diagnoses: any[];
  reviews: any[];
};

const actor = 'security.admin';

export function Phase16Observability() {
  const [route, setRoute] = useState(() => window.location.pathname);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [state, setState] = useState<ObservabilityState>(() => seedState());
  const source = state.sources[0];
  const incident = useMemo(() => state.incidents.find((item) => item.status !== 'Resolved') ?? state.incidents[0], [state.incidents]);

  async function load() {
    try {
      const customers = await api<any[]>('/api/customers');
      const customer = customers[0];
      if (!customer) throw new Error('No customer');
      const projects = await api<any[]>(`/api/customers/${customer.id}/projects`, customer.id);
      const project = projects[0];
      if (!project) throw new Error('No project');
      const scope = `projectId=${project.id}`;
      const base = `/api/customers/${customer.id}/observability`;
      const [dashboard, sources, telemetry, logSummaries, monitoringRules, alertRules, alerts, incidents, actions, diagnoses, reviews] = await Promise.all([
        api<any>(`${base}/dashboard?${scope}`, customer.id),
        api<any[]>(`${base}/sources?${scope}`, customer.id),
        api<any[]>(`${base}/telemetry?${scope}`, customer.id),
        api<any[]>(`${base}/log-summaries?${scope}`, customer.id),
        api<any[]>(`${base}/monitoring-rules?${scope}`, customer.id),
        api<any[]>(`${base}/alert-rules?${scope}`, customer.id),
        api<any[]>(`${base}/alerts?${scope}`, customer.id),
        api<any[]>(`${base}/incidents?${scope}`, customer.id),
        api<any[]>(`${base}/incident-actions?${scope}`, customer.id),
        api<any[]>(`${base}/ai-diagnoses?${scope}`, customer.id),
        api<any[]>(`${base}/post-incident-reviews?${scope}`, customer.id)
      ]);
      setState({ customerId: customer.id, projectId: project.id, customerName: customer.name, projectName: project.name, dashboard, sources, telemetry, logSummaries, monitoringRules, alertRules, alerts, incidents, actions, diagnoses, reviews });
      setLoadState('api');
    } catch {
      setState(seedState());
      setLoadState('mock');
    }
  }

  useEffect(() => {
    load();
  }, []);

  function navigate(nextRoute: string) {
    window.history.pushState(null, '', nextRoute);
    setRoute(nextRoute);
  }

  async function collectMock() {
    if (loadState !== 'api' || !source) return;
    await api(`/api/customers/${state.customerId}/observability/sources/${source.id}/mock-collect?projectId=${state.projectId}`, state.customerId, { method: 'POST' });
    await load();
  }

  async function evaluate() {
    if (loadState !== 'api') return;
    await api(`/api/customers/${state.customerId}/observability/evaluate?projectId=${state.projectId}`, state.customerId, { method: 'POST' });
    await load();
  }

  async function aiDiagnose() {
    if (loadState !== 'api' || !incident) return;
    await api(`/api/customers/${state.customerId}/observability/incidents/${incident.id}/ai-diagnose?projectId=${state.projectId}`, state.customerId, { method: 'POST' });
    await load();
  }

  async function convertToIssue() {
    if (loadState !== 'api' || !incident) return;
    await api(`/api/customers/${state.customerId}/observability/incidents/${incident.id}/convert-to-issue?projectId=${state.projectId}`, state.customerId, { method: 'POST' });
    await load();
  }

  async function postReview() {
    if (loadState !== 'api' || !incident) return;
    await api(`/api/customers/${state.customerId}/observability/incidents/${incident.id}/post-review?projectId=${state.projectId}`, state.customerId, { method: 'POST' });
    await load();
  }

  async function resolveIncident() {
    if (loadState !== 'api' || !incident) return;
    await api(`/api/customers/${state.customerId}/observability/incidents/${incident.id}/resolve?projectId=${state.projectId}`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({ resolution: 'Mitigated through approved runbook and monitored recovery.' })
    });
    await load();
  }

  return (
    <main className="phase7-shell">
      <aside className="sidebar phase7-nav">
        <div className="brand">
          <div className="brand-mark">P16</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>Observability</span>
          </div>
        </div>
        {nav.map((item) => (
          <button key={item.route} onClick={() => navigate(item.route)}>
            {item.icon} {item.label}
          </button>
        ))}
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <p>Phase 16 / {state.customerName} / {state.projectName}</p>
            <h2>{title(route)}</h2>
          </div>
          <span className={`status ${loadState === 'api' ? 'ok' : 'idle'}`}>
            {loadState === 'loading' ? 'Loading API' : loadState === 'api' ? 'API connected' : 'Local mock mode'}
          </span>
        </header>

        <section className="panel phase16-toolbar">
          <button onClick={collectMock}><Radar size={16} /> Collect</button>
          <button onClick={evaluate}><Activity size={16} /> Evaluate</button>
          <button onClick={aiDiagnose}><Bot size={16} /> Diagnose</button>
          <button onClick={convertToIssue}><AlertTriangle size={16} /> Issue</button>
          <button onClick={postReview}><ClipboardCheck size={16} /> Review</button>
          <button onClick={resolveIncident}><HeartPulse size={16} /> Resolve</button>
        </section>

        {route.includes('/runtime-telemetry') ? <Telemetry state={state} /> :
          route.includes('/monitoring-rules') ? <Rules state={state} /> :
            route.includes('/alerts') ? <Alerts state={state} /> :
              route.includes('/incidents') ? <Incidents state={state} /> :
                route.includes('/incident-ai') ? <Diagnosis state={state} /> :
                  route.includes('/post-incident-review') ? <Reviews state={state} /> :
                    <Dashboard state={state} />}
      </section>
    </main>
  );
}

const nav = [
  { route: '/observability-dashboard', label: 'Dashboard', icon: <Gauge size={16} /> },
  { route: '/runtime-telemetry', label: 'Telemetry', icon: <Activity size={16} /> },
  { route: '/monitoring-rules', label: 'Rules', icon: <Workflow size={16} /> },
  { route: '/alerts', label: 'Alerts', icon: <Siren size={16} /> },
  { route: '/incidents', label: 'Incidents', icon: <AlertTriangle size={16} /> },
  { route: '/incident-ai', label: 'AI Diagnosis', icon: <Bot size={16} /> },
  { route: '/post-incident-review', label: 'Reviews', icon: <ClipboardCheck size={16} /> }
];

function Dashboard({ state }: { state: ObservabilityState }) {
  const data = state.dashboard;
  return (
    <>
      <section className="metric-grid phase7-metrics">
        <Metric label="Sources" value={data.telemetrySources} icon={<Radar size={18} />} />
        <Metric label="Healthy" value={data.healthySources} icon={<HeartPulse size={18} />} />
        <Metric label="Degraded" value={data.degradedSources} icon={<Timer size={18} />} />
        <Metric label="Unhealthy" value={data.unhealthySources} icon={<Stethoscope size={18} />} />
        <Metric label="Open Alerts" value={data.openAlerts} icon={<Siren size={18} />} />
        <Metric label="Open Incidents" value={data.openIncidents} icon={<AlertTriangle size={18} />} />
      </section>
      <section className="content-grid two">
        <Panel title="Latest Telemetry" icon={<Activity size={18} />}><Rows items={data.latestTelemetry ?? state.telemetry} primary="summary" secondary="healthStatus" /></Panel>
        <Panel title="Latest Incidents" icon={<AlertTriangle size={18} />}><Rows items={data.latestIncidents ?? state.incidents} primary="title" secondary="status" /></Panel>
      </section>
    </>
  );
}

function Telemetry({ state }: { state: ObservabilityState }) {
  return <section className="content-grid two"><Panel title="Telemetry Sources" icon={<Radar size={18} />}><Rows items={state.sources} primary="name" secondary="sourceType" /></Panel><Panel title="Runtime Samples" icon={<Activity size={18} />}><Rows items={state.telemetry} primary="summary" secondary="healthStatus" /></Panel><Panel title="Log Summaries" icon={<Stethoscope size={18} />}><Rows items={state.logSummaries} primary="maskedSummary" secondary="errorCount" /></Panel></section>;
}

function Rules({ state }: { state: ObservabilityState }) {
  return <section className="content-grid two"><Panel title="Monitoring Rules" icon={<Workflow size={18} />}><Rows items={state.monitoringRules} primary="name" secondary="severity" /></Panel><Panel title="Alert Rules" icon={<Siren size={18} />}><Rows items={state.alertRules} primary="alertKey" secondary="recipientRef" /></Panel></section>;
}

function Alerts({ state }: { state: ObservabilityState }) {
  return <section className="content-grid"><Panel title="Alert Events" icon={<Siren size={18} />}><Rows items={state.alerts} primary="title" secondary="severity" /></Panel></section>;
}

function Incidents({ state }: { state: ObservabilityState }) {
  return <section className="content-grid two"><Panel title="Incidents" icon={<AlertTriangle size={18} />}><Rows items={state.incidents} primary="title" secondary="priority" /></Panel><Panel title="Incident Actions" icon={<ClipboardCheck size={18} />}><Rows items={state.actions} primary="summary" secondary="actionType" /></Panel></section>;
}

function Diagnosis({ state }: { state: ObservabilityState }) {
  return <section className="content-grid"><Panel title="AI Incident Diagnosis" icon={<Bot size={18} />}><Rows items={state.diagnoses} primary="rootCauseHypothesis" secondary="confidenceScore" /></Panel></section>;
}

function Reviews({ state }: { state: ObservabilityState }) {
  return <section className="content-grid"><Panel title="Post-Incident Reviews" icon={<ClipboardCheck size={18} />}><Rows items={state.reviews} primary="reviewNo" secondary="status" /></Panel></section>;
}

function Metric({ label, value, icon }: { label: string; value: any; icon: React.ReactNode }) {
  return <div className="metric">{icon}<span>{label}</span><strong>{Math.round(Number(value ?? 0))}</strong></div>;
}

function Panel({ title, icon, children }: { title: string; icon: React.ReactNode; children: React.ReactNode }) {
  return <section className="panel"><header>{icon}<h3>{title}</h3></header>{children}</section>;
}

function Rows({ items, primary, secondary }: { items: any[]; primary: string; secondary: string }) {
  if (!items?.length) return <p className="empty">No observability data.</p>;
  return (
    <div className="action-list">
      {items.map((item, index) => (
        <div className="list-row" key={item.id ?? index}>
          <div>
            <strong>{item[primary] ?? item.title ?? item.name}</strong>
            <span>{String(item[secondary] ?? item.status ?? '')}</span>
            <small>{item.correlationId ? `corr=${item.correlationId} trace=${item.traceId}` : item.incidentNo ?? item.sourceKey ?? item.createdAt ?? ''}</small>
          </div>
          <StatusBadge text={String(item.status ?? item.severity ?? item.healthStatus ?? 'Active')} />
        </div>
      ))}
    </div>
  );
}

function StatusBadge({ text }: { text: string }) {
  return <span className={`phase7-badge ${text.toLowerCase().replaceAll(' ', '')}`}>{text}</span>;
}

async function api<T>(path: string, customerId?: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      'X-User-Id': actor,
      ...(customerId ? { 'X-Customer-Id': customerId } : {}),
      ...(init?.headers ?? {})
    }
  });
  if (!response.ok) throw new Error(await response.text());
  return response.json() as Promise<T>;
}

function title(route: string) {
  return nav.find((item) => item.route === route)?.label ?? 'Dashboard';
}

function seedState(): ObservabilityState {
  const telemetry = [
    { id: 'tel-1', summary: 'Production API latency is healthy.', healthStatus: 'Healthy', correlationId: 'demo-corr', traceId: 'demo-trace' },
    { id: 'tel-2', summary: 'Production deployment validation failed; masked values removed.', healthStatus: 'Unhealthy', correlationId: 'demo-corr-2', traceId: 'demo-trace-2' }
  ];
  const incident = { id: 'inc-1', incidentNo: 'INC-00001', title: 'Critical production deployment health degradation', status: 'Investigating', priority: 'P0', severity: 'Critical' };
  return {
    customerId: 'demo',
    projectId: 'project-1',
    customerName: 'Demo Customer',
    projectName: 'HRM AI Ops Pilot',
    dashboard: { telemetrySources: 3, healthySources: 1, degradedSources: 0, unhealthySources: 1, samples: 2, openAlerts: 1, criticalAlerts: 1, openIncidents: 1, criticalIncidents: 1, latestTelemetry: telemetry, latestAlerts: [], latestIncidents: [incident] },
    sources: [{ id: 'src-1', name: 'Production Deployment Health', sourceType: 'Deployment', sourceKey: 'deployment.health.production' }],
    telemetry,
    logSummaries: [{ id: 'log-1', maskedSummary: 'Top production errors are masked and summarized only.', errorCount: 14 }],
    monitoringRules: [{ id: 'rule-1', name: 'Production deployment health critical', severity: 'Critical' }],
    alertRules: [{ id: 'alert-rule-1', alertKey: 'critical.incident.sre', recipientRef: 'sre.oncall' }],
    alerts: [{ id: 'alert-1', title: 'Production deployment health critical', severity: 'Critical', status: 'Open' }],
    incidents: [incident],
    actions: [{ id: 'act-1', summary: 'Seeded critical notification and escalation.', actionType: 'Escalate' }],
    diagnoses: [{ id: 'diag-1', rootCauseHypothesis: 'Likely deployment validation regression after production package preparation.', confidenceScore: 0.78 }],
    reviews: []
  };
}
