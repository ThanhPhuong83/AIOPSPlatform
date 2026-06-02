import {
  Activity, AlertTriangle, Bot, CheckCircle2, FileText, GitCompare,
  Layers3, Link2, LogOut, Play, Plus, RefreshCw, Rocket, Shield,
  ShieldCheck, Users, Lock
} from 'lucide-react';
import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  api, setCurrentUser,
  AuditLog, AiProposal, AiRun, ApplyRun, Blueprint, ConfigSpec,
  Customer, CustomerConnector, DocumentSignOff, Environment,
  EnvironmentSnapshot, FixProposal, HrmModule, Issue, PromptTemplate,
  Project, Requirement, RegressionTestPlan, RegressionTestRun,
  ReleaseDraft, ReleaseReadinessReport, SnapshotDiff, TraceChain, UrsDocument
} from './api';

// ── Types ─────────────────────────────────────────────────────────
type LoadState = 'idle' | 'loading' | 'error';
type Tab = 'overview' | 'documents' | 'issues' | 'ai' | 'apply' | 'audit';
type Engineer = { userId: string; name: string };

// ── App root ──────────────────────────────────────────────────────
export function App() {
  const [engineer, setEngineer] = useState<Engineer | null>(null);
  if (!engineer) return <LoginPage onLogin={setEngineer} />;
  return <Workspace engineer={engineer} onLogout={() => setEngineer(null)} />;
}

// ── Login ─────────────────────────────────────────────────────────
function LoginPage({ onLogin }: { onLogin: (e: Engineer) => void }) {
  const [error, setError] = useState('');

  function handle(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    const name = String(fd.get('name') ?? '').trim();
    const pass = String(fd.get('password') ?? '').trim();
    if (!name || !pass) { setError('Vui lòng nhập đầy đủ thông tin.'); return; }
    const userId = name.toLowerCase().replace(/\s+/g, '.');
    setCurrentUser(userId);
    onLogin({ userId, name });
  }

  return (
    <div className="login-bg">
      <div className="login-card">
        <div className="login-logo">
          <div className="login-logo-mark">HR</div>
          <div>
            <h1>HRM AI Ops</h1>
            <p>Nền tảng vận hành HRM với AI</p>
          </div>
        </div>

        <div className="login-heading">
          <h2>Chào mừng trở lại</h2>
          <p>Đăng nhập để tiếp tục quản lý khách hàng của bạn.</p>
        </div>

        <form className="login-form" onSubmit={handle}>
          <div>
            <label className="f-label" htmlFor="name">Tên kỹ sư</label>
            <input id="name" name="name" placeholder="Nguyen Van A" autoFocus />
          </div>
          <div>
            <label className="f-label" htmlFor="password">Mật khẩu</label>
            <input id="password" name="password" type="password" placeholder="••••••••" />
          </div>
          {error && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{error}</p>}
          <button type="submit" className="login-btn">
            <Lock size={15} /> Đăng nhập
          </button>
        </form>

        <p className="login-note">HRM AI Ops Platform v1 · Phase 1–17 Scaffold</p>
      </div>
    </div>
  );
}

// ── Workspace ─────────────────────────────────────────────────────
function Workspace({ engineer, onLogout }: { engineer: Engineer; onLogout: () => void }) {
  const [tab, setTab] = useState<Tab>('overview');
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [modules, setModules] = useState<HrmModule[]>([]);
  const [requirements, setRequirements] = useState<Requirement[]>([]);
  const [environments, setEnvironments] = useState<Environment[]>([]);
  const [urs, setUrs] = useState<UrsDocument[]>([]);
  const [blueprints, setBlueprints] = useState<Blueprint[]>([]);
  const [configs, setConfigs] = useState<ConfigSpec[]>([]);
  const [issues, setIssues] = useState<Issue[]>([]);
  const [fixProposals, setFixProposals] = useState<FixProposal[]>([]);
  const [testPlans, setTestPlans] = useState<RegressionTestPlan[]>([]);
  const [releaseDrafts, setReleaseDrafts] = useState<ReleaseDraft[]>([]);
  const [connectors, setConnectors] = useState<CustomerConnector[]>([]);
  const [applyRuns, setApplyRuns] = useState<ApplyRun[]>([]);
  const [snapshots, setSnapshots] = useState<EnvironmentSnapshot[]>([]);
  const [snapshotDiffs, setSnapshotDiffs] = useState<SnapshotDiff[]>([]);
  const [regressionRuns, setRegressionRuns] = useState<RegressionTestRun[]>([]);
  const [readinessReports, setReadinessReports] = useState<ReleaseReadinessReport[]>([]);
  const [aiRuns, setAiRuns] = useState<AiRun[]>([]);
  const [aiProposals, setAiProposals] = useState<AiProposal[]>([]);
  const [promptTemplates, setPromptTemplates] = useState<PromptTemplate[]>([]);
  const [signOffs, setSignOffs] = useState<DocumentSignOff[]>([]);
  const [traceChains, setTraceChains] = useState<TraceChain[]>([]);
  const [audits, setAudits] = useState<AuditLog[]>([]);
  const [customerId, setCustomerId] = useState('');
  const [projectId, setProjectId] = useState('');
  const [state, setState] = useState<LoadState>('idle');
  const [message, setMessage] = useState('');

  const selectedCustomer = customers.find(x => x.id === customerId);
  const selectedProject  = projects.find(x => x.id === projectId);

  async function refreshAll(nextCId = customerId, nextPId = projectId) {
    setState('loading');
    try {
      const [nc, nm] = await Promise.all([
        api.getList<Customer>('/api/customers'),
        api.get<HrmModule[]>('/api/hrm-modules')
      ]);
      setCustomers(nc); setModules(nm);
      setPromptTemplates(await api.get<PromptTemplate[]>('/api/ai/prompt-templates'));

      const cid = nextCId || nc[0]?.id || '';
      setCustomerId(cid);
      if (!cid) { setState('idle'); return; }

      const np = await api.getList<Project>(`/api/customers/${cid}/projects`);
      setProjects(np);
      const pid = nextPId || np[0]?.id || '';
      setProjectId(pid);

      const [na, nai] = await Promise.all([
        api.getList<AuditLog>(`/api/customers/${cid}/audit-logs`),
        api.getList<AiRun>(`/api/customers/${cid}/ai-runs`)
      ]);
      setAudits(na); setAiRuns(nai);

      if (pid) {
        const [nr, ne, nu, nb, ncf, ni, nfp, ntp, nrd, nco, nar, nsp, nsd, nrr, nrep, nso, ntc, nap] = await Promise.all([
          api.getList<Requirement>(`/api/customers/${cid}/projects/${pid}/requirements`),
          api.get<Environment[]>(`/api/customers/${cid}/projects/${pid}/environments`),
          api.get<UrsDocument[]>(`/api/customers/${cid}/projects/${pid}/urs`),
          api.get<Blueprint[]>(`/api/customers/${cid}/projects/${pid}/blueprints`),
          api.get<ConfigSpec[]>(`/api/customers/${cid}/projects/${pid}/config-specs`),
          api.getList<Issue>(`/api/customers/${cid}/projects/${pid}/issues`),
          api.get<FixProposal[]>(`/api/customers/${cid}/projects/${pid}/fix-proposals`),
          api.get<RegressionTestPlan[]>(`/api/customers/${cid}/projects/${pid}/regression-test-plans`),
          api.get<ReleaseDraft[]>(`/api/customers/${cid}/projects/${pid}/release-drafts`),
          api.get<CustomerConnector[]>(`/api/customers/${cid}/projects/${pid}/connectors`),
          api.get<ApplyRun[]>(`/api/customers/${cid}/projects/${pid}/apply-runs`),
          api.get<EnvironmentSnapshot[]>(`/api/customers/${cid}/projects/${pid}/environment-snapshots`),
          api.get<SnapshotDiff[]>(`/api/customers/${cid}/projects/${pid}/snapshot-diffs`),
          api.get<RegressionTestRun[]>(`/api/customers/${cid}/projects/${pid}/regression-test-runs`),
          api.get<ReleaseReadinessReport[]>(`/api/customers/${cid}/projects/${pid}/release-readiness-reports`),
          api.get<DocumentSignOff[]>(`/api/customers/${cid}/projects/${pid}/document-signoffs`),
          api.get<TraceChain[]>(`/api/customers/${cid}/projects/${pid}/traceability/view`),
          api.get<AiProposal[]>(`/api/customers/${cid}/projects/${pid}/ai-proposals`)
        ]);
        setRequirements(nr); setEnvironments(ne); setUrs(nu); setBlueprints(nb);
        setConfigs(ncf); setIssues(ni); setFixProposals(nfp); setTestPlans(ntp);
        setReleaseDrafts(nrd); setConnectors(nco); setApplyRuns(nar); setSnapshots(nsp);
        setSnapshotDiffs(nsd); setRegressionRuns(nrr); setReadinessReports(nrep);
        setSignOffs(nso); setTraceChains(ntc); setAiProposals(nap);
      }
      setState('idle');
    } catch (err) {
      setState('error');
      setMessage(err instanceof Error ? err.message : 'Lỗi không xác định');
    }
  }

  useEffect(() => { refreshAll(); }, []);

  async function submit<T>(action: () => Promise<T>, done: string) {
    try {
      setState('loading');
      await action();
      setMessage(done);
      await refreshAll();
    } catch (err) {
      setState('error');
      setMessage(err instanceof Error ? err.message : 'Lỗi không xác định');
    }
  }

  function signOff(route: string, id: string, done: string) {
    return submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/${route}/${id}/sign-off`, {
      signedOffBy: engineer.userId, role: 'Consultant', comment: 'Signed off.'
    }), done);
  }

  const pendingAi = aiProposals.filter(x => x.status === 'PendingReview').length;
  const openIssues = issues.filter(x => x.status !== 'Closed' && x.status !== 'Cancelled').length;

  const metrics = useMemo(() => [
    { label: 'Requirements', value: requirements.length, icon: <FileText size={16} /> },
    { label: 'URS', value: urs.length, icon: <FileText size={16} /> },
    { label: 'Blueprints', value: blueprints.length, icon: <Layers3 size={16} /> },
    { label: 'Config Specs', value: configs.length, icon: <ShieldCheck size={16} /> },
    { label: 'Open Issues', value: openIssues, icon: <AlertTriangle size={16} />, cls: openIssues > 0 ? 'warn' : '' },
    { label: 'Apply Runs', value: applyRuns.length, icon: <Rocket size={16} /> },
    { label: 'Release Ready', value: readinessReports.filter(x => x.status === 'Ready').length, icon: <CheckCircle2 size={16} />, cls: 'success' },
    { label: 'AI Proposals', value: aiProposals.length, icon: <Bot size={16} /> },
    { label: 'Pending Review', value: pendingAi, icon: <Activity size={16} />, cls: pendingAi > 0 ? 'warn' : '' },
    { label: 'Approved Docs', value: [...requirements, ...urs, ...blueprints, ...configs].filter(x => x.status === 'Approved').length, icon: <CheckCircle2 size={16} />, cls: 'success' },
  ], [requirements, urs, blueprints, configs, issues, applyRuns, readinessReports, aiProposals]);

  const tabs: { id: Tab; label: string; icon: React.ReactNode; badge?: number }[] = [
    { id: 'overview',   label: 'Tổng quan',    icon: <Activity size={15} /> },
    { id: 'documents',  label: 'Tài liệu',     icon: <FileText size={15} /> },
    { id: 'issues',     label: 'Issues & Ops', icon: <AlertTriangle size={15} />, badge: openIssues || undefined },
    { id: 'ai',         label: 'AI Ops',       icon: <Bot size={15} />,          badge: pendingAi || undefined },
    { id: 'apply',      label: 'Apply & Test', icon: <Rocket size={15} /> },
    { id: 'audit',      label: 'Audit',        icon: <Shield size={15} /> },
  ];

  return (
    <div className="ws-shell">
      {/* Topbar */}
      <header className="ws-topbar">
        <div className="ws-brand">
          <div className="ws-brand-mark">HR</div>
          <span className="ws-brand-name">HRM AI Ops</span>
        </div>
        <div className="top-divider" />
        <div className="ws-ctx">
          <span className="ws-ctx-lbl">Khách hàng</span>
          <select className="ws-ctx-sel" value={customerId} onChange={e => refreshAll(e.target.value, '')}>
            {customers.map(c => <option key={c.id} value={c.id}>{c.code} — {c.name}</option>)}
          </select>
          <span className="ws-ctx-lbl" style={{ marginLeft: 4 }}>Dự án</span>
          <select className="ws-ctx-sel" value={projectId} onChange={e => refreshAll(customerId, e.target.value)}>
            {projects.map(p => <option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}
          </select>
        </div>
        <div className="ws-topbar-right">
          <div className={`ws-status-pill ${state}`}>
            {state === 'loading' ? 'Đang tải...' : state === 'error' ? `Lỗi: ${message}` : message || 'Sẵn sàng'}
          </div>
          <button className="ws-refresh" onClick={() => refreshAll()}>
            <RefreshCw size={13} /> Làm mới
          </button>
          <div className="ws-user" onClick={onLogout} title="Đăng xuất">
            <div className="ws-avatar">{engineer.name[0].toUpperCase()}</div>
            <span>{engineer.name}</span>
            <LogOut size={13} />
          </div>
        </div>
      </header>

      <div className="ws-body">
        {/* Sidebar nav */}
        <nav className="ws-nav">
          <div className="ws-nav-section">Điều hướng</div>
          {tabs.map(t => (
            <button key={t.id} className={`ws-nav-item${tab === t.id ? ' active' : ''}`} onClick={() => setTab(t.id)}>
              {t.icon} {t.label}
              {t.badge ? <span className="nav-badge">{t.badge}</span> : null}
            </button>
          ))}
          <div className="ws-nav-section" style={{ marginTop: 8 }}>Khách hàng</div>
          <div style={{ padding: '4px 10px', fontSize: 12, color: '#64748b' }}>
            {selectedCustomer?.name ?? '—'}
          </div>
          <div style={{ padding: '0 10px 4px', fontSize: 11, color: '#3b5068' }}>
            {selectedProject?.name ?? '—'}
          </div>
        </nav>

        {/* Content */}
        <main className="ws-main">
          {tab === 'overview' && (
            <OverviewTab
              metrics={metrics}
              customers={customers} projects={projects} modules={modules}
              customerId={customerId} projectId={projectId}
              submit={submit} state={state}
            />
          )}
          {tab === 'documents' && (
            <DocumentsTab
              requirements={requirements} urs={urs} blueprints={blueprints} configs={configs}
              customerId={customerId} projectId={projectId}
              submit={submit} signOff={signOff}
            />
          )}
          {tab === 'issues' && (
            <IssuesTab
              issues={issues} fixProposals={fixProposals} testPlans={testPlans} releaseDrafts={releaseDrafts}
              configs={configs}
              customerId={customerId} projectId={projectId}
              submit={submit}
            />
          )}
          {tab === 'ai' && (
            <AiTab
              aiProposals={aiProposals} aiRuns={aiRuns} promptTemplates={promptTemplates}
              customerId={customerId} projectId={projectId}
              submit={submit}
            />
          )}
          {tab === 'apply' && (
            <ApplyTab
              applyRuns={applyRuns} snapshots={snapshots} snapshotDiffs={snapshotDiffs}
              regressionRuns={regressionRuns} readinessReports={readinessReports}
              environments={environments} connectors={connectors} fixProposals={fixProposals}
              customerId={customerId} projectId={projectId}
              submit={submit}
            />
          )}
          {tab === 'audit' && (
            <AuditTab
              audits={audits} signOffs={signOffs} traceChains={traceChains}
            />
          )}
        </main>
      </div>
    </div>
  );
}

// ── Overview Tab ──────────────────────────────────────────────────
function OverviewTab({ metrics, customers, projects, modules, customerId, projectId, submit, state }: any) {
  return (
    <>
      <div>
        <p className="page-title">Tổng quan</p>
        <p className="page-sub">Snapshot hiện tại của dự án đang chọn</p>
      </div>

      <div className="metric-grid">
        {metrics.map((m: any) => (
          <div key={m.label} className={`mc ${m.cls ?? ''}`}>
            {m.icon}
            <div className="mc-label">{m.label}</div>
            <div className="mc-value">{m.value}</div>
          </div>
        ))}
      </div>

      <div className="g2">
        <Card title="Tạo khách hàng / dự án" icon={<Users size={15} />}>
          <SmartForm
            fields={[['code','Code','ACME'],['name','Tên','ACME HR'],['industry','Ngành','Manufacturing']]}
            submitLabel="Khách hàng"
            onSubmit={b => submit(() => api.post('/api/customers', b), 'Đã tạo khách hàng')}
          />
          <SmartForm
            disabled={!customerId}
            fields={[['code','Code','HRM-OPS'],['name','Tên','HRM Operations'],['hrmProductName','Sản phẩm HRM','Custom HRM']]}
            submitLabel="Dự án"
            onSubmit={b => submit(() => api.post(`/api/customers/${customerId}/projects`, b), 'Đã tạo dự án')}
          />
        </Card>

        <Card title="HRM Modules" icon={<ShieldCheck size={15} />}>
          <ItemList empty="Chưa có module" items={modules.map((m: HrmModule) => ({
            key: m.id,
            title: `${m.code} — ${m.name}`,
            meta: `Mức rủi ro mặc định: ${m.defaultRiskLevel} · ${m.description}`,
            badge: riskBadge(m.defaultRiskLevel)
          }))} />
        </Card>
      </div>
    </>
  );
}

// ── Documents Tab ─────────────────────────────────────────────────
function DocumentsTab({ requirements, urs, blueprints, configs, customerId, projectId, submit, signOff }: any) {
  return (
    <>
      <div><p className="page-title">Vòng đời tài liệu</p><p className="page-sub">REQ → URS → Blueprint → Config Spec</p></div>

      <div className="g2">
        <Card title="Yêu cầu (Requirements)" icon={<FileText size={15} />} count={requirements.length}>
          <form className="f-stack" onSubmit={e => {
            e.preventDefault();
            const fd = new FormData(e.currentTarget);
            submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/requirements/manual`, {
              title: fd.get('title'), contentText: fd.get('contentText'), createdBy: 'demo.user'
            }), 'Đã tạo requirement');
            (e.target as HTMLFormElement).reset();
          }}>
            <input name="title" placeholder="Tiêu đề requirement" required disabled={!projectId} />
            <textarea name="contentText" placeholder="Mô tả nội dung..." disabled={!projectId} />
            <button disabled={!projectId} className="btn btn-primary"><Plus size={13} /> Thêm Requirement</button>
          </form>
          <ItemList empty="Chưa có requirement" items={requirements.map((r: Requirement) => ({
            key: r.id,
            title: `${r.requirementNo} v${r.version} — ${r.title}`,
            meta: `${r.status}${r.isLatest ? '' : ' · cũ'}`,
            badge: statusBadge(r.status),
            actions: (
              <div className="item-actions">
                <button className="btn" onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/requirements/${r.id}/generate-urs`), 'Đề xuất URS đã tạo')}>
                  <Bot size={12} /> Gen URS
                </button>
                <button className="btn" disabled={r.status === 'Approved'} onClick={() => signOff('requirements', r.id, 'Requirement đã ký')}>
                  <CheckCircle2 size={12} /> Ký
                </button>
              </div>
            )
          }))} />
        </Card>

        <Card title="URS" icon={<Bot size={15} />} count={urs.length}>
          <ItemList empty="Chưa có URS" items={urs.map((u: UrsDocument) => ({
            key: u.id,
            title: `${u.ursNo} v${u.version} — ${u.title}`,
            meta: trim(u.content),
            badge: statusBadge(u.status),
            actions: (
              <div className="item-actions">
                <button className="btn" onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/urs/${u.id}/generate-blueprint`), 'Đề xuất Blueprint đã tạo')}>
                  <Bot size={12} /> Gen BP
                </button>
                <button className="btn" disabled={u.status === 'Approved'} onClick={() => signOff('urs', u.id, 'URS đã ký')}>
                  <CheckCircle2 size={12} /> Ký
                </button>
              </div>
            )
          }))} />
        </Card>
      </div>

      <div className="g2">
        <Card title="Blueprints" icon={<Layers3 size={15} />} count={blueprints.length}>
          <ItemList empty="Chưa có blueprint" items={blueprints.map((b: Blueprint) => ({
            key: b.id,
            title: `${b.blueprintNo} v${b.version} — ${b.type}`,
            meta: trim(b.content),
            badge: statusBadge(b.status),
            actions: (
              <div className="item-actions">
                <button className="btn" onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/blueprints/${b.id}/generate-config-spec`, { moduleName: 'Leave Management' }), 'Đề xuất Config Spec đã tạo')}>
                  <Bot size={12} /> Gen CFG
                </button>
                <button className="btn" disabled={b.status === 'Approved'} onClick={() => signOff('blueprints', b.id, 'Blueprint đã ký')}>
                  <CheckCircle2 size={12} /> Ký
                </button>
              </div>
            )
          }))} />
        </Card>

        <Card title="Config Specifications" icon={<ShieldCheck size={15} />} count={configs.length}>
          <ItemList empty="Chưa có config spec" items={configs.map((c: ConfigSpec) => ({
            key: c.id,
            title: `${c.configNo} v${c.version} — ${c.moduleName}`,
            meta: `${trim(c.content)}`,
            badge: riskBadge(c.riskLevel),
            actions: (
              <div className="item-actions">
                <span className="badge">{statusBadge(c.status)}</span>
                <button className="btn" disabled={c.status === 'Approved'} onClick={() => signOff('config-specs', c.id, 'Config spec đã ký')}>
                  <CheckCircle2 size={12} /> Ký
                </button>
              </div>
            )
          }))} />
        </Card>
      </div>
    </>
  );
}

// ── Issues Tab ────────────────────────────────────────────────────
function IssuesTab({ issues, fixProposals, testPlans, releaseDrafts, configs, customerId, projectId, submit }: any) {
  return (
    <>
      <div><p className="page-title">Issues & Vận hành</p><p className="page-sub">Quản lý sự cố + AI chẩn đoán</p></div>

      <div className="g2">
        <Card title="Tạo Issue" icon={<AlertTriangle size={15} />}>
          <form className="f-stack" onSubmit={e => {
            e.preventDefault();
            const fd = new FormData(e.currentTarget);
            const cid2 = String(fd.get('linkedConfigId') ?? '');
            submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues`, {
              environmentId: null,
              linkedEntityType: cid2 ? 'ConfigSpec' : null,
              linkedEntityId: cid2 || null,
              title: fd.get('title'),
              description: fd.get('description'),
              category: fd.get('category') || null,
              severity: fd.get('severity'),
              priority: fd.get('priority'),
              reportedBy: fd.get('reportedBy') || 'customer.hr'
            }), 'Đã tạo issue');
            (e.target as HTMLFormElement).reset();
          }}>
            <input name="title" placeholder="Tiêu đề issue" required disabled={!projectId} />
            <textarea name="description" placeholder="Mô tả chi tiết..." disabled={!projectId} />
            <div className="f-row3">
              <select name="category" defaultValue="" disabled={!projectId}>
                <option value="">Tự phân loại</option>
                <option>Functional</option><option>Configuration</option>
                <option>Data</option><option>Integration</option>
                <option>Security</option><option>Permission</option>
                <option>Payroll</option><option>Performance</option>
                <option>ProductionDatabase</option><option>Other</option>
              </select>
              <select name="severity" defaultValue="High" disabled={!projectId}>
                <option>Low</option><option>Medium</option>
                <option>High</option><option>Critical</option>
              </select>
              <select name="priority" defaultValue="P2" disabled={!projectId}>
                <option value="P0">P0 — Critical</option>
                <option value="P1">P1 — High</option>
                <option value="P2">P2 — Medium</option>
                <option value="P3">P3 — Low</option>
                <option value="P4">P4 — Minimal</option>
              </select>
            </div>
            <div className="f-row">
              <input name="reportedBy" placeholder="Người báo cáo" defaultValue="customer.hr" disabled={!projectId} />
              <select name="linkedConfigId" defaultValue="" disabled={!projectId}>
                <option value="">Không liên kết config</option>
                {configs.map((c: ConfigSpec) => <option key={c.id} value={c.id}>{c.configNo} - {c.moduleName}</option>)}
              </select>
            </div>
            <button className="btn btn-primary" disabled={!projectId}><Plus size={13} /> Tạo Issue</button>
          </form>
        </Card>

        <Card title="Danh sách Issues" icon={<AlertTriangle size={15} />} count={issues.length}>
          <ItemList empty="Chưa có issue" items={issues.map((i: Issue) => ({
            key: i.id,
            title: `${i.issueNo} — ${i.title}`,
            meta: `${i.category} · ${i.severity} · ${i.priority} · ${trim(i.rootCauseSummary ?? i.description)}`,
            badge: issueBadge(i.status),
            actions: (
              <div className="item-actions">
                <button className="btn" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/classify`), 'Phân loại AI xong')}>
                  <Bot size={11} /> Classify
                </button>
                <button className="btn" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/root-cause`), 'RCA xong')}>RCA</button>
                <button className="btn" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/fix-proposal`), 'Fix proposal xong')}>Fix</button>
                <button className="btn" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/change-request-draft`), 'CR draft xong')}>CR</button>
                <button className="btn" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/regression-test-plan`), 'Test plan xong')}>Test</button>
                <button className="btn" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/release-draft`), 'Release draft xong')}>Release</button>
                <button className="btn btn-danger" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/close`, { closedBy: 'support.lead', resolutionNote: 'Resolved.' }), 'Issue đã đóng')}>Đóng</button>
              </div>
            )
          }))} />
        </Card>
      </div>

      <div className="g2">
        <Card title="Fix Proposals" icon={<Rocket size={15} />} count={fixProposals.length}>
          <ItemList empty="Chưa có fix proposal" items={fixProposals.slice(0, 6).map((f: FixProposal) => ({
            key: f.id,
            title: f.title,
            meta: trim(f.proposedSolution),
            badge: riskBadge(f.riskLevel)
          }))} />
        </Card>
        <Card title="Test Plans & Release Drafts" icon={<GitCompare size={15} />}>
          <ItemList empty="Chưa có test plan" items={testPlans.slice(0, 4).map((t: RegressionTestPlan) => ({
            key: t.id, title: `${t.testPlanNo} — ${t.title}`, meta: trim(t.content), badge: riskBadge(t.riskLevel)
          }))} />
          <ItemList empty="Chưa có release draft" items={releaseDrafts.slice(0, 4).map((r: ReleaseDraft) => ({
            key: r.id, title: `${r.releaseDraftNo} — ${r.title}`, meta: trim(r.releaseNotes), badge: riskBadge(r.riskLevel)
          }))} />
        </Card>
      </div>
    </>
  );
}

// ── AI Tab ────────────────────────────────────────────────────────
function AiTab({ aiProposals, aiRuns, promptTemplates, customerId, projectId, submit }: any) {
  return (
    <>
      <div><p className="page-title">AI Ops</p><p className="page-sub">Đề xuất AI + Lịch sử chạy + Prompt templates</p></div>

      <Card title="Đề xuất AI — Cần xem xét" icon={<Bot size={15} />} count={aiProposals.length}>
        <ItemList empty="Chưa có đề xuất AI" items={aiProposals.map((a: AiProposal) => ({
          key: a.id,
          title: `${a.taskType} — ${a.title}`,
          meta: trim(a.proposedContent),
          badge: aiProposalBadge(a.status),
          actions: a.status === 'PendingReview' ? (
            <div className="item-actions">
              <button className="btn btn-success" onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/ai-proposals/${a.id}/accept`, { reviewedBy: 'consultant', comment: 'Accepted.' }), 'Đã chấp nhận')}>
                <CheckCircle2 size={12} /> Chấp nhận
              </button>
              <button className="btn btn-danger" onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/ai-proposals/${a.id}/reject`, { reviewedBy: 'consultant', comment: 'Rejected.' }), 'Đã từ chối')}>
                Từ chối
              </button>
            </div>
          ) : undefined
        }))} />
      </Card>

      <div className="g2">
        <Card title="Lịch sử AI Runs" icon={<Activity size={15} />} count={aiRuns.length}>
          <ItemList empty="Chưa có AI run" items={aiRuns.slice(0, 10).map((r: AiRun) => ({
            key: r.id,
            title: `${r.runType} — ${r.status}`,
            meta: `${r.provider} · ${r.promptTemplateKey ?? 'no-template'} · ${trim(r.maskedInputPreview ?? r.outputSummary ?? '')}`,
            badge: aiRunBadge(r.status)
          }))} />
        </Card>

        <Card title="Prompt Templates" icon={<Bot size={15} />} count={promptTemplates.length}>
          <form className="f-stack" onSubmit={e => {
            e.preventDefault();
            const fd = new FormData(e.currentTarget);
            submit(() => api.post('/api/ai/prompt-templates', {
              key: fd.get('key'), name: fd.get('name'), taskType: fd.get('taskType'),
              description: fd.get('description'), systemPrompt: fd.get('systemPrompt'),
              userPromptTemplate: fd.get('userPromptTemplate'),
              outputJsonSchema: fd.get('outputJsonSchema') || '{"type":"object","required":["title","content"],"properties":{"title":{"type":"string"},"content":{"type":"string"}}}',
              createdBy: 'platform.admin'
            }), 'Đã tạo prompt template');
            (e.target as HTMLFormElement).reset();
          }}>
            <div className="f-row">
              <input name="key" placeholder="template-key" required />
              <input name="name" placeholder="Tên template" required />
            </div>
            <select name="taskType" defaultValue="GenerateUrs">
              <option>GenerateUrs</option><option>GenerateBlueprint</option>
              <option>GenerateConfigSpec</option><option>ClassifyIssue</option>
              <option>AnalyzeRootCause</option><option>GenerateFixProposal</option>
              <option>GenerateChangeRequest</option><option>GenerateRegressionTestPlan</option>
              <option>GenerateReleaseDraft</option><option>GenerateKnowledgeUpdate</option>
            </select>
            <input name="description" placeholder="Mô tả" required />
            <textarea name="systemPrompt" placeholder="System prompt..." required />
            <textarea name="userPromptTemplate" placeholder="User prompt template..." required />
            <button className="btn btn-primary"><Plus size={13} /> Tạo Template</button>
          </form>
          <ItemList empty="Chưa có template" items={promptTemplates.map((t: PromptTemplate) => ({
            key: t.template.id,
            title: `${t.template.key} — ${t.template.taskType}`,
            meta: t.template.description,
            badge: <span className="badge b-blue">{t.versions[0] ? `v${t.versions[0].version}` : 'no ver'}</span>
          }))} />
        </Card>
      </div>
    </>
  );
}

// ── Apply Tab ─────────────────────────────────────────────────────
function ApplyTab({ applyRuns, snapshots, snapshotDiffs, regressionRuns, readinessReports, environments, connectors, fixProposals, customerId, projectId, submit }: any) {
  const testEnvs = environments.filter((e: Environment) => e.kind === 'Test' || e.kind === 'Uat');
  return (
    <>
      <div><p className="page-title">Apply & Test</p><p className="page-sub">Controlled apply · Test/UAT · Regression</p></div>

      <div className="g2">
        <Card title="Dry Run / Apply" icon={<Rocket size={15} />}>
          <form className="f-stack" onSubmit={e => {
            e.preventDefault();
            const fd = new FormData(e.currentTarget);
            const cid2 = String(fd.get('connectorId') ?? '');
            submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/controlled-test-apply/dry-run`, {
              environmentId: fd.get('environmentId'),
              connectorId: cid2 || null,
              sourceType: 'FixProposal',
              sourceId: fd.get('sourceId'),
              requestedBy: 'test.operator'
            }), 'Dry run xong');
          }}>
            <select name="environmentId" defaultValue={testEnvs[0]?.id ?? ''} disabled={testEnvs.length === 0}>
              {testEnvs.map((e: Environment) => <option key={e.id} value={e.id}>{e.name} ({e.kind})</option>)}
            </select>
            <select name="connectorId" defaultValue="">
              <option value="">Auto connector</option>
              {connectors.map((c: CustomerConnector) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
            <select name="sourceId" disabled={fixProposals.length === 0}>
              {fixProposals.map((f: FixProposal) => <option key={f.id} value={f.id}>{f.title} — {f.riskLevel}</option>)}
            </select>
            <button className="btn btn-primary" disabled={testEnvs.length === 0 || fixProposals.length === 0}>
              <Play size={13} /> Dry Run
            </button>
          </form>
        </Card>

        <Card title="Apply Runs" icon={<Rocket size={15} />} count={applyRuns.length}>
          <ItemList empty="Chưa có apply run" items={applyRuns.slice(0, 6).map((a: ApplyRun) => ({
            key: a.id,
            title: `${a.applyRunNo} — ${a.sourceType}`,
            meta: `${a.riskLevel} · ${trim(a.summary || a.rollbackRecommendation || '')}`,
            badge: applyBadge(a.status),
            actions: (
              <button className="btn btn-primary"
                disabled={a.status !== 'DryRunSucceeded' && a.status !== 'ApprovalRequired'}
                onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/apply-runs/${a.id}/execute`, { requestedBy: 'test.operator' }), 'Apply đã thực hiện')}>
                <Rocket size={12} /> Execute
              </button>
            )
          }))} />
        </Card>
      </div>

      <div className="g2">
        <Card title="Regression & Readiness" icon={<GitCompare size={15} />}>
          <ItemList empty="Chưa có readiness report" items={readinessReports.slice(0, 4).map((r: ReleaseReadinessReport) => ({
            key: r.id, title: `${r.reportNo} — ${r.status}`, meta: trim(r.summary || r.blockers),
            badge: r.status === 'Ready' ? <span className="badge b-green">Ready</span> : <span className="badge b-red">Not Ready</span>
          }))} />
          <ItemList empty="Chưa có regression run" items={regressionRuns.slice(0, 4).map((r: RegressionTestRun) => ({
            key: r.id, title: `${r.runNo} — ${r.status}`,
            meta: `Pass: ${r.passedTests}/${r.totalTests} · ${trim(r.summary)}`,
            badge: r.status === 'Passed' ? <span className="badge b-green">Passed</span> : <span className="badge b-red">{r.status}</span>
          }))} />
        </Card>

        <Card title="Snapshots" icon={<GitCompare size={15} />}>
          <ItemList empty="Chưa có snapshot" items={snapshots.slice(0, 4).map((s: EnvironmentSnapshot) => ({
            key: s.id, title: `${s.snapshotNo} — ${s.stage}`, meta: trim(s.maskedSummary),
            badge: <span className="badge b-default">{s.kind}</span>
          }))} />
          <ItemList empty="Chưa có diff" items={snapshotDiffs.slice(0, 4).map((d: SnapshotDiff) => ({
            key: d.id, title: `${d.snapshotKind} diff`, meta: trim(d.diffSummary),
            badge: riskBadge(d.riskLevel)
          }))} />
        </Card>
      </div>
    </>
  );
}

// ── Audit Tab ─────────────────────────────────────────────────────
function AuditTab({ audits, signOffs, traceChains }: any) {
  return (
    <>
      <div><p className="page-title">Audit & Traceability</p><p className="page-sub">Sign-offs · Audit logs · Traceability chains</p></div>

      <Card title="Traceability — REQ → URS → Blueprint → Config" icon={<Link2 size={15} />}>
        <div className="trace-list">
          {traceChains.length === 0 && <div className="empty-msg">Chưa có trace chain</div>}
          {traceChains.map((c: TraceChain, i: number) => (
            <div className="trace-row" key={`${c.requirement.id}-${i}`}>
              <div className={`trace-node ${c.requirement.status.toLowerCase()}`}>
                <span className="tn-lbl">REQ</span>
                <strong className="tn-text">{c.requirement.requirementNo} v{c.requirement.version}</strong>
                <span className="tn-stat">{c.requirement.status}</span>
              </div>
              <div className={`trace-node ${c.urs ? c.urs.status.toLowerCase() : 'gap'}`}>
                <span className="tn-lbl">URS</span>
                <strong className="tn-text">{c.urs ? `${c.urs.ursNo} v${c.urs.version}` : 'Chưa có'}</strong>
                <span className="tn-stat">{c.urs?.status ?? 'Gap'}</span>
              </div>
              <div className={`trace-node ${c.blueprint ? c.blueprint.status.toLowerCase() : 'gap'}`}>
                <span className="tn-lbl">Blueprint</span>
                <strong className="tn-text">{c.blueprint ? `${c.blueprint.blueprintNo} v${c.blueprint.version}` : 'Chưa có'}</strong>
                <span className="tn-stat">{c.blueprint?.status ?? 'Gap'}</span>
              </div>
              <div className={`trace-node ${c.configSpec ? c.configSpec.riskLevel.toLowerCase().replace(' ','-') : 'gap'}`}>
                <span className="tn-lbl">Config</span>
                <strong className="tn-text">{c.configSpec ? `${c.configSpec.configNo} ${c.configSpec.riskLevel}` : 'Chưa có'}</strong>
                <span className="tn-stat">{c.configSpec?.status ?? 'Gap'}</span>
              </div>
            </div>
          ))}
        </div>
      </Card>

      <div className="g2">
        <Card title="Sign-offs" icon={<CheckCircle2 size={15} />} count={signOffs.length}>
          <ItemList empty="Chưa có sign-off" items={signOffs.slice(0, 8).map((s: DocumentSignOff) => ({
            key: s.id,
            title: `${s.documentKind} v${s.version}`,
            meta: `${s.signedOffBy} · ${s.role ?? 'Reviewer'} · ${new Date(s.signedOffAt).toLocaleString('vi-VN')}`
          }))} />
        </Card>

        <Card title="Audit Logs" icon={<Shield size={15} />} count={audits.length}>
          <ItemList empty="Chưa có audit log" items={audits.slice(0, 10).map((a: AuditLog) => ({
            key: a.id,
            title: a.action,
            meta: `${a.entityType} · ${new Date(a.createdAt).toLocaleString('vi-VN')}`
          }))} />
        </Card>
      </div>
    </>
  );
}

// ── Reusable components ───────────────────────────────────────────
function Card({ title, icon, count, children }: { title: string; icon: React.ReactNode; count?: number; children: React.ReactNode }) {
  return (
    <div className="card">
      <div className="card-header">
        {icon}
        <h3>{title}</h3>
        {count !== undefined && <span className="card-count">{count}</span>}
      </div>
      <div className="card-body">{children}</div>
    </div>
  );
}

function ItemList({ items, empty }: { items: { key: string; title: string; meta: string; badge?: React.ReactNode; actions?: React.ReactNode }[]; empty: string }) {
  if (items.length === 0) return <div className="empty-msg">{empty}</div>;
  return (
    <div className="item-list">
      {items.map(item => (
        <div className="item" key={item.key}>
          <div className="item-body">
            <span className="item-title">{item.title}</span>
            <span className="item-meta">{item.meta}</span>
          </div>
          {item.badge && <div style={{ flexShrink: 0 }}>{item.badge}</div>}
          {item.actions}
        </div>
      ))}
    </div>
  );
}

function SmartForm({ fields, submitLabel, disabled, onSubmit }: { fields: [string, string, string][]; submitLabel: string; disabled?: boolean; onSubmit: (b: unknown) => void }) {
  return (
    <form className="f-row" style={{ alignItems: 'end' }} onSubmit={(e: FormEvent<HTMLFormElement>) => {
      e.preventDefault();
      const fd = new FormData(e.currentTarget);
      onSubmit(Object.fromEntries(fields.map(([k]) => [k, fd.get(k)])));
      e.currentTarget.reset();
    }}>
      {fields.map(([k, , ph]) => (
        <input key={k} name={k} placeholder={ph} disabled={disabled} required={k === 'code' || k === 'name'} />
      ))}
      <button disabled={disabled} className="btn btn-primary" style={{ whiteSpace: 'nowrap' }}>
        <Plus size={13} /> {submitLabel}
      </button>
    </form>
  );
}

// ── Badge helpers ─────────────────────────────────────────────────
function statusBadge(status: string) {
  const cls = status === 'Approved' ? 'b-green' : status === 'Draft' ? 'b-default' : status === 'Archived' ? 'b-red' : 'b-yellow';
  return <span className={`badge ${cls}`}>{status}</span>;
}
function riskBadge(risk: string) {
  const cls = risk === 'Critical' ? 'b-red' : risk === 'High' ? 'b-yellow' : risk === 'Low' ? 'b-green' : 'b-blue';
  return <span className={`badge ${cls}`}>{risk}</span>;
}
function issueBadge(status: string) {
  const cls = status === 'Closed' || status === 'Resolved' ? 'b-green' : status === 'InProgress' ? 'b-blue' : status === 'Cancelled' ? 'b-default' : 'b-yellow';
  return <span className={`badge ${cls}`}>{status}</span>;
}
function aiProposalBadge(status: string) {
  const cls = status === 'Accepted' ? 'b-green' : status === 'PendingReview' ? 'b-yellow' : status === 'Rejected' || status === 'FailedValidation' ? 'b-red' : 'b-default';
  return <span className={`badge ${cls}`}>{status}</span>;
}
function aiRunBadge(status: string) {
  const cls = status === 'Completed' ? 'b-green' : status === 'Running' || status === 'Queued' ? 'b-blue' : status === 'Failed' ? 'b-red' : 'b-default';
  return <span className={`badge ${cls}`}>{status}</span>;
}
function applyBadge(status: string) {
  const cls = status === 'Applied' || status === 'ReleaseReady' ? 'b-green' : status === 'Applying' || status === 'DryRunSucceeded' ? 'b-blue' : status.includes('Failed') ? 'b-red' : 'b-yellow';
  return <span className={`badge ${cls}`}>{status}</span>;
}

function trim(val: string, max = 120) {
  if (!val) return '';
  return val.length > max ? val.slice(0, max) + '…' : val;
}
