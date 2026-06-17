import {
  BarChart3,
  Bot,
  Download,
  FileSpreadsheet,
  FileText,
  Gauge,
  HeartPulse,
  LayoutDashboard,
  LineChart,
  Share2
} from 'lucide-react';
import type React from 'react';
import { useEffect, useMemo, useState } from 'react';
import { pt } from './phaseI18n';

type LoadState = 'loading' | 'api' | 'mock';

type ReportCatalogItem = {
  templateId: string;
  templateKey: string;
  name: string;
  reportType: string;
  defaultFormat: string;
  activeVersion: number;
  maxClassification: string;
  requiresPermission: boolean;
  requiredPermission?: string;
};

type ReportFile = {
  id: string;
  reportType: string;
  outputFormat: string;
  fileName: string;
  storageRef: string;
  sizeBytes: number;
  visibility: string;
  maskingApplied: boolean;
  containsSensitiveData: boolean;
  createdAt: string;
};

type ReportJob = {
  id: string;
  reportType: string;
  outputFormat: string;
  status: string;
  visibility: string;
  requestedBy: string;
  maskingApplied: boolean;
  createdAt: string;
};

type Phase13State = {
  customerId: string;
  projectId: string;
  customerName: string;
  projectName: string;
  executive: any;
  project: any;
  health: any;
  catalog: ReportCatalogItem[];
  jobs: ReportJob[];
  files: ReportFile[];
  shared: ReportFile[];
  summary?: any;
};

const actor = 'security.admin';

export function Phase13ReportingDashboards() {
  const [route, setRoute] = useState(() => window.location.pathname);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [dateFrom, setDateFrom] = useState(() => toDateInput(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)));
  const [dateTo, setDateTo] = useState(() => toDateInput(new Date()));
  const [state, setState] = useState<Phase13State>(() => seedState());
  const [selectedTemplateId, setSelectedTemplateId] = useState('');
  const selectedTemplate = useMemo(
    () => state.catalog.find((item) => item.templateId === selectedTemplateId) ?? state.catalog[0],
    [selectedTemplateId, state.catalog]
  );

  async function load(nextFrom = dateFrom, nextTo = dateTo) {
    try {
      const customers = await api<any[]>('/api/customers');
      const customer = customers[0];
      if (!customer) throw new Error('No customer');
      const projects = await api<any[]>(`/api/customers/${customer.id}/projects`, customer.id);
      const project = projects[0];
      if (!project) throw new Error('No project');
      const range = `dateFrom=${encodeURIComponent(asIsoStart(nextFrom))}&dateTo=${encodeURIComponent(asIsoEnd(nextTo))}`;
      const [executive, projectDashboard, health, catalog, exports, shared] = await Promise.all([
        api<any>(`/api/customers/${customer.id}/projects/${project.id}/dashboards/executive?${range}`, customer.id),
        api<any>(`/api/customers/${customer.id}/projects/${project.id}/dashboards/project?${range}`, customer.id),
        api<any>(`/api/customers/${customer.id}/projects/${project.id}/dashboards/customer-health?${range}`, customer.id),
        api<ReportCatalogItem[]>(`/api/customers/${customer.id}/projects/${project.id}/reporting/catalog`, customer.id),
        api<{ jobs: ReportJob[]; files: ReportFile[] }>(`/api/customers/${customer.id}/projects/${project.id}/reporting/exports`, customer.id),
        api<ReportFile[]>(`/api/portal/customers/${customer.id}/projects/${project.id}/reports/shared`, customer.id)
      ]);
      const nextState = {
        customerId: customer.id,
        projectId: project.id,
        customerName: customer.name,
        projectName: project.name,
        executive,
        project: projectDashboard,
        health,
        catalog,
        jobs: exports.jobs ?? [],
        files: exports.files ?? [],
        shared
      };
      setState(nextState);
      setSelectedTemplateId((current) => current || catalog[0]?.templateId || '');
      setLoadState('api');
    } catch {
      setState(seedState());
      setSelectedTemplateId(seedState().catalog[0]?.templateId ?? '');
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

  async function refreshRange() {
    await load(dateFrom, dateTo);
  }

  async function generateSummary() {
    if (loadState !== 'api') return;
    const summary = await api<any>(`/api/customers/${state.customerId}/projects/${state.projectId}/dashboards/ai-summary`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({ dateFrom: asIsoStart(dateFrom), dateTo: asIsoEnd(dateTo) })
    });
    setState((current) => ({ ...current, summary }));
    await load();
  }

  async function generateExport(visibility: string) {
    if (loadState !== 'api' || !selectedTemplate) return;
    await api(`/api/customers/${state.customerId}/projects/${state.projectId}/reporting/exports`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({
        templateId: selectedTemplate.templateId,
        reportType: selectedTemplate.reportType,
        outputFormat: selectedTemplate.defaultFormat,
        visibility,
        dateFrom: asIsoStart(dateFrom),
        dateTo: asIsoEnd(dateTo),
        externalExport: visibility !== 'InternalOnly'
      })
    });
    await load();
  }

  return (
    <main className="phase7-shell">
      <aside className="sidebar phase7-nav">
        <div className="brand">
          <div className="brand-mark">P13</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>{pt('Reporting')}</span>
          </div>
        </div>
        {nav.map((item) => (
          <button key={item.route} onClick={() => navigate(item.route)}>
            {item.icon} {pt(item.label)}
          </button>
        ))}
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <p>Phase 13 / {state.customerName} / {state.projectName}</p>
            <h2>{pt(title(route))}</h2>
          </div>
          <span className={`status ${loadState === 'api' ? 'ok' : 'idle'}`}>
            {loadState === 'loading' ? pt('Loading API') : loadState === 'api' ? pt('API connected') : pt('Local mock mode')}
          </span>
        </header>

        <section className="panel phase13-toolbar">
          <label>
            {pt('From')}
            <input type="date" value={dateFrom} onChange={(event) => setDateFrom(event.target.value)} />
          </label>
          <label>
            {pt('To')}
            <input type="date" value={dateTo} onChange={(event) => setDateTo(event.target.value)} />
          </label>
          <label>
            {pt('Report')}
            <select value={selectedTemplateId} onChange={(event) => setSelectedTemplateId(event.target.value)}>
              {state.catalog.map((item) => (
                <option key={item.templateId} value={item.templateId}>{item.name}</option>
              ))}
            </select>
          </label>
          <button onClick={refreshRange}><LineChart size={16} /> {pt('Refresh')}</button>
          <button onClick={generateSummary}><Bot size={16} /> {pt('AI Summary')}</button>
          <button onClick={() => generateExport('InternalOnly')}><Download size={16} /> {pt('Export')}</button>
          <button onClick={() => generateExport('PublishedToPortal')}><Share2 size={16} /> {pt('Publish')}</button>
        </section>

        {route.includes('/project-dashboard') ? <ProjectDashboard state={state} /> :
          route.includes('/customer-health') ? <CustomerHealth state={state} /> :
            route.includes('/reporting-exports') ? <Exports state={state} /> :
              <ExecutiveDashboard state={state} />}
      </section>
    </main>
  );
}

const nav = [
  { route: '/executive-dashboard', label: 'Executive', icon: <LayoutDashboard size={16} /> },
  { route: '/project-dashboard', label: 'Project', icon: <BarChart3 size={16} /> },
  { route: '/customer-health-dashboard', label: 'Customer Health', icon: <HeartPulse size={16} /> },
  { route: '/reporting-exports', label: 'Exports', icon: <FileSpreadsheet size={16} /> }
];

function ExecutiveDashboard({ state }: { state: Phase13State }) {
  const data = state.executive;
  return (
    <>
      <section className="metric-grid phase7-metrics">
        <Metric label={pt('Health score')} value={data.healthScore} icon={<Gauge size={18} />} />
        <Metric label={pt('Open issues')} value={data.openIssues} icon={<FileText size={18} />} />
        <Metric label={pt('High risk')} value={data.highRiskIssues} icon={<BarChart3 size={18} />} />
        <Metric label={pt('SLA breached')} value={data.slaBreached} icon={<HeartPulse size={18} />} />
        <Metric label={pt('AI runs')} value={data.aiRuns} icon={<Bot size={18} />} />
        <Metric label={pt('Exports')} value={data.reportExports} icon={<Download size={18} />} />
        <Metric label={pt('Releases')} value={data.releases} icon={<LineChart size={18} />} />
        <Metric label={pt('Projects')} value={data.activeProjects} icon={<LayoutDashboard size={18} />} />
      </section>
      <section className="content-grid two">
        <Panel title={pt('AI Summary')} icon={<Bot size={18} />}>
          <div className="phase7-summary">
            <strong>{state.summary?.aiSummary ?? data.latestAiSummary ?? pt('No AI summary yet.')}</strong>
            <span>AiRun: {state.summary?.aiRunId ?? 'pending'}</span>
          </div>
        </Panel>
        <Panel title={pt('Top Risks')} icon={<BarChart3 size={18} />}>
          <Rows items={data.topRisks ?? []} primary="title" secondary="riskLevel" />
        </Panel>
      </section>
    </>
  );
}

function ProjectDashboard({ state }: { state: Phase13State }) {
  const docs = state.project.documents ?? {};
  const delivery = state.project.delivery ?? {};
  return (
    <>
      <section className="metric-grid phase7-metrics">
        <Metric label={pt('Requirements')} value={docs.requirements} icon={<FileText size={18} />} />
        <Metric label={pt('URS')} value={docs.urs} icon={<FileText size={18} />} />
        <Metric label={pt('Blueprints')} value={docs.blueprints} icon={<FileText size={18} />} />
        <Metric label={pt('Config specs')} value={docs.configSpecs} icon={<FileText size={18} />} />
        <Metric label={pt('Sign-offs')} value={docs.signedOff} icon={<Share2 size={18} />} />
        <Metric label={pt('Apply runs')} value={delivery.applyRuns} icon={<LineChart size={18} />} />
        <Metric label={pt('Release ready')} value={delivery.releaseReady} icon={<Gauge size={18} />} />
        <Metric label={pt('Trace links')} value={state.project.traceability} icon={<BarChart3 size={18} />} />
      </section>
      <section className="content-grid two">
        <Panel title="Latest Readiness" icon={<Gauge size={18} />}>
          <Rows items={state.project.latestReadiness ? [state.project.latestReadiness] : []} primary="reportNo" secondary="status" />
        </Panel>
        <Panel title="Latest Documents" icon={<FileText size={18} />}>
          <Rows items={state.project.latestDocuments ?? []} primary="title" secondary="status" />
        </Panel>
      </section>
    </>
  );
}

function CustomerHealth({ state }: { state: Phase13State }) {
  const data = state.health;
  return (
    <>
      <section className="metric-grid phase7-metrics">
        <Metric label="Health score" value={data.healthScore} icon={<HeartPulse size={18} />} />
        <Metric label="Open tickets" value={data.openTickets} icon={<FileText size={18} />} />
        <Metric label="SLA met" value={data.slaMet} icon={<Gauge size={18} />} />
        <Metric label="SLA breached" value={data.slaBreached} icon={<BarChart3 size={18} />} />
        <Metric label="Quota warnings" value={data.quotaWarnings} icon={<LineChart size={18} />} />
        <Metric label="Unpaid invoices" value={data.unpaidInvoices} icon={<FileSpreadsheet size={18} />} />
        <Metric label="Visible reports" value={data.customerVisibleReports} icon={<Share2 size={18} />} />
        <Metric label="Shared files" value={state.shared.length} icon={<Download size={18} />} />
      </section>
      <section className="content-grid two">
        <Panel title="Latest Service Report" icon={<HeartPulse size={18} />}>
          <Rows items={data.latestServiceReport ? [data.latestServiceReport] : []} primary="reportNo" secondary="summary" />
        </Panel>
        <Panel title="Portal Reports" icon={<Share2 size={18} />}>
          <Rows items={state.shared} primary="fileName" secondary="visibility" />
        </Panel>
      </section>
    </>
  );
}

function Exports({ state }: { state: Phase13State }) {
  return (
    <section className="content-grid two">
      <Panel title="Report Catalog" icon={<FileText size={18} />}>
        <Rows items={state.catalog.map((item) => ({ ...item, status: `${item.defaultFormat} / v${item.activeVersion}` }))} primary="name" secondary="status" />
      </Panel>
      <Panel title="Export Files" icon={<Download size={18} />}>
        <Rows items={state.files} primary="fileName" secondary="visibility" />
      </Panel>
      <Panel title="Generation Jobs" icon={<LineChart size={18} />}>
        <Rows items={state.jobs} primary="reportType" secondary="status" />
      </Panel>
      <Panel title="Portal Shared" icon={<Share2 size={18} />}>
        <Rows items={state.shared} primary="fileName" secondary="storageRef" />
      </Panel>
    </section>
  );
}

function Metric({ label, value, icon }: { label: string; value: any; icon: React.ReactNode }) {
  return <div className="metric">{icon}<span>{label}</span><strong>{value ?? 0}</strong></div>;
}

function Panel({ title, icon, children }: { title: string; icon: React.ReactNode; children: React.ReactNode }) {
  return <section className="panel"><header>{icon}<h3>{title}</h3></header>{children}</section>;
}

function Rows({ items, primary, secondary }: { items: any[]; primary: string; secondary: string }) {
  if (!items?.length) return <p className="empty">No reporting data.</p>;
  return (
    <div className="action-list">
      {items.map((item, index) => (
        <div className="list-row" key={item.id ?? item.templateId ?? index}>
          <div>
            <strong>{item[primary] ?? item.title ?? item.name}</strong>
            <span>{item[secondary] ?? item.status}</span>
            <small>{item.storageRef ?? item.createdAt ?? item.summary ?? ''}</small>
          </div>
          <StatusBadge text={String(item.status ?? item.visibility ?? item.maxClassification ?? 'Active')} />
        </div>
      ))}
    </div>
  );
}

function StatusBadge({ text }: { text: string }) {
  return <span className={`phase7-badge ${text.toLowerCase().replaceAll(' ', '').replaceAll('/', '-')}`}>{text}</span>;
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
  return nav.find((item) => item.route === route)?.label ?? 'Executive';
}

function toDateInput(date: Date) {
  return date.toISOString().slice(0, 10);
}

function asIsoStart(date: string) {
  return new Date(`${date}T00:00:00.000Z`).toISOString();
}

function asIsoEnd(date: string) {
  return new Date(`${date}T23:59:59.999Z`).toISOString();
}

function seedState(): Phase13State {
  const catalog = ['Urs', 'Blueprint', 'ConfigSpec', 'UatTestCase', 'ReleaseReadiness', 'ProductionRelease', 'CustomerService', 'Sla', 'Billing', 'Audit', 'Security', 'Knowledge', 'ExecutiveSummary'].map((type) => ({
    templateId: type,
    templateKey: `phase13.${type.toLowerCase()}`,
    name: `${type} Report`,
    reportType: type,
    defaultFormat: type === 'Billing' || type === 'Sla' ? 'Excel' : type === 'Urs' || type === 'Blueprint' || type === 'ConfigSpec' ? 'Word' : 'Pdf',
    activeVersion: 1,
    maxClassification: type === 'Audit' || type === 'Security' ? 'Restricted' : type === 'ConfigSpec' || type === 'Billing' ? 'Confidential' : 'Internal',
    requiresPermission: ['Audit', 'Security', 'Billing', 'ConfigSpec'].includes(type)
  }));
  return {
    customerId: 'demo',
    projectId: 'project-1',
    customerName: 'Demo Customer',
    projectName: 'HRM Stabilization',
    executive: { healthScore: 92, openIssues: 3, highRiskIssues: 1, slaBreached: 0, aiRuns: 4, reportExports: 1, releases: 1, activeProjects: 1, latestAiSummary: 'Executive health is stable; monitor release readiness and SLA trends.', topRisks: [{ id: 'risk-1', title: 'Payroll cutover validation', riskLevel: 'High', status: 'Open' }] },
    project: { documents: { requirements: 1, urs: 1, blueprints: 1, configSpecs: 1, signedOff: 1 }, delivery: { applyRuns: 1, applied: 1, releaseReady: 1, blocked: 0 }, traceability: 4, latestDocuments: [{ id: 'doc-1', title: 'Leave balance visibility', status: 'Approved' }] },
    health: { healthScore: 96, openTickets: 1, slaMet: 1, slaBreached: 0, quotaWarnings: 0, unpaidInvoices: 1, customerVisibleReports: 1, latestServiceReport: { id: 'csr-1', reportNo: 'CSR-00001', summary: 'Service stable.' } },
    catalog,
    jobs: [{ id: 'job-1', reportType: 'CustomerService', outputFormat: 'Pdf', status: 'Completed', visibility: 'PublishedToPortal', requestedBy: 'system', maskingApplied: false, createdAt: new Date().toISOString() }],
    files: [{ id: 'file-1', reportType: 'CustomerService', outputFormat: 'Pdf', fileName: 'customer-service-report-demo.pdf', storageRef: 'reports://demo/customer-service-report-demo.pdf', sizeBytes: 18432, visibility: 'PublishedToPortal', maskingApplied: false, containsSensitiveData: false, createdAt: new Date().toISOString() }],
    shared: [{ id: 'file-1', reportType: 'CustomerService', outputFormat: 'Pdf', fileName: 'customer-service-report-demo.pdf', storageRef: 'reports://demo/customer-service-report-demo.pdf', sizeBytes: 18432, visibility: 'PublishedToPortal', maskingApplied: false, containsSensitiveData: false, createdAt: new Date().toISOString() }]
  };
}
