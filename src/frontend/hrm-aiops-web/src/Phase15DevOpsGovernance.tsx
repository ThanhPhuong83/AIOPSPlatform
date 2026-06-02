import {
  Bot,
  Boxes,
  CheckCircle2,
  Code2,
  GitBranch,
  GitMerge,
  GitPullRequest,
  PackageCheck,
  Play,
  ShieldCheck,
  Workflow
} from 'lucide-react';
import type React from 'react';
import { useEffect, useMemo, useState } from 'react';

type LoadState = 'loading' | 'api' | 'mock';

type DevOpsState = {
  customerId: string;
  projectId: string;
  customerName: string;
  projectName: string;
  dashboard: any;
  repositories: any[];
  branches: any[];
  pullRequests: any[];
  pipelines: any[];
  pipelineRuns: any[];
  packages: any[];
  snapshots: any[];
  runs: any[];
  policies: any[];
};

const actor = 'security.admin';

export function Phase15DevOpsGovernance() {
  const [route, setRoute] = useState(() => window.location.pathname);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [state, setState] = useState<DevOpsState>(() => seedState());
  const repo = state.repositories[0];
  const pullRequest = useMemo(() => state.pullRequests.find((item) => item.status !== 'Merged') ?? state.pullRequests[0], [state.pullRequests]);
  const pipeline = state.pipelines[0];

  async function load() {
    try {
      const customers = await api<any[]>('/api/customers');
      const customer = customers[0];
      if (!customer) throw new Error('No customer');
      const projects = await api<any[]>(`/api/customers/${customer.id}/projects`, customer.id);
      const project = projects[0];
      if (!project) throw new Error('No project');
      const base = `/api/customers/${customer.id}/projects/${project.id}/devops`;
      const [dashboard, repositories, branches, pullRequests, pipelines, pipelineRuns, packages, snapshots, runs, policies] = await Promise.all([
        api<any>(`${base}/dashboard`, customer.id),
        api<any[]>(`${base}/repositories`, customer.id),
        api<any[]>(`${base}/branches`, customer.id),
        api<any[]>(`${base}/pull-requests`, customer.id),
        api<any[]>(`${base}/pipelines`, customer.id),
        api<any[]>(`${base}/pipeline-runs`, customer.id),
        api<any[]>(`${base}/deployment-packages`, customer.id),
        api<any[]>(`${base}/source-snapshots`, customer.id),
        api<any[]>(`${base}/runs`, customer.id),
        api<any[]>(`${base}/governance-policies`, customer.id)
      ]);
      setState({ customerId: customer.id, projectId: project.id, customerName: customer.name, projectName: project.name, dashboard, repositories, branches, pullRequests, pipelines, pipelineRuns, packages, snapshots, runs, policies });
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

  async function aiAnalyze() {
    if (loadState !== 'api' || !pullRequest) return;
    await api(`/api/customers/${state.customerId}/projects/${state.projectId}/devops/pull-requests/${pullRequest.id}/ai-analyze`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({
        branchName: pullRequest.sourceBranch,
        diffText: 'integration webhook permission check with token=secret123 and security review required'
      })
    });
    await load();
  }

  async function proposePatch() {
    if (loadState !== 'api' || !pullRequest) return;
    await api(`/api/customers/${state.customerId}/projects/${state.projectId}/devops/pull-requests/${pullRequest.id}/ai-patch`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({
        branchName: pullRequest.sourceBranch,
        intent: 'Add guarded HRM validation without direct merge or production deploy.'
      })
    });
    await load();
  }

  async function approveReview() {
    if (loadState !== 'api' || !pullRequest) return;
    await api(`/api/customers/${state.customerId}/projects/${state.projectId}/devops/pull-requests/${pullRequest.id}/reviews`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({ reviewerUserId: actor, decision: 'Approved', comments: 'Reviewed in Phase 15 console.' })
    });
    await load();
  }

  async function runPipeline(runType: 'Build' | 'Test' | 'CodeScan') {
    if (loadState !== 'api' || !repo || !pipeline || !pullRequest) return;
    await api(`/api/customers/${state.customerId}/projects/${state.projectId}/devops/pipelines/${pipeline.id}/run`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({
        repositoryId: repo.id,
        pullRequestId: pullRequest.id,
        runType,
        inputJson: JSON.stringify({ mock: true, runType, branch: pullRequest.sourceBranch })
      })
    });
    await load();
  }

  async function createPackage() {
    if (loadState !== 'api' || !pullRequest) return;
    await api(`/api/customers/${state.customerId}/projects/${state.projectId}/devops/pull-requests/${pullRequest.id}/release-package`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({ version: `2026.06.${new Date().getMinutes()}` })
    });
    await load();
  }

  async function mergePullRequest(requestedByAi = false) {
    if (loadState !== 'api' || !pullRequest) return;
    await api(`/api/customers/${state.customerId}/projects/${state.projectId}/devops/pull-requests/${pullRequest.id}/merge`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({ requestedByAi })
    }).catch(() => undefined);
    await load();
  }

  return (
    <main className="phase7-shell">
      <aside className="sidebar phase7-nav">
        <div className="brand">
          <div className="brand-mark">P15</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>DevOps Governance</span>
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
            <p>Phase 15 / {state.customerName} / {state.projectName}</p>
            <h2>{title(route)}</h2>
          </div>
          <span className={`status ${loadState === 'api' ? 'ok' : 'idle'}`}>
            {loadState === 'loading' ? 'Loading API' : loadState === 'api' ? 'API connected' : 'Local mock mode'}
          </span>
        </header>

        <section className="panel phase15-toolbar">
          <button onClick={aiAnalyze}><Bot size={16} /> AI Analyze</button>
          <button onClick={proposePatch}><Code2 size={16} /> Patch</button>
          <button onClick={approveReview}><CheckCircle2 size={16} /> Review</button>
          <button onClick={() => runPipeline('Build')}><Play size={16} /> Build</button>
          <button onClick={() => runPipeline('Test')}><Workflow size={16} /> Test</button>
          <button onClick={() => runPipeline('CodeScan')}><ShieldCheck size={16} /> Scan</button>
          <button onClick={createPackage}><PackageCheck size={16} /> Package</button>
          <button onClick={() => mergePullRequest(false)}><GitMerge size={16} /> Merge</button>
          <button onClick={() => mergePullRequest(true)}><Bot size={16} /> AI Merge</button>
        </section>

        {route.includes('/source-repositories') ? <Repositories state={state} /> :
          route.includes('/pull-requests') ? <PullRequests state={state} /> :
            route.includes('/ci-cd-pipelines') ? <Pipelines state={state} /> :
              route.includes('/release-packages') ? <Packages state={state} /> :
                route.includes('/ai-code-governance') ? <Governance state={state} /> :
                  <Dashboard state={state} />}
      </section>
    </main>
  );
}

const nav = [
  { route: '/devops-dashboard', label: 'Dashboard', icon: <Boxes size={16} /> },
  { route: '/source-repositories', label: 'Repositories', icon: <GitBranch size={16} /> },
  { route: '/pull-requests', label: 'Pull Requests', icon: <GitPullRequest size={16} /> },
  { route: '/ci-cd-pipelines', label: 'CI/CD', icon: <Workflow size={16} /> },
  { route: '/release-packages', label: 'Packages', icon: <PackageCheck size={16} /> },
  { route: '/ai-code-governance', label: 'AI Governance', icon: <ShieldCheck size={16} /> }
];

function Dashboard({ state }: { state: DevOpsState }) {
  const data = state.dashboard;
  return (
    <>
      <section className="metric-grid phase7-metrics">
        <Metric label="Repositories" value={data.repositories} icon={<GitBranch size={18} />} />
        <Metric label="Open PRs" value={data.openPullRequests} icon={<GitPullRequest size={18} />} />
        <Metric label="Blocked PRs" value={data.blockedPullRequests} icon={<ShieldCheck size={18} />} />
        <Metric label="Pipelines" value={data.pipelines} icon={<Workflow size={18} />} />
        <Metric label="CI Passed" value={data.successfulPipelineRuns} icon={<CheckCircle2 size={18} />} />
        <Metric label="Ready Packages" value={data.readyPackages} icon={<PackageCheck size={18} />} />
      </section>
      <section className="content-grid two">
        <Panel title="Latest Pull Requests" icon={<GitPullRequest size={18} />}><Rows items={data.latestPullRequests ?? state.pullRequests} primary="title" secondary="status" /></Panel>
        <Panel title="Latest DevOps Runs" icon={<Play size={18} />}><Rows items={data.latestRuns ?? state.runs} primary="summary" secondary="status" /></Panel>
      </section>
    </>
  );
}

function Repositories({ state }: { state: DevOpsState }) {
  return <section className="content-grid two"><Panel title="Repositories" icon={<GitBranch size={18} />}><Rows items={state.repositories} primary="name" secondary="provider" /></Panel><Panel title="Snapshots" icon={<Code2 size={18} />}><Rows items={state.snapshots} primary="snapshotNo" secondary="diffSummary" /></Panel></section>;
}

function PullRequests({ state }: { state: DevOpsState }) {
  return <section className="content-grid"><Panel title="Pull Requests" icon={<GitPullRequest size={18} />}><Rows items={state.pullRequests} primary="title" secondary="riskLevel" /></Panel></section>;
}

function Pipelines({ state }: { state: DevOpsState }) {
  return <section className="content-grid two"><Panel title="Pipelines" icon={<Workflow size={18} />}><Rows items={state.pipelines} primary="name" secondary="pipelineKey" /></Panel><Panel title="Pipeline Runs" icon={<Play size={18} />}><Rows items={state.pipelineRuns} primary="summary" secondary="runType" /></Panel></section>;
}

function Packages({ state }: { state: DevOpsState }) {
  return <section className="content-grid"><Panel title="Deployment Packages" icon={<PackageCheck size={18} />}><Rows items={state.packages} primary="packageNo" secondary="status" /></Panel></section>;
}

function Governance({ state }: { state: DevOpsState }) {
  return <section className="content-grid two"><Panel title="AI Code Policies" icon={<ShieldCheck size={18} />}><Rows items={state.policies} primary="policyKey" secondary="specialApprovalAreasCsv" /></Panel><Panel title="Guardrails" icon={<Bot size={18} />}><div className="phase7-summary"><strong>AI can analyze and propose patches only.</strong><span>Main/master merge, production deploy, high-risk scan findings and special-area changes are gated by review, CI and approval.</span></div></Panel></section>;
}

function Metric({ label, value, icon }: { label: string; value: any; icon: React.ReactNode }) {
  return <div className="metric">{icon}<span>{label}</span><strong>{value ?? 0}</strong></div>;
}

function Panel({ title, icon, children }: { title: string; icon: React.ReactNode; children: React.ReactNode }) {
  return <section className="panel"><header>{icon}<h3>{title}</h3></header>{children}</section>;
}

function Rows({ items, primary, secondary }: { items: any[]; primary: string; secondary: string }) {
  if (!items?.length) return <p className="empty">No DevOps data.</p>;
  return (
    <div className="action-list">
      {items.map((item, index) => (
        <div className="list-row" key={item.id ?? index}>
          <div>
            <strong>{item[primary] ?? item.name ?? item.title ?? item.packageNo}</strong>
            <span>{item[secondary] ?? item.status}</span>
            <small>{item.externalPrRef ?? item.repoUrl ?? item.artifactRef ?? item.createdAt ?? ''}</small>
          </div>
          <StatusBadge text={String(item.status ?? item.riskLevel ?? item.provider ?? item.runType ?? 'Active')} />
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

function seedState(): DevOpsState {
  const repo = { id: 'repo-1', name: 'HRM Leave Ops', provider: 'MockGit', repoUrl: 'https://mock.git.local/hrm-aiops/hrm-leave-ops' };
  const pr = { id: 'pr-1', repositoryId: repo.id, title: 'Fix leave balance rounding drift', externalPrRef: 'mock-pr-1', sourceBranch: 'ai/fix-leave-balance-rounding', targetBranch: 'main', status: 'Approved', riskLevel: 'Medium' };
  const pipeline = { id: 'pipe-1', repositoryId: repo.id, name: 'Mock HRM CI Pipeline', pipelineKey: 'mock.hrm-ci' };
  const pipelineRuns = [
    { id: 'build', summary: 'Mock build succeeded.', runType: 'Build', status: 'Succeeded' },
    { id: 'test', summary: 'Mock tests passed.', runType: 'Test', status: 'Succeeded' },
    { id: 'scan', summary: 'Mock code scan passed.', runType: 'CodeScan', status: 'Succeeded' }
  ];
  const runs = [{ id: 'run-1', summary: 'Seeded deployment package is ready after build/test/scan.', status: 'Succeeded' }];
  return {
    customerId: 'demo',
    projectId: 'project-1',
    customerName: 'Demo Customer',
    projectName: 'HRM AI Ops Pilot',
    dashboard: { repositories: 1, openPullRequests: 1, blockedPullRequests: 0, pipelines: 1, successfulPipelineRuns: 3, failedPipelineRuns: 0, readyPackages: 1, blockedPackages: 0, aiCodeAnalyses: 1, latestPullRequests: [pr], latestPipelineRuns: pipelineRuns, latestRuns: runs },
    repositories: [repo],
    branches: [{ id: 'branch-1', branchName: pr.sourceBranch, sourceBranch: 'main' }],
    pullRequests: [pr],
    pipelines: [pipeline],
    pipelineRuns,
    packages: [{ id: 'pkg-1', packageNo: 'DPKG-00001', version: '2026.06.15-demo', status: 'Ready', artifactRef: 'artifact://devops/packages/mock.zip' }],
    snapshots: [{ id: 'snap-1', snapshotNo: 'SNAP-00001', diffSummary: 'Metadata and masked diff preview only.' }],
    runs,
    policies: [{ id: 'pol-1', policyKey: 'devops.ai-code.default', specialApprovalAreasCsv: 'Payroll,Permission,Security,Integration,ProductionDeployment' }]
  };
}
