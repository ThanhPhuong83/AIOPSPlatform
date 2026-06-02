import {
  Bot,
  CheckCircle2,
  ClipboardCheck,
  DatabaseZap,
  FileSpreadsheet,
  FileText,
  GitCompareArrows,
  ListChecks,
  PlayCircle,
  ShieldCheck,
  TableProperties,
  Upload
} from 'lucide-react';
import type React from 'react';
import { useEffect, useMemo, useState } from 'react';

type LoadState = 'loading' | 'api' | 'mock';

type DataMigrationState = {
  customerId: string;
  projectId: string;
  customerName: string;
  projectName: string;
  dashboard: any;
  templates: any[];
  templateVersions: any[];
  files: any[];
  mappings: any[];
  validationRules: any[];
  batches: any[];
  runs: any[];
  validationIssues: any[];
  reconciliationReports: any[];
  signOffs: any[];
  aiAssistance: any[];
  environments: any[];
};

const actor = 'security.admin';

export function Phase17DataMigration() {
  const [route, setRoute] = useState(() => window.location.pathname);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [state, setState] = useState<DataMigrationState>(() => seedState());
  const activeBatch = useMemo(() => state.batches.find((item) => item.status !== 'SignedOff') ?? state.batches[0], [state.batches]);
  const activeTemplate = state.templates[0];
  const activeFile = state.files[0];
  const testEnvironment = state.environments.find((item) => item.kind === 'Test' || item.kind === 'Uat') ?? state.environments[0];

  async function load() {
    try {
      const customers = await api<any[]>('/api/customers');
      const customer = customers[0];
      if (!customer) throw new Error('No customer');
      const projects = await api<any[]>(`/api/customers/${customer.id}/projects`, customer.id);
      const project = projects[0];
      if (!project) throw new Error('No project');
      const base = `/api/customers/${customer.id}/projects/${project.id}/data-migration`;
      const [dashboard, templates, templateVersions, files, mappings, validationRules, batches, runs, validationIssues, reconciliationReports, signOffs, aiAssistance, environments] = await Promise.all([
        api<any>(`${base}/dashboard`, customer.id),
        api<any[]>(`${base}/templates`, customer.id),
        api<any[]>(`${base}/template-versions`, customer.id),
        api<any[]>(`${base}/files`, customer.id),
        api<any[]>(`${base}/mappings`, customer.id),
        api<any[]>(`${base}/validation-rules`, customer.id),
        api<any[]>(`${base}/batches`, customer.id),
        api<any[]>(`${base}/runs`, customer.id),
        api<any[]>(`${base}/validation-issues`, customer.id),
        api<any[]>(`${base}/reconciliation-reports`, customer.id),
        api<any[]>(`${base}/sign-offs`, customer.id),
        api<any[]>(`${base}/ai-assistance`, customer.id),
        api<any[]>(`/api/customers/${customer.id}/projects/${project.id}/environments`, customer.id)
      ]);
      setState({ customerId: customer.id, projectId: project.id, customerName: customer.name, projectName: project.name, dashboard, templates, templateVersions, files, mappings, validationRules, batches, runs, validationIssues, reconciliationReports, signOffs, aiAssistance, environments });
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

  async function registerMockFile() {
    if (loadState !== 'api' || !activeTemplate || !testEnvironment) return;
    const base = dataBase(state);
    await api(`${base}/files`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({
        environmentId: testEnvironment.id,
        templateId: activeTemplate.id,
        fileRef: `file://uploads/local/employee-master-${Date.now()}.csv`,
        fileName: 'employee-master-smoke.csv',
        fileType: 'Csv',
        sizeBytes: 1840,
        rowCount: 1,
        classification: 'Restricted',
        previewJson: '[{"EmployeeCode":"E999","NationalId":"123456789","BankAccount":"999000111","LeaveBalance":"12"}]'
      })
    });
    await load();
  }

  async function createBatch() {
    if (loadState !== 'api' || !activeTemplate || !activeFile || !testEnvironment) return;
    const base = dataBase(state);
    await api(`${base}/batches`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({
        environmentId: testEnvironment.id,
        templateId: activeTemplate.id,
        templateVersion: activeTemplate.currentVersion ?? 1,
        importFileId: activeFile.id,
        domain: 'Employee'
      })
    });
    await load();
  }

  async function dryRun() {
    if (loadState !== 'api' || !activeBatch) return;
    await api(`${dataBase(state)}/batches/${activeBatch.id}/dry-run`, state.customerId, { method: 'POST' });
    await load();
  }

  async function applyTestUat() {
    if (loadState !== 'api' || !activeBatch) return;
    await api(`${dataBase(state)}/batches/${activeBatch.id}/apply-test-uat`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({ confirmed: true })
    });
    await load();
  }

  async function reconcile() {
    if (loadState !== 'api' || !activeBatch) return;
    await api(`${dataBase(state)}/batches/${activeBatch.id}/reconcile`, state.customerId, { method: 'POST' });
    await load();
  }

  async function aiAssist() {
    if (loadState !== 'api' || !activeTemplate) return;
    await api(`${dataBase(state)}/ai-assistance`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({
        batchId: activeBatch?.id,
        templateId: activeTemplate.id,
        assistanceType: 'MappingSuggestion',
        context: 'Suggest secure employee master mapping for NationalId, bank, payroll and leave balance columns.'
      })
    });
    await load();
  }

  async function signOff() {
    if (loadState !== 'api' || !activeBatch) return;
    await api(`${dataBase(state)}/batches/${activeBatch.id}/sign-off`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({ signedBy: 'customer.data.owner', role: 'Data Owner', comment: 'Approved after reconciliation review.' })
    });
    await load();
  }

  return (
    <main className="phase7-shell">
      <aside className="sidebar phase7-nav">
        <div className="brand">
          <div className="brand-mark">P17</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>Data Migration</span>
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
            <p>Phase 17 / {state.customerName} / {state.projectName}</p>
            <h2>{title(route)}</h2>
          </div>
          <span className={`status ${loadState === 'api' ? 'ok' : 'idle'}`}>
            {loadState === 'loading' ? 'Loading API' : loadState === 'api' ? 'API connected' : 'Local mock mode'}
          </span>
        </header>

        <section className="panel phase17-toolbar">
          <button onClick={registerMockFile}><Upload size={16} /> FileRef</button>
          <button onClick={createBatch}><DatabaseZap size={16} /> Batch</button>
          <button onClick={dryRun}><PlayCircle size={16} /> Dry Run</button>
          <button onClick={applyTestUat}><CheckCircle2 size={16} /> Apply UAT</button>
          <button onClick={reconcile}><GitCompareArrows size={16} /> Reconcile</button>
          <button onClick={aiAssist}><Bot size={16} /> AI Assist</button>
          <button onClick={signOff}><ShieldCheck size={16} /> Sign-off</button>
        </section>

        {route.includes('/import-templates') ? <Templates state={state} /> :
          route.includes('/import-files') ? <Files state={state} /> :
            route.includes('/data-mappings') ? <Mappings state={state} /> :
              route.includes('/validation-rules') ? <ValidationRules state={state} /> :
                route.includes('/import-batches') ? <Batches state={state} /> :
                  route.includes('/reconciliation-reports') ? <Reconciliation state={state} /> :
                    route.includes('/data-signoff') ? <SignOffs state={state} /> :
                      <Dashboard state={state} />}
      </section>
    </main>
  );
}

const nav = [
  { route: '/data-migration-dashboard', label: 'Dashboard', icon: <TableProperties size={16} /> },
  { route: '/import-templates', label: 'Templates', icon: <FileText size={16} /> },
  { route: '/import-files', label: 'Files', icon: <FileSpreadsheet size={16} /> },
  { route: '/data-mappings', label: 'Mappings', icon: <GitCompareArrows size={16} /> },
  { route: '/validation-rules', label: 'Validation', icon: <ListChecks size={16} /> },
  { route: '/import-batches', label: 'Batches', icon: <DatabaseZap size={16} /> },
  { route: '/reconciliation-reports', label: 'Reconcile', icon: <ClipboardCheck size={16} /> },
  { route: '/data-signoff', label: 'Sign-off', icon: <ShieldCheck size={16} /> }
];

function Dashboard({ state }: { state: DataMigrationState }) {
  const data = state.dashboard;
  return (
    <>
      <section className="metric-grid phase7-metrics">
        <Metric label="Templates" value={data.templates ?? state.templates.length} icon={<FileText size={18} />} />
        <Metric label="Files" value={data.files ?? state.files.length} icon={<FileSpreadsheet size={18} />} />
        <Metric label="Batches" value={data.batches ?? state.batches.length} icon={<DatabaseZap size={18} />} />
        <Metric label="Issues" value={data.validationIssues ?? state.validationIssues.length} icon={<ListChecks size={18} />} />
        <Metric label="Reconciled" value={data.reconciledBatches ?? state.reconciliationReports.length} icon={<GitCompareArrows size={18} />} />
        <Metric label="Signed-off" value={data.signedOffBatches ?? state.signOffs.length} icon={<ShieldCheck size={18} />} />
      </section>
      <section className="content-grid two">
        <Panel title="Latest Batches" icon={<DatabaseZap size={18} />}><Rows items={data.latestBatches ?? state.batches} primary="batchNo" secondary="status" /></Panel>
        <Panel title="AI Assistance" icon={<Bot size={18} />}><Rows items={state.aiAssistance} primary="summary" secondary="assistanceType" /></Panel>
      </section>
    </>
  );
}

function Templates({ state }: { state: DataMigrationState }) {
  return <section className="content-grid two"><Panel title="Import Templates" icon={<FileText size={18} />}><Rows items={state.templates} primary="name" secondary="status" /></Panel><Panel title="Template Versions" icon={<TableProperties size={18} />}><Rows items={state.templateVersions} primary="templateId" secondary="version" /></Panel></section>;
}

function Files({ state }: { state: DataMigrationState }) {
  return <section className="content-grid two"><Panel title="Uploaded FileRefs" icon={<FileSpreadsheet size={18} />}><Rows items={state.files} primary="fileName" secondary="classification" /></Panel><Panel title="Masked Preview" icon={<ShieldCheck size={18} />}><Rows items={state.files} primary="previewJson" secondary="fileRef" /></Panel></section>;
}

function Mappings({ state }: { state: DataMigrationState }) {
  return <section className="content-grid"><Panel title="Versioned Column Mappings" icon={<GitCompareArrows size={18} />}><Rows items={state.mappings} primary="targetField" secondary="sourceColumn" /></Panel></section>;
}

function ValidationRules({ state }: { state: DataMigrationState }) {
  return <section className="content-grid two"><Panel title="Configurable Rules" icon={<ListChecks size={18} />}><Rows items={state.validationRules} primary="name" secondary="ruleType" /></Panel><Panel title="Validation Issues" icon={<ClipboardCheck size={18} />}><Rows items={state.validationIssues} primary="message" secondary="severity" /></Panel></section>;
}

function Batches({ state }: { state: DataMigrationState }) {
  return <section className="content-grid two"><Panel title="Import Batches" icon={<DatabaseZap size={18} />}><Rows items={state.batches} primary="batchNo" secondary="status" /></Panel><Panel title="Dry Run / Apply Runs" icon={<PlayCircle size={18} />}><Rows items={state.runs} primary="runNo" secondary="status" /></Panel></section>;
}

function Reconciliation({ state }: { state: DataMigrationState }) {
  return <section className="content-grid"><Panel title="Reconciliation Reports" icon={<GitCompareArrows size={18} />}><Rows items={state.reconciliationReports} primary="reportNo" secondary="status" /></Panel></section>;
}

function SignOffs({ state }: { state: DataMigrationState }) {
  return <section className="content-grid two"><Panel title="Data Sign-offs" icon={<ShieldCheck size={18} />}><Rows items={state.signOffs} primary="signedBy" secondary="status" /></Panel><Panel title="Migration Reports" icon={<ClipboardCheck size={18} />}><Rows items={state.dashboard?.latestReports ?? []} primary="reportNo" secondary="status" /></Panel></section>;
}

function Metric({ label, value, icon }: { label: string; value: any; icon: React.ReactNode }) {
  return <div className="metric">{icon}<span>{label}</span><strong>{Math.round(Number(value ?? 0))}</strong></div>;
}

function Panel({ title, icon, children }: { title: string; icon: React.ReactNode; children: React.ReactNode }) {
  return <section className="panel"><header>{icon}<h3>{title}</h3></header>{children}</section>;
}

function Rows({ items, primary, secondary }: { items: any[]; primary: string; secondary: string }) {
  if (!items?.length) return <p className="empty">No data migration records.</p>;
  return (
    <div className="action-list">
      {items.map((item, index) => (
        <div className="list-row" key={item.id ?? index}>
          <div>
            <strong>{String(item[primary] ?? item.name ?? item.title ?? item.batchNo ?? 'Record')}</strong>
            <span>{String(item[secondary] ?? item.status ?? '')}</span>
            <small>{item.fileRef ?? item.dataClassification ?? item.domain ?? item.createdAt ?? ''}</small>
          </div>
          <StatusBadge text={String(item.status ?? item.severity ?? item.classification ?? item.ruleType ?? 'Active')} />
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

function dataBase(state: DataMigrationState) {
  return `/api/customers/${state.customerId}/projects/${state.projectId}/data-migration`;
}

function title(route: string) {
  return nav.find((item) => item.route === route)?.label ?? 'Dashboard';
}

function seedState(): DataMigrationState {
  const batch = { id: 'batch-1', batchNo: 'MIG-00001', status: 'DryRunPassed', domain: 'Employee' };
  return {
    customerId: 'demo',
    projectId: 'project-1',
    customerName: 'Demo Customer',
    projectName: 'HRM AI Ops Pilot',
    dashboard: { templates: 1, files: 1, batches: 1, validationIssues: 1, reconciledBatches: 0, signedOffBatches: 0, latestBatches: [batch] },
    templates: [{ id: 'tpl-1', name: 'Employee Master Import', status: 'Active', currentVersion: 1 }],
    templateVersions: [{ id: 'tv-1', templateId: 'tpl-1', version: 1, status: 'Active' }],
    files: [{ id: 'file-1', fileName: 'employee-master.csv', fileRef: 'file://uploads/mock/employee-master.csv', classification: 'Restricted', previewJson: '[{"EmployeeCode":"E001","NationalId":"[masked]","BankAccount":"[masked]"}]' }],
    mappings: [{ id: 'map-1', targetField: 'NationalId', sourceColumn: 'national_id', dataClassification: 'Restricted' }, { id: 'map-2', targetField: 'BankAccount', sourceColumn: 'bank_account', dataClassification: 'Restricted' }],
    validationRules: [{ id: 'rule-1', name: 'Employee code required', ruleType: 'Required', severity: 'Error' }, { id: 'rule-2', name: 'Duplicate employee detection', ruleType: 'DuplicateDetection', severity: 'Critical' }],
    batches: [batch],
    runs: [{ id: 'run-1', runNo: 'RUN-00001', runType: 'DryRun', status: 'Passed' }],
    validationIssues: [{ id: 'issue-1', message: 'Duplicate employee codes are blocked before apply.', severity: 'Critical' }],
    reconciliationReports: [],
    signOffs: [],
    aiAssistance: [{ id: 'ai-1', summary: 'AI suggested mapping personal data columns with restricted classification.', assistanceType: 'MappingSuggestion' }],
    environments: [{ id: 'env-1', name: 'UAT', kind: 'Uat' }]
  };
}
