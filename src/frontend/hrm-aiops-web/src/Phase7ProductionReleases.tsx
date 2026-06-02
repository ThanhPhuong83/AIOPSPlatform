import {
  AlertTriangle,
  CalendarClock,
  CheckCircle2,
  ClipboardCheck,
  FileCheck2,
  Megaphone,
  Play,
  RotateCcw,
  Send,
  ShieldCheck,
  SquareCheckBig,
  XCircle
} from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { api } from './api';

type RiskLevel = 'Low' | 'Medium' | 'High' | 'Critical';
type ReleaseStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Scheduled'
  | 'Deploying'
  | 'ValidationFailed'
  | 'RollbackRequested'
  | 'ReadyToClose'
  | 'Closed'
  | 'Rejected';
type WindowStatus = 'Draft' | 'Scheduled' | 'Active' | 'Expired' | 'Cancelled';
type DeploymentRunStatus = 'NotStarted' | 'Running' | 'Succeeded' | 'Failed' | 'Blocked';
type DeploymentStepStatus = 'Pending' | 'WaitingConfirmation' | 'Running' | 'Succeeded' | 'Failed' | 'Skipped';
type ValidationStatus = 'Pending' | 'Running' | 'Passed' | 'Warning' | 'Failed' | 'Completed';
type RollbackStatus = 'NotRequested' | 'Requested' | 'Approved' | 'Rejected' | 'Executed';
type CommunicationAudience = 'Internal' | 'Customer' | 'Support' | 'Training';
type TabKey =
  | 'overview'
  | 'checklist'
  | 'approval'
  | 'window'
  | 'plan'
  | 'snapshots'
  | 'runs'
  | 'validation'
  | 'rollback'
  | 'communications'
  | 'tasks'
  | 'closure'
  | 'audit';

type ProductionReleasePackage = {
  id: string;
  customerId: string;
  customerName: string;
  projectId: string;
  projectName: string;
  productionEnvironmentId: string;
  releaseNo: string;
  version: string;
  title: string;
  status: ReleaseStatus;
  riskLevel: RiskLevel;
  readinessReportId: string;
  summary: string;
  createdAt: string;
};

type ChecklistItem = {
  id: string;
  title: string;
  required: boolean;
  completed: boolean;
  evidenceRef: string;
};

type ApprovalStep = {
  id: string;
  order: number;
  approver: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  comment: string;
};

type ReleaseWindow = {
  start: string;
  end: string;
  timezone: string;
  status: WindowStatus;
};

type DeploymentStep = {
  id: string;
  order: number;
  title: string;
  riskLevel: RiskLevel;
  executionMethod: 'Automated' | 'Manual' | 'GuardedScript' | 'ReadOnlyCheck';
  manualConfirmationRequired: boolean;
  confirmed: boolean;
};

type DeploymentPlan = {
  id: string;
  validated: boolean;
  validationErrors: string[];
  steps: DeploymentStep[];
};

type DeploymentStepRun = {
  id: string;
  stepId: string;
  title: string;
  status: DeploymentStepStatus;
  startedAt?: string;
  completedAt?: string;
};

type DeploymentRun = {
  id: string;
  status: DeploymentRunStatus;
  preSnapshotId?: string;
  postSnapshotId?: string;
  diffId?: string;
  error?: string;
  steps: DeploymentStepRun[];
  logs: ProductionDeploymentLog[];
};

type ProductionDeploymentLog = {
  id: string;
  level: 'Info' | 'Warning' | 'Error';
  message: string;
  createdAt: string;
};

type ValidationCheck = {
  id: string;
  title: string;
  status: ValidationStatus;
  evidence: string;
};

type RollbackDecision = {
  id: string;
  status: RollbackStatus;
  reason: string;
  impact: string;
  approvedBy?: string;
  rollbackRun?: string;
};

type ReleaseCommunication = {
  id: string;
  audience: CommunicationAudience;
  subject: string;
  content: string;
  sent: boolean;
};

type PostReleaseTask = {
  id: string;
  title: string;
  target: 'Blueprint' | 'ConfigSpec' | 'Training' | 'KnowledgeBase' | 'Issue' | 'ChangeRequest';
  completed: boolean;
};

type ClosureReport = {
  deploymentSummary: string;
  validationSummary: string;
  rollbackSummary: string;
  documentUpdateSummary: string;
  finalRecommendation: string;
};

type ReleaseDetail = {
  checklist: ChecklistItem[];
  approvals: ApprovalStep[];
  window: ReleaseWindow;
  deploymentPlan?: DeploymentPlan;
  preSnapshotId?: string;
  postSnapshotId?: string;
  snapshotDiffId?: string;
  deploymentRuns: DeploymentRun[];
  validationChecks: ValidationCheck[];
  rollback: RollbackDecision;
  communications: ReleaseCommunication[];
  postReleaseTasks: PostReleaseTask[];
  closureReport?: ClosureReport;
  audit: string[];
};

type Store = {
  packages: ProductionReleasePackage[];
  details: Record<string, ReleaseDetail>;
};

type ApiLoadState = 'loading' | 'api' | 'mock';

const nowIso = () => new Date().toISOString();

const tabs: { key: TabKey; label: string }[] = [
  { key: 'overview', label: 'Overview' },
  { key: 'checklist', label: 'Checklist' },
  { key: 'approval', label: 'Approval' },
  { key: 'window', label: 'Release Window' },
  { key: 'plan', label: 'Deployment Plan' },
  { key: 'snapshots', label: 'Pre/Post Snapshot' },
  { key: 'runs', label: 'Deployment Runs' },
  { key: 'validation', label: 'Post-Release Validation' },
  { key: 'rollback', label: 'Rollback' },
  { key: 'communications', label: 'Communications' },
  { key: 'tasks', label: 'Post-Release Tasks' },
  { key: 'closure', label: 'Closure Report' },
  { key: 'audit', label: 'Audit' }
];

export function Phase7ProductionReleases() {
  const [route, setRoute] = useState(() => window.location.pathname);
  const [store, setStore] = useState<Store>(() => seedStore());
  const [loadState, setLoadState] = useState<ApiLoadState>('loading');
  const selectedId = route.match(/\/production-release-packages\/([^/]+)/)?.[1];

  useEffect(() => {
    let cancelled = false;

    async function loadProductionReleases() {
      try {
        const customers = await api.get<any[]>('/api/customers');
        const customer = customers[0];
        if (!customer) {
          setLoadState('mock');
          return;
        }

        const projects = await api.get<any[]>(`/api/customers/${customer.id}/projects`);
        const project = projects[0];
        if (!project) {
          setLoadState('mock');
          return;
        }

        const packages = await api.get<any[]>(`/api/customers/${customer.id}/projects/${project.id}/production-release-packages`);
        const detailPairs = await Promise.all(
          packages.map(async (item) => {
            const detail = await api.get<any>(`/api/customers/${customer.id}/projects/${project.id}/production-release-packages/${item.id}`);
            return [item.id, adaptApiDetail(detail)] as const;
          })
        );

        if (cancelled) return;
        setStore({
          packages: packages.map((item) => adaptApiPackage(item, customer, project)),
          details: Object.fromEntries(detailPairs)
        });
        setLoadState('api');
      } catch {
        if (!cancelled) setLoadState('mock');
      }
    }

    loadProductionReleases();
    return () => {
      cancelled = true;
    };
  }, []);

  function navigate(nextRoute: string) {
    window.history.pushState(null, '', nextRoute);
    setRoute(nextRoute);
  }

  function updatePackage(id: string, patch: Partial<ProductionReleasePackage>, audit: string) {
    setStore((current) => ({
      packages: current.packages.map((item) => (item.id === id ? { ...item, ...patch } : item)),
      details: {
        ...current.details,
        [id]: { ...current.details[id], audit: [`${new Date().toLocaleString()} - ${audit}`, ...current.details[id].audit] }
      }
    }));
  }

  function updateDetail(id: string, recipe: (detail: ReleaseDetail, pkg: ProductionReleasePackage) => ReleaseDetail, audit?: string) {
    setStore((current) => {
      const pkg = current.packages.find((item) => item.id === id);
      if (!pkg) return current;
      const nextDetail = recipe(current.details[id], pkg);
      return {
        ...current,
        details: {
          ...current.details,
          [id]: audit ? { ...nextDetail, audit: [`${new Date().toLocaleString()} - ${audit}`, ...nextDetail.audit] } : nextDetail
        }
      };
    });
  }

  if (selectedId) {
    const pkg = store.packages.find((item) => item.id === selectedId);
    const detail = store.details[selectedId];
    if (!pkg || !detail) {
      return <Phase7Shell title="Production Release Not Found" navigate={navigate} loadState={loadState} />;
    }

    return (
      <Phase7Shell title={pkg.releaseNo} navigate={navigate} loadState={loadState}>
        <ProductionReleaseDetail
          pkg={pkg}
          detail={detail}
          updatePackage={updatePackage}
          updateDetail={updateDetail}
        />
      </Phase7Shell>
    );
  }

  if (route === '/production-release-packages') {
    return (
      <Phase7Shell title="Production Release Packages" navigate={navigate} loadState={loadState}>
        <ProductionReleaseList store={store} navigate={navigate} setStore={setStore} />
      </Phase7Shell>
    );
  }

  return (
    <Phase7Shell title="Production Release Dashboard" navigate={navigate} loadState={loadState}>
      <ProductionReleaseDashboard packages={store.packages} navigate={navigate} />
    </Phase7Shell>
  );
}

function Phase7Shell({ title, navigate, loadState, children }: { title: string; navigate: (route: string) => void; loadState: ApiLoadState; children?: React.ReactNode }) {
  return (
    <main className="phase7-shell">
      <aside className="sidebar phase7-nav">
        <div className="brand">
          <div className="brand-mark">P7</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>Production Release Control</span>
          </div>
        </div>
        <button onClick={() => navigate('/production-releases')}>
          <ShieldCheck size={16} /> Dashboard
        </button>
        <button onClick={() => navigate('/production-release-packages')}>
          <FileCheck2 size={16} /> Release Packages
        </button>
      </aside>
      <section className="workspace">
        <header className="topbar">
          <div>
            <p>Phase 7</p>
            <h2>{title}</h2>
          </div>
          <span className={`status ${loadState === 'api' ? 'ok' : 'idle'}`}>
            {loadState === 'loading' ? 'Loading API' : loadState === 'api' ? 'API connected' : 'Local mock mode'}
          </span>
        </header>
        {children}
      </section>
    </main>
  );
}

function Panel({ title, icon, children }: { title: string; icon: React.ReactNode; children: React.ReactNode }) {
  return (
    <section className="panel">
      <header>
        {icon}
        <h3>{title}</h3>
      </header>
      {children}
    </section>
  );
}

function ProductionReleaseDashboard({ packages, navigate }: { packages: ProductionReleasePackage[]; navigate: (route: string) => void }) {
  const metrics = [
    ['Releases draft', packages.filter((x) => x.status === 'Draft').length],
    ['Pending approval', packages.filter((x) => x.status === 'PendingApproval').length],
    ['Scheduled', packages.filter((x) => x.status === 'Scheduled').length],
    ['Deploying', packages.filter((x) => x.status === 'Deploying').length],
    ['Validation failed', packages.filter((x) => x.status === 'ValidationFailed').length],
    ['Rollback requested', packages.filter((x) => x.status === 'RollbackRequested').length],
    ['Ready to close', packages.filter((x) => x.status === 'ReadyToClose').length],
    ['High/Critical', packages.filter((x) => x.riskLevel === 'High' || x.riskLevel === 'Critical').length]
  ];

  return (
    <>
      <section className="metric-grid phase7-metrics">
        {metrics.map(([label, value]) => (
          <button className="metric metric-button" key={label} onClick={() => navigate('/production-release-packages')}>
            <ClipboardCheck size={18} />
            <span>{label}</span>
            <strong>{value}</strong>
          </button>
        ))}
      </section>
      <section className="content-grid two">
        <Panel title="Critical Release Queue" icon={<AlertTriangle size={18} />}>
          <PackageRows packages={packages.filter((item) => item.riskLevel === 'High' || item.riskLevel === 'Critical')} navigate={navigate} />
        </Panel>
        <Panel title="Ready To Close" icon={<CheckCircle2 size={18} />}>
          <PackageRows packages={packages.filter((item) => item.status === 'ReadyToClose')} navigate={navigate} />
        </Panel>
      </section>
    </>
  );
}

function ProductionReleaseList({ store, navigate, setStore }: { store: Store; navigate: (route: string) => void; setStore: React.Dispatch<React.SetStateAction<Store>> }) {
  const [filters, setFilters] = useState({ customer: '', project: '', status: '', risk: '', version: '' });
  const filtered = store.packages.filter((item) =>
    contains(item.customerName, filters.customer) &&
    contains(item.projectName, filters.project) &&
    contains(item.status, filters.status) &&
    contains(item.riskLevel, filters.risk) &&
    contains(item.version, filters.version)
  );

  function createFromReadiness() {
    const id = crypto.randomUUID();
    const pkg: ProductionReleasePackage = {
      id,
      customerId: 'cus-demo',
      customerName: 'Demo Customer',
      projectId: 'prj-hrm',
      projectName: 'HRM AI Ops Pilot',
      productionEnvironmentId: 'env-prod',
      releaseNo: `PRD-${String(store.packages.length + 1).padStart(5, '0')}`,
      version: `2026.06.${store.packages.length + 1}`,
      title: 'Production package from release readiness report',
      status: 'Draft',
      riskLevel: 'High',
      readinessReportId: `RRR-${String(store.packages.length + 1).padStart(5, '0')}`,
      summary: 'Created from a ReleaseReadinessReport. Checklist and plan are pending.',
      createdAt: nowIso()
    };
    setStore((current) => ({
      packages: [pkg, ...current.packages],
      details: { ...current.details, [id]: emptyDetail(pkg.riskLevel, 'Draft package created from release readiness report.') }
    }));
    navigate(`/production-release-packages/${id}`);
  }

  return (
    <section className="content-grid">
      <Panel title="Filters" icon={<FileCheck2 size={18} />}>
        <div className="compact-form phase7-filter-grid">
          <input placeholder="Customer" value={filters.customer} onChange={(event) => setFilters({ ...filters, customer: event.target.value })} />
          <input placeholder="Project" value={filters.project} onChange={(event) => setFilters({ ...filters, project: event.target.value })} />
          <select value={filters.status} onChange={(event) => setFilters({ ...filters, status: event.target.value })}>
            <option value="">All statuses</option>
            {statusOptions.map((status) => (
              <option key={status}>{status}</option>
            ))}
          </select>
          <select value={filters.risk} onChange={(event) => setFilters({ ...filters, risk: event.target.value })}>
            <option value="">All risk</option>
            <option>Low</option>
            <option>Medium</option>
            <option>High</option>
            <option>Critical</option>
          </select>
          <input placeholder="Version" value={filters.version} onChange={(event) => setFilters({ ...filters, version: event.target.value })} />
          <button className="primary" onClick={createFromReadiness}>
            <FileCheck2 size={15} /> Create from Release Readiness Report
          </button>
        </div>
      </Panel>
      <Panel title="Production Release List" icon={<ShieldCheck size={18} />}>
        <PackageRows packages={filtered} navigate={navigate} />
      </Panel>
    </section>
  );
}

function ProductionReleaseDetail({
  pkg,
  detail,
  updatePackage,
  updateDetail
}: {
  pkg: ProductionReleasePackage;
  detail: ReleaseDetail;
  updatePackage: (id: string, patch: Partial<ProductionReleasePackage>, audit: string) => void;
  updateDetail: (id: string, recipe: (detail: ReleaseDetail, pkg: ProductionReleasePackage) => ReleaseDetail, audit?: string) => void;
}) {
  const [activeTab, setActiveTab] = useState<TabKey>('overview');
  const gates = getDeployGates(pkg, detail);

  const actions = {
    generateChecklist: () =>
      updateDetail(pkg.id, (current) => ({ ...current, checklist: defaultChecklist(pkg.riskLevel) }), 'Release checklist generated.'),
    submitApproval: () => {
      const approvals = approvalStepsFor(pkg.riskLevel, pkg.title);
      updateDetail(pkg.id, (current) => ({ ...current, approvals }), 'Release submitted for approval.');
      updatePackage(pkg.id, { status: 'PendingApproval' }, 'Release status moved to PendingApproval.');
    },
    approve: () => {
      updateDetail(pkg.id, (current) => ({ ...current, approvals: current.approvals.map((item) => ({ ...item, status: 'Approved' })) }), 'Release approvals completed.');
      updatePackage(pkg.id, { status: 'Approved' }, 'Release approved.');
    },
    reject: () => {
      updateDetail(pkg.id, (current) => ({ ...current, approvals: current.approvals.map((item, index) => (index === 0 ? { ...item, status: 'Rejected' } : item)) }), 'Release rejected.');
      updatePackage(pkg.id, { status: 'Rejected' }, 'Release rejected.');
    },
    generatePlan: () =>
      updateDetail(pkg.id, (current) => ({ ...current, deploymentPlan: defaultDeploymentPlan(pkg.riskLevel) }), 'Deployment plan generated.'),
    validatePlan: () =>
      updateDetail(
        pkg.id,
        (current) => {
          if (!current.deploymentPlan) return current;
          const errors = current.deploymentPlan.steps.some((step) => step.manualConfirmationRequired && !step.confirmed)
            ? ['Manual confirmation is required before deploy.']
            : [];
          return { ...current, deploymentPlan: { ...current.deploymentPlan, validated: errors.length === 0, validationErrors: errors } };
        },
        'Deployment plan validated.'
      ),
    createPreSnapshot: () =>
      updateDetail(pkg.id, (current) => ({ ...current, preSnapshotId: `PRE-${Date.now()}` }), 'Production pre-snapshot created.'),
    deploy: () => {
      if (gates.blocked) return;
      const run = createDeploymentRun(detail.deploymentPlan);
      updateDetail(pkg.id, (current) => ({ ...current, deploymentRuns: [run, ...current.deploymentRuns] }), 'Production deployment run created.');
      updatePackage(pkg.id, { status: 'Deploying' }, 'Production deployment started.');
    },
    createPostSnapshot: () =>
      updateDetail(pkg.id, (current) => ({ ...current, postSnapshotId: `POST-${Date.now()}` }), 'Production post-snapshot created.'),
    compareSnapshot: () =>
      updateDetail(pkg.id, (current) => ({ ...current, snapshotDiffId: `DIFF-${Date.now()}` }), 'Production snapshot diff created.'),
    generateValidation: () =>
      updateDetail(pkg.id, (current) => ({ ...current, validationChecks: defaultValidationChecks() }), 'Post-release validation plan generated.'),
    startValidation: () => {
      updateDetail(
        pkg.id,
        (current) => ({ ...current, validationChecks: current.validationChecks.map((item) => ({ ...item, status: item.status === 'Pending' ? 'Passed' : item.status })) }),
        'Post-release validation completed.'
      );
      updatePackage(pkg.id, { status: 'ReadyToClose' }, 'Release ready to close after validation.');
    },
    requestRollback: () => {
      updateDetail(pkg.id, (current) => ({ ...current, rollback: { ...current.rollback, status: 'Requested', reason: 'Validation failed or release owner requested rollback review.' } }), 'Rollback decision requested.');
      updatePackage(pkg.id, { status: 'RollbackRequested' }, 'Rollback requested.');
    },
    generateCommunications: () =>
      updateDetail(pkg.id, (current) => ({ ...current, communications: defaultCommunications(pkg) }), 'Release communications generated.'),
    generateTasks: () =>
      updateDetail(pkg.id, (current) => ({ ...current, postReleaseTasks: defaultPostReleaseTasks() }), 'Post-release tasks generated.'),
    generateClosure: () =>
      updateDetail(pkg.id, (current) => ({ ...current, closureReport: defaultClosureReport(current) }), 'Release closure report generated.'),
    close: () => {
      if (!canClose(detail)) return;
      updatePackage(pkg.id, { status: 'Closed' }, 'Release closed.');
    }
  };

  return (
    <section className="content-grid">
      <Panel title="Release Actions" icon={<Play size={18} />}>
        <div className="phase7-command-bar">
          <button onClick={actions.generateChecklist}>Generate Checklist</button>
          <button onClick={actions.submitApproval}>Submit for Approval</button>
          <button onClick={actions.approve}>Approve</button>
          <button onClick={actions.reject}>Reject</button>
          <button onClick={actions.generatePlan}>Generate Deployment Plan</button>
          <button onClick={actions.validatePlan}>Validate Deployment Plan</button>
          <button onClick={actions.createPreSnapshot}>Create Pre-Snapshot</button>
          <button disabled={gates.blocked} onClick={actions.deploy} title={gates.reasons.join('\n')}>
            Deploy Production
          </button>
          <button onClick={actions.createPostSnapshot}>Create Post-Snapshot</button>
          <button onClick={actions.compareSnapshot}>Compare Snapshot</button>
          <button onClick={actions.generateValidation}>Generate Validation Plan</button>
          <button onClick={actions.startValidation}>Start Validation</button>
          <button onClick={actions.requestRollback}>Request Rollback</button>
          <button onClick={actions.generateCommunications}>Generate Communications</button>
          <button onClick={actions.generateTasks}>Generate Post-Release Tasks</button>
          <button onClick={actions.generateClosure}>Generate Closure Report</button>
          <button disabled={!canClose(detail)} onClick={actions.close}>Close Release</button>
        </div>
      </Panel>

      <div className="phase7-tabs">
        {tabs.map((tab) => (
          <button key={tab.key} className={activeTab === tab.key ? 'active' : ''} onClick={() => setActiveTab(tab.key)}>
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === 'overview' && <OverviewPanel pkg={pkg} detail={detail} gates={gates} />}
      {activeTab === 'checklist' && <ReleaseChecklistPanel checklist={detail.checklist} onChange={(checklist) => updateDetail(pkg.id, (current) => ({ ...current, checklist }), 'Checklist item updated.')} />}
      {activeTab === 'approval' && <ApprovalPanel approvals={detail.approvals} onChange={(approvals) => updateDetail(pkg.id, (current) => ({ ...current, approvals }), 'Approval step updated.')} />}
      {activeTab === 'window' && <ReleaseWindowPanel window={detail.window} onChange={(window) => updateDetail(pkg.id, (current) => ({ ...current, window }), 'Release window updated.')} />}
      {activeTab === 'plan' && <ProductionDeploymentPlanDetail plan={detail.deploymentPlan} onChange={(deploymentPlan) => updateDetail(pkg.id, (current) => ({ ...current, deploymentPlan }), 'Deployment plan step updated.')} />}
      {activeTab === 'snapshots' && <SnapshotPanel detail={detail} />}
      {activeTab === 'runs' && <ProductionDeploymentRunDetail runs={detail.deploymentRuns} />}
      {activeTab === 'validation' && <PostReleaseValidationPanel checks={detail.validationChecks} onChange={(validationChecks) => updateDetail(pkg.id, (current) => ({ ...current, validationChecks }), 'Validation check updated.')} />}
      {activeTab === 'rollback' && <RollbackDecisionPanel rollback={detail.rollback} onChange={(rollback) => updateDetail(pkg.id, (current) => ({ ...current, rollback }), 'Rollback decision updated.')} />}
      {activeTab === 'communications' && <ReleaseCommunicationPanel communications={detail.communications} onChange={(communications) => updateDetail(pkg.id, (current) => ({ ...current, communications }), 'Release communication updated.')} />}
      {activeTab === 'tasks' && <PostReleaseTaskPanel tasks={detail.postReleaseTasks} onChange={(postReleaseTasks) => updateDetail(pkg.id, (current) => ({ ...current, postReleaseTasks }), 'Post-release task updated.')} />}
      {activeTab === 'closure' && <ReleaseClosureReportPanel report={detail.closureReport} />}
      {activeTab === 'audit' && <AuditPanel audit={detail.audit} />}
    </section>
  );
}

function OverviewPanel({ pkg, detail, gates }: { pkg: ProductionReleasePackage; detail: ReleaseDetail; gates: { blocked: boolean; reasons: string[] } }) {
  return (
    <section className="content-grid two">
      <Panel title="Overview" icon={<ShieldCheck size={18} />}>
        <div className="phase7-summary">
          <strong>{pkg.title}</strong>
          <span>{pkg.customerName} / {pkg.projectName} / {pkg.version}</span>
          <span>{pkg.summary}</span>
          <div className="badge-row">
            <ProductionReleaseStatusBadge status={pkg.status} />
            <RiskBadge risk={pkg.riskLevel} />
          </div>
        </div>
      </Panel>
      <Panel title="Deploy Gates" icon={<AlertTriangle size={18} />}>
        {gates.blocked ? (
          <ul className="phase7-gate-list">
            {gates.reasons.map((reason) => <li key={reason}>{reason}</li>)}
          </ul>
        ) : (
          <p className="empty">All production deployment gates are satisfied.</p>
        )}
        <span>Checklist: {detail.checklist.filter((x) => x.completed).length}/{detail.checklist.length}</span>
        <span>Approvals: {detail.approvals.filter((x) => x.status === 'Approved').length}/{detail.approvals.length}</span>
      </Panel>
    </section>
  );
}

function ReleaseChecklistPanel({ checklist, onChange }: { checklist: ChecklistItem[]; onChange: (next: ChecklistItem[]) => void }) {
  return (
    <Panel title="Release Checklist" icon={<SquareCheckBig size={18} />}>
      <div className="action-list">
        {checklist.map((item) => (
          <div className="list-row" key={item.id}>
            <div>
              <strong>{item.title} {item.required && <span className="inline-badge">Required</span>}</strong>
              <input
                placeholder="EvidenceRef"
                value={item.evidenceRef}
                onChange={(event) => onChange(checklist.map((row) => row.id === item.id ? { ...row, evidenceRef: event.target.value } : row))}
              />
            </div>
            <button onClick={() => onChange(checklist.map((row) => row.id === item.id ? { ...row, completed: !row.completed } : row))}>
              {item.completed ? <CheckCircle2 size={15} /> : <SquareCheckBig size={15} />} {item.completed ? 'Completed' : 'Mark completed'}
            </button>
          </div>
        ))}
      </div>
    </Panel>
  );
}

function ApprovalPanel({ approvals, onChange }: { approvals: ApprovalStep[]; onChange: (next: ApprovalStep[]) => void }) {
  return (
    <Panel title="Approval" icon={<ShieldCheck size={18} />}>
      <div className="action-list">
        {approvals.map((step) => (
          <div className="list-row" key={step.id}>
            <div>
              <strong>Step {step.order}: {step.approver}</strong>
              <span>{step.status} / {step.comment || 'No comment'}</span>
            </div>
            <div className="row-actions">
              <button onClick={() => onChange(approvals.map((row) => row.id === step.id ? { ...row, status: 'Approved', comment: 'Approved from UI.' } : row))}>Approve</button>
              <button onClick={() => onChange(approvals.map((row) => row.id === step.id ? { ...row, status: 'Rejected', comment: 'Rejected from UI.' } : row))}>Reject</button>
            </div>
          </div>
        ))}
      </div>
    </Panel>
  );
}

function ReleaseWindowPanel({ window, onChange }: { window: ReleaseWindow; onChange: (next: ReleaseWindow) => void }) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    onChange({ start: String(form.get('start')), end: String(form.get('end')), timezone: String(form.get('timezone')), status: 'Scheduled' });
  }

  return (
    <Panel title="Release Window" icon={<CalendarClock size={18} />}>
      <form className="compact-form" onSubmit={submit}>
        <input name="start" type="datetime-local" defaultValue={window.start} required />
        <input name="end" type="datetime-local" defaultValue={window.end} required />
        <input name="timezone" defaultValue={window.timezone} required />
        <button>Schedule Release Window</button>
      </form>
      <div className="phase7-summary">
        <ReleaseWindowStatusBadge status={window.status} />
        <span>{window.start || 'No start'} - {window.end || 'No end'} / {window.timezone}</span>
        <button onClick={() => onChange({ ...window, status: 'Cancelled' })}>Cancel window</button>
      </div>
    </Panel>
  );
}

function ProductionDeploymentPlanDetail({ plan, onChange }: { plan?: DeploymentPlan; onChange: (next: DeploymentPlan) => void }) {
  if (!plan) return <Panel title="Deployment Plan" icon={<ClipboardCheck size={18} />}><p className="empty">No deployment plan generated.</p></Panel>;
  return (
    <Panel title="Production Deployment Plan Detail" icon={<ClipboardCheck size={18} />}>
      <div className="phase7-summary">
        <strong>{plan.validated ? 'Plan validated' : 'Plan pending validation'}</strong>
        {plan.validationErrors.map((error) => <span key={error}>{error}</span>)}
      </div>
      <div className="action-list">
        {plan.steps.map((step) => (
          <div className="list-row" key={step.id}>
            <div>
              <strong>{step.order}. {step.title}</strong>
              <span>{step.executionMethod} / {step.riskLevel} / Manual confirmation: {step.manualConfirmationRequired ? 'Required' : 'No'}</span>
            </div>
            <button disabled={!step.manualConfirmationRequired} onClick={() => onChange({ ...plan, validated: false, steps: plan.steps.map((row) => row.id === step.id ? { ...row, confirmed: true } : row) })}>
              {step.confirmed ? 'Confirmed' : 'Confirm manual step'}
            </button>
          </div>
        ))}
      </div>
    </Panel>
  );
}

function SnapshotPanel({ detail }: { detail: ReleaseDetail }) {
  return (
    <Panel title="Pre/Post Snapshot" icon={<FileCheck2 size={18} />}>
      <ActionRows rows={[
        ['Pre snapshot', detail.preSnapshotId ?? 'Missing'],
        ['Post snapshot', detail.postSnapshotId ?? 'Missing'],
        ['Snapshot diff', detail.snapshotDiffId ?? 'Missing']
      ]} />
    </Panel>
  );
}

function ProductionDeploymentRunDetail({ runs }: { runs: DeploymentRun[] }) {
  return (
    <Panel title="Production Deployment Run Detail" icon={<Play size={18} />}>
      {runs.length === 0 ? <p className="empty">No deployment runs.</p> : runs.map((run) => (
        <div className="phase7-run" key={run.id}>
          <div className="badge-row">
            <DeploymentRunStatusBadge status={run.status} />
            <span>{run.error ?? 'No error'}</span>
          </div>
          <DeploymentTimeline steps={run.steps} />
          <ProductionDeploymentLogPanel logs={run.logs} />
        </div>
      ))}
    </Panel>
  );
}

function DeploymentTimeline({ steps }: { steps: DeploymentStepRun[] }) {
  return (
    <div className="phase7-timeline">
      {steps.map((step) => (
        <div className="trace-node" key={step.id}>
          <span>{step.title}</span>
          <DeploymentStepStatusBadge status={step.status} />
          <small>{step.startedAt ? new Date(step.startedAt).toLocaleTimeString() : 'Pending'}</small>
        </div>
      ))}
    </div>
  );
}

function ProductionDeploymentLogPanel({ logs }: { logs: ProductionDeploymentLog[] }) {
  return (
    <div className="control-list">
      {logs.map((log) => <span key={log.id}>{log.level} / {new Date(log.createdAt).toLocaleString()} / {log.message}</span>)}
    </div>
  );
}

function PostReleaseValidationPanel({ checks, onChange }: { checks: ValidationCheck[]; onChange: (next: ValidationCheck[]) => void }) {
  return (
    <Panel title="Post-Release Validation Detail" icon={<ClipboardCheck size={18} />}>
      {checks.length === 0 ? <p className="empty">No validation checks.</p> : checks.map((check) => (
        <div className="list-row" key={check.id}>
          <div>
            <strong>{check.title}</strong>
            <input value={check.evidence} placeholder="Evidence" onChange={(event) => onChange(checks.map((row) => row.id === check.id ? { ...row, evidence: event.target.value } : row))} />
          </div>
          <select value={check.status} onChange={(event) => onChange(checks.map((row) => row.id === check.id ? { ...row, status: event.target.value as ValidationStatus } : row))}>
            <option>Pending</option>
            <option>Passed</option>
            <option>Warning</option>
            <option>Failed</option>
          </select>
        </div>
      ))}
    </Panel>
  );
}

function RollbackDecisionPanel({ rollback, onChange }: { rollback: RollbackDecision; onChange: (next: RollbackDecision) => void }) {
  return (
    <Panel title="Rollback Decision Detail" icon={<RotateCcw size={18} />}>
      <div className="phase7-summary">
        <RollbackDecisionStatusBadge status={rollback.status} />
        <strong>{rollback.reason || 'No rollback requested'}</strong>
        <span>{rollback.impact || 'Impact assessment pending.'}</span>
      </div>
      <div className="row-actions">
        <button onClick={() => onChange({ ...rollback, status: 'Approved', approvedBy: 'release.manager' })}>Approve rollback</button>
        <button onClick={() => onChange({ ...rollback, status: 'Rejected' })}>Reject rollback</button>
        <button disabled={rollback.status !== 'Approved'} onClick={() => onChange({ ...rollback, status: 'Executed', rollbackRun: `RBK-${Date.now()}` })}>Execute approved rollback</button>
      </div>
    </Panel>
  );
}

function ReleaseCommunicationPanel({ communications, onChange }: { communications: ReleaseCommunication[]; onChange: (next: ReleaseCommunication[]) => void }) {
  return (
    <Panel title="Release Communication Page" icon={<Megaphone size={18} />}>
      {communications.length === 0 ? <p className="empty">No communications generated.</p> : communications.map((item) => (
        <div className="list-row" key={item.id}>
          <div>
            <strong>{item.audience}: {item.subject}</strong>
            <span>{item.content}</span>
          </div>
          <button onClick={() => onChange(communications.map((row) => row.id === item.id ? { ...row, sent: true } : row))}>
            <Send size={15} /> {item.sent ? 'Sent' : 'Mark sent'}
          </button>
        </div>
      ))}
    </Panel>
  );
}

function PostReleaseTaskPanel({ tasks, onChange }: { tasks: PostReleaseTask[]; onChange: (next: PostReleaseTask[]) => void }) {
  return (
    <Panel title="Post-Release Tasks Page" icon={<SquareCheckBig size={18} />}>
      {tasks.length === 0 ? <p className="empty">No post-release tasks generated.</p> : tasks.map((task) => (
        <div className="list-row" key={task.id}>
          <div>
            <strong>{task.title}</strong>
            <span>{task.target}</span>
          </div>
          <button onClick={() => onChange(tasks.map((row) => row.id === task.id ? { ...row, completed: true } : row))}>
            {task.completed ? 'Completed' : 'Mark completed'}
          </button>
        </div>
      ))}
    </Panel>
  );
}

function ReleaseClosureReportPanel({ report }: { report?: ClosureReport }) {
  if (!report) return <Panel title="Release Closure Report Page" icon={<FileCheck2 size={18} />}><p className="empty">No closure report generated.</p></Panel>;
  return (
    <Panel title="Release Closure Report Page" icon={<FileCheck2 size={18} />}>
      <ActionRows rows={[
        ['Deployment summary', report.deploymentSummary],
        ['Validation summary', report.validationSummary],
        ['Rollback summary', report.rollbackSummary],
        ['Document update summary', report.documentUpdateSummary],
        ['Final recommendation', report.finalRecommendation]
      ]} />
    </Panel>
  );
}

function AuditPanel({ audit }: { audit: string[] }) {
  return <Panel title="Audit" icon={<FileCheck2 size={18} />}><div className="control-list">{audit.map((row) => <span key={row}>{row}</span>)}</div></Panel>;
}

function PackageRows({ packages, navigate }: { packages: ProductionReleasePackage[]; navigate: (route: string) => void }) {
  if (packages.length === 0) return <p className="empty">No production releases.</p>;
  return (
    <div className="action-list">
      {packages.map((pkg) => (
        <div className="list-row" key={pkg.id}>
          <div>
            <strong>{pkg.releaseNo} / {pkg.version} / {pkg.title}</strong>
            <span>{pkg.customerName} / {pkg.projectName} / {pkg.summary}</span>
            <div className="badge-row">
              <ProductionReleaseStatusBadge status={pkg.status} />
              <RiskBadge risk={pkg.riskLevel} />
            </div>
          </div>
          <button onClick={() => navigate(`/production-release-packages/${pkg.id}`)}>View detail</button>
        </div>
      ))}
    </div>
  );
}

function ActionRows({ rows }: { rows: [string, string][] }) {
  return <div className="control-list">{rows.map(([label, value]) => <span key={label}><strong>{label}:</strong> {value}</span>)}</div>;
}

function ProductionReleaseStatusBadge({ status }: { status: ReleaseStatus }) {
  return <span className={`phase7-badge ${status.toLowerCase()}`}>{status}</span>;
}

function ReleaseWindowStatusBadge({ status }: { status: WindowStatus }) {
  return <span className={`phase7-badge ${status.toLowerCase()}`}>{status}</span>;
}

function DeploymentRunStatusBadge({ status }: { status: DeploymentRunStatus }) {
  return <span className={`phase7-badge ${status.toLowerCase()}`}>{status}</span>;
}

function DeploymentStepStatusBadge({ status }: { status: DeploymentStepStatus }) {
  return <span className={`phase7-badge ${status.toLowerCase()}`}>{status}</span>;
}

function ValidationStatusBadge({ status }: { status: ValidationStatus }) {
  return <span className={`phase7-badge ${status.toLowerCase()}`}>{status}</span>;
}

function RollbackDecisionStatusBadge({ status }: { status: RollbackStatus }) {
  return <span className={`phase7-badge ${status.toLowerCase()}`}>{status}</span>;
}

function RiskBadge({ risk }: { risk: RiskLevel }) {
  return <span className={`phase7-badge risk-${risk.toLowerCase()}`}>{risk}</span>;
}

function getDeployGates(pkg: ProductionReleasePackage, detail: ReleaseDetail) {
  const reasons = [
    !detail.checklist.every((item) => !item.required || item.completed) ? 'Required checklist items must be complete.' : '',
    !detail.approvals.every((item) => item.status === 'Approved') ? 'All approval steps must be approved.' : '',
    detail.window.status !== 'Scheduled' ? 'Release window must be scheduled.' : '',
    !isInsideWindow(detail.window) ? 'Current time is outside the release window.' : '',
    !detail.deploymentPlan?.validated ? 'Deployment plan must be validated.' : '',
    !detail.deploymentPlan?.steps.every((step) => !step.manualConfirmationRequired || step.confirmed) ? 'Manual deployment steps require confirmation.' : '',
    !detail.preSnapshotId ? 'Pre-snapshot is required.' : '',
    pkg.status === 'Rejected' || pkg.status === 'Closed' ? 'Rejected or closed releases cannot deploy.' : ''
  ].filter(Boolean);
  return { blocked: reasons.length > 0, reasons };
}

function canClose(detail: ReleaseDetail) {
  return Boolean(
    detail.closureReport &&
    detail.validationChecks.length > 0 &&
    detail.validationChecks.every((check) => check.status === 'Passed' || check.status === 'Warning') &&
    detail.communications.every((item) => item.sent) &&
    detail.postReleaseTasks.every((item) => item.completed)
  );
}

function isInsideWindow(window: ReleaseWindow) {
  if (!window.start || !window.end || window.status !== 'Scheduled') return false;
  const now = Date.now();
  const start = new Date(window.start).getTime();
  const end = new Date(window.end).getTime();
  return start <= now && now <= end;
}

function contains(value: string, filter: string) {
  return !filter || value.toLowerCase().includes(filter.toLowerCase());
}

const statusOptions: ReleaseStatus[] = ['Draft', 'PendingApproval', 'Approved', 'Scheduled', 'Deploying', 'ValidationFailed', 'RollbackRequested', 'ReadyToClose', 'Closed', 'Rejected'];

function adaptApiPackage(item: any, customer: any, project: any): ProductionReleasePackage {
  return {
    id: item.id,
    customerId: item.customerId,
    customerName: customer.name ?? 'Customer',
    projectId: item.projectId,
    projectName: project.name ?? 'Project',
    productionEnvironmentId: item.productionEnvironmentId,
    releaseNo: item.packageNo,
    version: item.version,
    title: item.title,
    status: item.status as ReleaseStatus,
    riskLevel: item.riskLevel as RiskLevel,
    readinessReportId: item.releaseReadinessReportId,
    summary: item.summary,
    createdAt: item.createdAt
  };
}

function adaptApiDetail(detail: any): ReleaseDetail {
  const rollback = detail.rollbackDecisions?.[0];
  const deploymentStepRuns = detail.deploymentStepRuns ?? [];
  const deploymentLogs = detail.deploymentLogs ?? [];
  return {
    checklist: (detail.checklist ?? []).map((item: any) => ({
      id: item.id,
      title: item.title,
      required: item.required,
      completed: item.completed,
      evidenceRef: item.evidenceRef ?? ''
    })),
    approvals: (detail.approvalSteps ?? []).map((item: any) => ({
      id: item.id,
      order: item.stepOrder,
      approver: item.approverUserId,
      status: item.status as ApprovalStep['status'],
      comment: item.comment ?? ''
    })),
    window: detail.releaseWindow
      ? {
          start: isoToLocalInputValue(detail.releaseWindow.startsAt),
          end: isoToLocalInputValue(detail.releaseWindow.endsAt),
          timezone: detail.releaseWindow.timezone,
          status: detail.releaseWindow.status as WindowStatus
        }
      : defaultWindow(),
    deploymentPlan: detail.deploymentPlan
      ? {
          id: detail.deploymentPlan.id,
          validated: detail.deploymentPlan.validated,
          validationErrors: detail.deploymentPlan.validationErrors ? detail.deploymentPlan.validationErrors.split(';').filter(Boolean) : [],
          steps: (detail.deploymentSteps ?? []).map((step: any) => ({
            id: step.id,
            order: step.stepOrder,
            title: step.title,
            riskLevel: step.riskLevel as RiskLevel,
            executionMethod: step.executionMethod,
            manualConfirmationRequired: step.manualConfirmationRequired,
            confirmed: step.confirmed
          }))
        }
      : undefined,
    preSnapshotId: detail.preSnapshot?.snapshotNo ?? detail.package?.preSnapshotId,
    postSnapshotId: detail.postSnapshot?.snapshotNo ?? detail.package?.postSnapshotId,
    snapshotDiffId: detail.snapshotDiff?.id ?? detail.package?.snapshotDiffId,
    deploymentRuns: (detail.deploymentRuns ?? []).map((run: any) => ({
      id: run.id,
      status: run.status as DeploymentRunStatus,
      preSnapshotId: run.preSnapshotId,
      postSnapshotId: run.postSnapshotId,
      diffId: run.snapshotDiffId,
      error: run.errorMessage,
      steps: deploymentStepRuns
        .filter((step: any) => step.deploymentRunId === run.id)
        .map((step: any) => ({
          id: step.id,
          stepId: step.deploymentStepId,
          title: step.title,
          status: step.status as DeploymentStepStatus,
          startedAt: step.startedAt,
          completedAt: step.completedAt
        })),
      logs: deploymentLogs
        .filter((log: any) => log.deploymentRunId === run.id)
        .map((log: any) => ({
          id: log.id,
          level: log.level,
          message: log.message,
          createdAt: log.createdAt
        }))
    })),
    validationChecks: (detail.validationChecks ?? []).map((check: any) => ({
      id: check.id,
      title: check.title,
      status: check.status as ValidationStatus,
      evidence: check.evidence ?? ''
    })),
    rollback: rollback
      ? {
          id: rollback.id,
          status: rollback.status as RollbackStatus,
          reason: rollback.reason,
          impact: rollback.impact,
          approvedBy: rollback.approvedBy,
          rollbackRun: rollback.rollbackRunRef
        }
      : { id: crypto.randomUUID(), status: 'NotRequested', reason: '', impact: '' },
    communications: (detail.communications ?? []).map((item: any) => ({
      id: item.id,
      audience: item.audience as CommunicationAudience,
      subject: item.subject,
      content: item.content,
      sent: item.sent
    })),
    postReleaseTasks: (detail.postReleaseTasks ?? []).map((task: any) => ({
      id: task.id,
      title: task.title,
      target: task.target,
      completed: task.completed
    })),
    closureReport: detail.closureReport
      ? {
          deploymentSummary: detail.closureReport.deploymentSummary,
          validationSummary: detail.closureReport.validationSummary,
          rollbackSummary: detail.closureReport.rollbackSummary,
          documentUpdateSummary: detail.closureReport.documentUpdateSummary,
          finalRecommendation: detail.closureReport.finalRecommendation
        }
      : undefined,
    audit: (detail.audit ?? []).map((item: any) => `${new Date(item.createdAt).toLocaleString()} - ${item.action}`)
  };
}

function isoToLocalInputValue(value?: string) {
  return value ? toLocalInputValue(new Date(value)) : '';
}

function seedStore(): Store {
  const release: ProductionReleasePackage = {
    id: 'pkg-001',
    customerId: 'cus-demo',
    customerName: 'Demo Customer',
    projectId: 'prj-hrm',
    projectName: 'HRM AI Ops Pilot',
    productionEnvironmentId: 'env-prod',
    releaseNo: 'PRD-00001',
    version: '2026.06.1',
    title: 'Leave balance recalculation production release',
    status: 'Draft',
    riskLevel: 'High',
    readinessReportId: 'RRR-00001',
    summary: 'Release package created from successful Test/UAT readiness report.',
    createdAt: nowIso()
  };
  const pending: ProductionReleasePackage = { ...release, id: 'pkg-002', releaseNo: 'PRD-00002', version: '2026.06.2', title: 'Payroll approval matrix hotfix', status: 'PendingApproval', riskLevel: 'Critical' };
  const ready: ProductionReleasePackage = { ...release, id: 'pkg-003', releaseNo: 'PRD-00003', version: '2026.06.3', title: 'Permission audit export release', status: 'ReadyToClose', riskLevel: 'High' };
  return {
    packages: [release, pending, ready],
    details: {
      [release.id]: emptyDetail(release.riskLevel, 'Initial release package seeded.'),
      [pending.id]: { ...emptyDetail(pending.riskLevel, 'Pending approval release seeded.'), approvals: approvalStepsFor(pending.riskLevel, pending.title) },
      [ready.id]: readyToCloseDetail(ready.riskLevel)
    }
  };
}

function emptyDetail(risk: RiskLevel, audit: string): ReleaseDetail {
  return {
    checklist: defaultChecklist(risk),
    approvals: approvalStepsFor(risk, 'Production Release'),
    window: defaultWindow(),
    deploymentPlan: undefined,
    preSnapshotId: undefined,
    postSnapshotId: undefined,
    snapshotDiffId: undefined,
    deploymentRuns: [],
    validationChecks: [],
    rollback: { id: crypto.randomUUID(), status: 'NotRequested', reason: '', impact: '' },
    communications: [],
    postReleaseTasks: [],
    closureReport: undefined,
    audit: [`${new Date().toLocaleString()} - ${audit}`]
  };
}

function readyToCloseDetail(risk: RiskLevel): ReleaseDetail {
  const detail = emptyDetail(risk, 'Ready-to-close sample seeded.');
  detail.checklist = defaultChecklist(risk).map((item) => ({ ...item, completed: true, evidenceRef: `evidence://${item.id}` }));
  detail.approvals = approvalStepsFor(risk, 'Permission audit export release').map((item) => ({ ...item, status: 'Approved' }));
  detail.window = defaultWindow(true);
  detail.deploymentPlan = { ...defaultDeploymentPlan(risk), validated: true };
  detail.preSnapshotId = 'PRE-READY';
  detail.postSnapshotId = 'POST-READY';
  detail.snapshotDiffId = 'DIFF-READY';
  detail.validationChecks = defaultValidationChecks().map((item) => ({ ...item, status: 'Passed', evidence: 'validation://passed' }));
  detail.communications = defaultCommunications({ title: 'Permission audit export release' } as ProductionReleasePackage).map((item) => ({ ...item, sent: true }));
  detail.postReleaseTasks = defaultPostReleaseTasks().map((item) => ({ ...item, completed: true }));
  detail.closureReport = defaultClosureReport(detail);
  return detail;
}

function defaultChecklist(risk: RiskLevel): ChecklistItem[] {
  const base = [
    ['Readiness report reviewed', true],
    ['Production pre-snapshot owner assigned', true],
    ['Rollback plan validated', true],
    ['Support team briefed', true],
    ['Customer communication approved', risk === 'High' || risk === 'Critical']
  ] as const;
  return base.map(([title, required]) => ({ id: crypto.randomUUID(), title, required, completed: false, evidenceRef: '' }));
}

function approvalStepsFor(risk: RiskLevel, title: string): ApprovalStep[] {
  const steps = [
    { approver: 'Release Manager' },
    ...(risk === 'High' || risk === 'Critical' || /payroll|permission|security|integration/i.test(title) ? [{ approver: 'Business Owner' }] : []),
    ...(risk === 'Critical' ? [{ approver: 'Security Lead' }] : [])
  ];
  return steps.map((step, index) => ({ id: crypto.randomUUID(), order: index + 1, approver: step.approver, status: 'Pending', comment: '' }));
}

function defaultWindow(active = false): ReleaseWindow {
  const start = new Date(Date.now() - (active ? 15 : -15) * 60_000);
  const end = new Date(Date.now() + 2 * 60 * 60_000);
  return { start: toLocalInputValue(start), end: toLocalInputValue(end), timezone: 'Asia/Bangkok', status: active ? 'Scheduled' : 'Draft' };
}

function defaultDeploymentPlan(risk: RiskLevel): DeploymentPlan {
  return {
    id: crypto.randomUUID(),
    validated: false,
    validationErrors: [],
    steps: [
      { id: crypto.randomUUID(), order: 1, title: 'Verify production health and release window', riskLevel: risk, executionMethod: 'ReadOnlyCheck', manualConfirmationRequired: false, confirmed: true },
      { id: crypto.randomUUID(), order: 2, title: 'Confirm deployment owner presence', riskLevel: risk, executionMethod: 'Manual', manualConfirmationRequired: true, confirmed: false },
      { id: crypto.randomUUID(), order: 3, title: 'Execute guarded production deployment package', riskLevel: risk, executionMethod: 'GuardedScript', manualConfirmationRequired: true, confirmed: false },
      { id: crypto.randomUUID(), order: 4, title: 'Capture post-deployment health signal', riskLevel: 'Medium', executionMethod: 'Automated', manualConfirmationRequired: false, confirmed: true }
    ]
  };
}

function createDeploymentRun(plan?: DeploymentPlan): DeploymentRun {
  const steps = (plan?.steps ?? []).map((step) => ({ id: crypto.randomUUID(), stepId: step.id, title: step.title, status: 'Succeeded' as DeploymentStepStatus, startedAt: nowIso(), completedAt: nowIso() }));
  return {
    id: crypto.randomUUID(),
    status: 'Succeeded',
    preSnapshotId: `PRE-${Date.now()}`,
    postSnapshotId: `POST-${Date.now()}`,
    diffId: `DIFF-${Date.now()}`,
    steps,
    logs: [
      { id: crypto.randomUUID(), level: 'Info', message: 'Production deployment run started inside release window.', createdAt: nowIso() },
      { id: crypto.randomUUID(), level: 'Info', message: 'All guarded deployment steps completed.', createdAt: nowIso() }
    ]
  };
}

function defaultValidationChecks(): ValidationCheck[] {
  return ['Application health check', 'Leave approval smoke test', 'Permission boundary check', 'Integration heartbeat'].map((title) => ({ id: crypto.randomUUID(), title, status: 'Pending', evidence: '' }));
}

function defaultCommunications(pkg: ProductionReleasePackage): ReleaseCommunication[] {
  return (['Internal', 'Customer', 'Support', 'Training'] as CommunicationAudience[]).map((audience) => ({ id: crypto.randomUUID(), audience, subject: `${pkg.version} ${pkg.title}`, content: `${audience} communication for ${pkg.releaseNo}.`, sent: false }));
}

function defaultPostReleaseTasks(): PostReleaseTask[] {
  return [
    ['Update Blueprint', 'Blueprint'],
    ['Update Config Spec', 'ConfigSpec'],
    ['Update Training', 'Training'],
    ['Update Knowledge Base', 'KnowledgeBase'],
    ['Close Issue', 'Issue'],
    ['Close Change Request', 'ChangeRequest']
  ].map(([title, target]) => ({ id: crypto.randomUUID(), title, target: target as PostReleaseTask['target'], completed: false }));
}

function defaultClosureReport(detail: ReleaseDetail): ClosureReport {
  return {
    deploymentSummary: `${detail.deploymentRuns.length} deployment run(s), latest status ${detail.deploymentRuns[0]?.status ?? 'N/A'}.`,
    validationSummary: `${detail.validationChecks.filter((item) => item.status === 'Passed').length}/${detail.validationChecks.length} validation checks passed.`,
    rollbackSummary: `Rollback status: ${detail.rollback.status}.`,
    documentUpdateSummary: `${detail.postReleaseTasks.filter((item) => item.completed).length}/${detail.postReleaseTasks.length} post-release tasks completed.`,
    finalRecommendation: 'Close release when communications and document updates are complete.'
  };
}

function toLocalInputValue(date: Date) {
  const offset = date.getTimezoneOffset();
  return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 16);
}
