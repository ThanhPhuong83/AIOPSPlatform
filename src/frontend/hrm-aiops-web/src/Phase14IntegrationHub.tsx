import {
  BellRing,
  Cable,
  GitBranch,
  Globe2,
  KeyRound,
  Network,
  Play,
  RefreshCw,
  Route,
  ShieldCheck,
  Webhook
} from 'lucide-react';
import type React from 'react';
import { useEffect, useMemo, useState } from 'react';

type LoadState = 'loading' | 'api' | 'mock';

type HubState = {
  customerId: string;
  projectId: string;
  customerName: string;
  projectName: string;
  dashboard: any;
  providers: any[];
  endpoints: any[];
  mappings: any[];
  eventSubscriptions: any[];
  outboundWebhooks: any[];
  gatewayRoutes: any[];
  runs: any[];
  triggers: any[];
};

const actor = 'security.admin';

export function Phase14IntegrationHub() {
  const [route, setRoute] = useState(() => window.location.pathname);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [state, setState] = useState<HubState>(() => seedState());
  const outboundEndpoint = useMemo(() => state.endpoints.find((item) => item.direction === 'Outbound') ?? state.endpoints[0], [state.endpoints]);
  const inboundEndpoint = useMemo(() => state.endpoints.find((item) => item.direction === 'Inbound'), [state.endpoints]);
  const gatewayRoute = state.gatewayRoutes[0];

  async function load() {
    try {
      const customers = await api<any[]>('/api/customers');
      const customer = customers[0];
      if (!customer) throw new Error('No customer');
      const projects = await api<any[]>(`/api/customers/${customer.id}/projects`, customer.id);
      const project = projects[0];
      if (!project) throw new Error('No project');
      const scope = `projectId=${project.id}`;
      const [dashboard, providers, endpoints, mappings, eventSubscriptions, outboundWebhooks, gatewayRoutes, runs, triggers] = await Promise.all([
        api<any>(`/api/customers/${customer.id}/integrations/dashboard?${scope}`, customer.id),
        api<any[]>(`/api/customers/${customer.id}/integrations/providers?${scope}`, customer.id),
        api<any[]>(`/api/customers/${customer.id}/integrations/endpoints?${scope}`, customer.id),
        api<any[]>(`/api/customers/${customer.id}/integrations/payload-mappings?${scope}`, customer.id),
        api<any[]>(`/api/customers/${customer.id}/integrations/event-subscriptions?${scope}`, customer.id),
        api<any[]>(`/api/customers/${customer.id}/integrations/outbound-webhooks?${scope}`, customer.id),
        api<any[]>(`/api/customers/${customer.id}/integrations/gateway-routes?${scope}`, customer.id),
        api<any[]>(`/api/customers/${customer.id}/integrations/runs?${scope}`, customer.id),
        api<any[]>(`/api/customers/${customer.id}/integrations/automation-triggers?${scope}`, customer.id)
      ]);
      setState({ customerId: customer.id, projectId: project.id, customerName: customer.name, projectName: project.name, dashboard, providers, endpoints, mappings, eventSubscriptions, outboundWebhooks, gatewayRoutes, runs, triggers });
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

  async function runOutbound(forceFailure = false) {
    if (loadState !== 'api' || !outboundEndpoint) return;
    await api(`/api/customers/${state.customerId}/integrations/endpoints/${outboundEndpoint.id}/outbound-test`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({
        projectId: state.projectId,
        eventType: outboundEndpoint.endpointKey?.includes('slack') ? 'SlaBreached' : 'IssueCreated',
        payloadJson: JSON.stringify({ title: 'Leave approval integration smoke', email: 'employee@example.com', token: 'secret=abc123', forceFailure }),
        correlationId: `ui-${Date.now()}`
      })
    });
    await load();
  }

  async function receiveWebhook() {
    if (loadState !== 'api' || !inboundEndpoint) return;
    await api(`/api/customers/${state.customerId}/integrations/providers/${inboundEndpoint.providerId}/webhooks/inbound`, state.customerId, {
      method: 'POST',
      headers: { 'X-Webhook-Signature': 'mock-valid-signature' },
      body: JSON.stringify({
        projectId: state.projectId,
        endpointId: inboundEndpoint.id,
        eventType: 'WebhookReceived',
        payloadJson: JSON.stringify({ source: 'customer-hrm-api', employeeEmail: 'person@example.com', issue: 'Leave balance mismatch' }),
        correlationId: `webhook-${Date.now()}`
      })
    });
    await load();
  }

  async function invokeGateway() {
    if (loadState !== 'api' || !gatewayRoute) return;
    await api(`/api/customers/${state.customerId}/integrations/gateway-routes/${gatewayRoute.id}/invoke`, state.customerId, {
      method: 'POST',
      headers: { 'X-Gateway-Token-Ref': gatewayRoute.tokenSecretRef },
      body: JSON.stringify({
        projectId: state.projectId,
        externalSystem: gatewayRoute.allowedExternalSystem,
        payloadJson: JSON.stringify({ title: 'External issue via gateway', customerScoped: true }),
        correlationId: `gateway-${Date.now()}`
      })
    });
    await load();
  }

  async function processRetries() {
    if (loadState !== 'api') return;
    await api(`/api/customers/${state.customerId}/integrations/retries/run-due?projectId=${state.projectId}`, state.customerId, { method: 'POST' });
    await load();
  }

  return (
    <main className="phase7-shell">
      <aside className="sidebar phase7-nav">
        <div className="brand">
          <div className="brand-mark">P14</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>Integration Hub</span>
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
            <p>Phase 14 / {state.customerName} / {state.projectName}</p>
            <h2>{title(route)}</h2>
          </div>
          <span className={`status ${loadState === 'api' ? 'ok' : 'idle'}`}>
            {loadState === 'loading' ? 'Loading API' : loadState === 'api' ? 'API connected' : 'Local mock mode'}
          </span>
        </header>

        <section className="panel phase14-toolbar">
          <button onClick={() => runOutbound(false)}><Play size={16} /> Outbound</button>
          <button onClick={() => runOutbound(true)}><BellRing size={16} /> Fail + Task</button>
          <button onClick={receiveWebhook}><Webhook size={16} /> Inbound</button>
          <button onClick={invokeGateway}><ShieldCheck size={16} /> Gateway</button>
          <button onClick={processRetries}><RefreshCw size={16} /> Retry Due</button>
        </section>

        {route.includes('/webhooks') ? <Webhooks state={state} /> :
          route.includes('/api-gateway') ? <Gateway state={state} /> :
            route.includes('/automation-triggers') ? <Automation state={state} /> :
              route.includes('/integration-runs') ? <Runs state={state} /> :
                route.includes('/integration-providers') ? <Providers state={state} /> :
                  <Dashboard state={state} />}
      </section>
    </main>
  );
}

const nav = [
  { route: '/integration-hub', label: 'Dashboard', icon: <Network size={16} /> },
  { route: '/integration-providers', label: 'Providers', icon: <Cable size={16} /> },
  { route: '/webhooks', label: 'Webhooks', icon: <Webhook size={16} /> },
  { route: '/api-gateway', label: 'API Gateway', icon: <Route size={16} /> },
  { route: '/automation-triggers', label: 'Automation', icon: <GitBranch size={16} /> },
  { route: '/integration-runs', label: 'Run History', icon: <RefreshCw size={16} /> }
];

function Dashboard({ state }: { state: HubState }) {
  const data = state.dashboard;
  return (
    <>
      <section className="metric-grid phase7-metrics">
        <Metric label="Providers" value={data.providers} icon={<Cable size={18} />} />
        <Metric label="Endpoints" value={data.activeEndpoints} icon={<Globe2 size={18} />} />
        <Metric label="Inbound" value={data.inboundWebhooks} icon={<Webhook size={18} />} />
        <Metric label="Outbound" value={data.outboundSubscriptions} icon={<BellRing size={18} />} />
        <Metric label="Gateway" value={data.gatewayRoutes} icon={<Route size={18} />} />
        <Metric label="Runs" value={data.runs} icon={<RefreshCw size={18} />} />
        <Metric label="Failures" value={data.failedRuns} icon={<ShieldCheck size={18} />} />
        <Metric label="Success %" value={data.successRate} icon={<Network size={18} />} />
      </section>
      <section className="content-grid two">
        <Panel title="Latest Runs" icon={<RefreshCw size={18} />}><Rows items={data.latestRuns ?? state.runs} primary="requestSummary" secondary="status" /></Panel>
        <Panel title="Latest Errors" icon={<BellRing size={18} />}><Rows items={data.latestErrors ?? []} primary="requestSummary" secondary="errorMessage" /></Panel>
      </section>
    </>
  );
}

function Providers({ state }: { state: HubState }) {
  return <section className="content-grid two"><Panel title="Providers" icon={<Cable size={18} />}><Rows items={state.providers} primary="name" secondary="category" /></Panel><Panel title="Endpoints" icon={<Globe2 size={18} />}><Rows items={state.endpoints} primary="name" secondary="direction" /></Panel><Panel title="Payload Mappings" icon={<GitBranch size={18} />}><Rows items={state.mappings} primary="mappingKey" secondary="targetSystem" /></Panel></section>;
}

function Webhooks({ state }: { state: HubState }) {
  return <section className="content-grid two"><Panel title="Inbound Subscriptions" icon={<Webhook size={18} />}><Rows items={state.eventSubscriptions} primary="subscriptionKey" secondary="eventType" /></Panel><Panel title="Outbound Webhooks" icon={<BellRing size={18} />}><Rows items={state.outboundWebhooks} primary="targetUrl" secondary="eventType" /></Panel></section>;
}

function Gateway({ state }: { state: HubState }) {
  return <section className="content-grid"><Panel title="API Gateway Routes" icon={<ShieldCheck size={18} />}><Rows items={state.gatewayRoutes} primary="routeKey" secondary="allowedExternalSystem" /></Panel></section>;
}

function Automation({ state }: { state: HubState }) {
  return <section className="content-grid two"><Panel title="Automation Triggers" icon={<GitBranch size={18} />}><Rows items={state.triggers} primary="triggerKey" secondary="actionType" /></Panel><Panel title="Failure Policy" icon={<KeyRound size={18} />}><div className="phase7-summary"><strong>Failures create notification/task when configured.</strong><span>Payloads are masked, secrets are secret_ref only, and all runs carry correlationId and traceId.</span></div></Panel></section>;
}

function Runs({ state }: { state: HubState }) {
  return <section className="content-grid"><Panel title="Integration Run History" icon={<RefreshCw size={18} />}><Rows items={state.runs} primary="requestSummary" secondary="status" /></Panel></section>;
}

function Metric({ label, value, icon }: { label: string; value: any; icon: React.ReactNode }) {
  return <div className="metric">{icon}<span>{label}</span><strong>{value ?? 0}</strong></div>;
}

function Panel({ title, icon, children }: { title: string; icon: React.ReactNode; children: React.ReactNode }) {
  return <section className="panel"><header>{icon}<h3>{title}</h3></header>{children}</section>;
}

function Rows({ items, primary, secondary }: { items: any[]; primary: string; secondary: string }) {
  if (!items?.length) return <p className="empty">No integration data.</p>;
  return (
    <div className="action-list">
      {items.map((item, index) => (
        <div className="list-row" key={item.id ?? index}>
          <div>
            <strong>{item[primary] ?? item.name ?? item.routeKey}</strong>
            <span>{item[secondary] ?? item.status}</span>
            <small>{item.correlationId ? `corr=${item.correlationId} trace=${item.traceId}` : item.pathOrUrl ?? item.publicPath ?? item.createdAt ?? ''}</small>
          </div>
          <StatusBadge text={String(item.status ?? item.direction ?? item.category ?? 'Active')} />
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

function seedState(): HubState {
  const providers = [
    { id: 'jira', providerKey: 'jira', name: 'Jira', category: 'IssueTracking' },
    { id: 'slack', providerKey: 'slack', name: 'Slack', category: 'Messaging' },
    { id: 'hrm', providerKey: 'customer-hrm-api', name: 'Customer HRM API', category: 'CustomerHrmApi' }
  ];
  const endpoints = [
    { id: 'jira-endpoint', providerId: 'jira', name: 'Create Jira Issue', endpointKey: 'jira.issue.create', direction: 'Outbound', pathOrUrl: '/rest/api/3/issue' },
    { id: 'hrm-webhook', providerId: 'hrm', name: 'Customer HRM Issue Webhook', endpointKey: 'customer-hrm.issue.webhook', direction: 'Inbound', pathOrUrl: '/webhooks/customer-hrm/issues' }
  ];
  const runs = [{ id: 'run-1', requestSummary: 'Seeded outbound Jira issue sync.', status: 'Succeeded', correlationId: 'demo-correlation', traceId: 'demo-trace' }];
  return {
    customerId: 'demo',
    projectId: 'project-1',
    customerName: 'Demo Customer',
    projectName: 'HRM Stabilization',
    dashboard: { providers: 13, activeEndpoints: 4, inboundWebhooks: 1, outboundSubscriptions: 2, gatewayRoutes: 1, runs: 1, failedRuns: 0, retryingRuns: 0, successRate: 100, latestRuns: runs, latestErrors: [] },
    providers,
    endpoints,
    mappings: [{ id: 'map-1', mappingKey: 'issue.to.jira', targetSystem: 'Jira', eventType: 'IssueCreated' }],
    eventSubscriptions: [{ id: 'sub-1', subscriptionKey: 'customer-hrm-webhook', eventType: 'WebhookReceived' }],
    outboundWebhooks: [{ id: 'wh-1', targetUrl: 'https://hooks.slack.com/services/mock', eventType: 'SlaBreached' }],
    gatewayRoutes: [{ id: 'gw-1', routeKey: 'external.issue.create', allowedExternalSystem: 'customer-hrm-api', tokenSecretRef: 'secret://integrations/customer-hrm/gateway-token', publicPath: '/gateway/hrm-aiops/issues' }],
    runs,
    triggers: [{ id: 'trg-1', triggerKey: 'integration.failure.task', actionType: 'CreateTask', eventType: 'SlaBreached' }]
  };
}
