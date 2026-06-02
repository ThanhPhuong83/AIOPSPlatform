import {
  Activity, AlertTriangle, Bot, Building2, CheckCircle2, ChevronDown,
  FileText, GitCompare, Layers3, LogOut, Moon, Plus, RefreshCw, Rocket,
  Shield, ShieldCheck, Sun, Users, Lock, Bell
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

type LoadState = 'idle' | 'loading' | 'error';
type Tab = 'dashboard' | 'documents' | 'issues' | 'ai' | 'apply' | 'audit';
type Engineer = { userId: string; name: string };

// ── Root ──────────────────────────────────────────────────────────
export function App() {
  const [engineer, setEngineer] = useState<Engineer | null>(null);
  const [dark, setDark] = useState(() => localStorage.getItem('theme') === 'dark');

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
    localStorage.setItem('theme', dark ? 'dark' : 'light');
  }, [dark]);

  if (!engineer) return <LoginPage onLogin={setEngineer} dark={dark} onToggleDark={() => setDark(d => !d)} />;
  return <Workspace engineer={engineer} onLogout={() => setEngineer(null)} dark={dark} onToggleDark={() => setDark(d => !d)} />;
}

// ── Login ─────────────────────────────────────────────────────────
function LoginPage({ onLogin, dark, onToggleDark }: { onLogin: (e: Engineer) => void; dark: boolean; onToggleDark: () => void }) {
  const [err, setErr] = useState('');
  function handle(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    const name = String(fd.get('name') ?? '').trim();
    const pass = String(fd.get('password') ?? '').trim();
    if (!name || !pass) { setErr('Vui lòng nhập đầy đủ thông tin.'); return; }
    const userId = name.toLowerCase().replace(/\s+/g, '.');
    setCurrentUser(userId);
    onLogin({ userId, name });
  }
  return (
    <div className="login-bg">
      <div className="login-card">
        <div className="login-logo">
          <div className="login-logo-mark">
            <Building2 size={26} color="#fff" />
          </div>
          <div>
            <h1>HRM AI Ops Platform</h1>
            <p>Nền tảng vận hành HRM tích hợp AI</p>
          </div>
        </div>
        <div className="login-heading">
          <h2>Xin chào, Kỹ sư!</h2>
          <p>Đăng nhập để truy cập hệ thống quản lý khách hàng của bạn.</p>
        </div>
        <form className="login-form" onSubmit={handle}>
          <div>
            <label className="f-label">Tên kỹ sư</label>
            <input name="name" placeholder="Nguyễn Văn A" autoFocus />
          </div>
          <div>
            <label className="f-label">Mật khẩu</label>
            <input name="password" type="password" placeholder="••••••••" />
          </div>
          {err && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{err}</p>}
          <button type="submit" className="login-btn"><Lock size={15} /> Đăng nhập</button>
        </form>
        <p className="login-note">HRM AI Ops · v1.0 · Phase 1–17 Scaffold</p>
        <button className="theme-toggle-login" onClick={onToggleDark} title="Toggle theme">
          {dark ? <Sun size={16} /> : <Moon size={16} />}
          {dark ? 'Chế độ sáng' : 'Chế độ tối'}
        </button>
      </div>
    </div>
  );
}

// ── Workspace ─────────────────────────────────────────────────────
function Workspace({ engineer, onLogout, dark, onToggleDark }: { engineer: Engineer; onLogout: () => void; dark: boolean; onToggleDark: () => void }) {
  const [tab, setTab] = useState<Tab>('dashboard');
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

  const selCustomer = customers.find(x => x.id === customerId);
  const selProject  = projects.find(x => x.id === projectId);

  async function refreshAll(cid = customerId, pid = projectId) {
    setState('loading');
    try {
      const [nc, nm] = await Promise.all([
        api.getList<Customer>('/api/customers'),
        api.get<HrmModule[]>('/api/hrm-modules')
      ]);
      setCustomers(nc); setModules(nm);
      setPromptTemplates(await api.get<PromptTemplate[]>('/api/ai/prompt-templates'));
      const activeCid = cid || nc[0]?.id || '';
      setCustomerId(activeCid);
      if (!activeCid) { setState('idle'); return; }

      const np = await api.getList<Project>(`/api/customers/${activeCid}/projects`);
      setProjects(np);
      const activePid = pid || np[0]?.id || '';
      setProjectId(activePid);

      const [na, nai] = await Promise.all([
        api.getList<AuditLog>(`/api/customers/${activeCid}/audit-logs`),
        api.getList<AiRun>(`/api/customers/${activeCid}/ai-runs`)
      ]);
      setAudits(na); setAiRuns(nai);

      if (activePid) {
        const [nr, ne, nu, nb, ncf, ni, nfp, ntp, nrd, nco, nar, nsp, nsd, nrr, nrep, nso, ntc, nap] = await Promise.all([
          api.getList<Requirement>(`/api/customers/${activeCid}/projects/${activePid}/requirements`),
          api.get<Environment[]>(`/api/customers/${activeCid}/projects/${activePid}/environments`),
          api.get<UrsDocument[]>(`/api/customers/${activeCid}/projects/${activePid}/urs`),
          api.get<Blueprint[]>(`/api/customers/${activeCid}/projects/${activePid}/blueprints`),
          api.get<ConfigSpec[]>(`/api/customers/${activeCid}/projects/${activePid}/config-specs`),
          api.getList<Issue>(`/api/customers/${activeCid}/projects/${activePid}/issues`),
          api.get<FixProposal[]>(`/api/customers/${activeCid}/projects/${activePid}/fix-proposals`),
          api.get<RegressionTestPlan[]>(`/api/customers/${activeCid}/projects/${activePid}/regression-test-plans`),
          api.get<ReleaseDraft[]>(`/api/customers/${activeCid}/projects/${activePid}/release-drafts`),
          api.get<CustomerConnector[]>(`/api/customers/${activeCid}/projects/${activePid}/connectors`),
          api.get<ApplyRun[]>(`/api/customers/${activeCid}/projects/${activePid}/apply-runs`),
          api.get<EnvironmentSnapshot[]>(`/api/customers/${activeCid}/projects/${activePid}/environment-snapshots`),
          api.get<SnapshotDiff[]>(`/api/customers/${activeCid}/projects/${activePid}/snapshot-diffs`),
          api.get<RegressionTestRun[]>(`/api/customers/${activeCid}/projects/${activePid}/regression-test-runs`),
          api.get<ReleaseReadinessReport[]>(`/api/customers/${activeCid}/projects/${activePid}/release-readiness-reports`),
          api.get<DocumentSignOff[]>(`/api/customers/${activeCid}/projects/${activePid}/document-signoffs`),
          api.get<TraceChain[]>(`/api/customers/${activeCid}/projects/${activePid}/traceability/view`),
          api.get<AiProposal[]>(`/api/customers/${activeCid}/projects/${activePid}/ai-proposals`)
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

  const openIssues  = issues.filter(x => x.status !== 'Closed' && x.status !== 'Cancelled').length;
  const pendingAi   = aiProposals.filter(x => x.status === 'PendingReview').length;
  const approvedDocs = [...requirements, ...urs, ...blueprints, ...configs].filter(x => x.status === 'Approved').length;

  const navMain: { id: Tab; label: string; icon: React.ReactNode }[] = [
    { id: 'dashboard', label: 'Dashboard',     icon: <Activity size={13} /> },
    { id: 'documents', label: 'Tài liệu',      icon: <FileText size={13} /> },
    { id: 'issues',    label: 'Issues & Ops',  icon: <AlertTriangle size={13} /> },
    { id: 'ai',        label: 'AI Ops',         icon: <Bot size={13} /> },
    { id: 'apply',     label: 'Apply & Test',   icon: <Rocket size={13} /> },
    { id: 'audit',     label: 'Audit',          icon: <Shield size={13} /> },
  ];

  const subNav: Record<Tab, string[]> = {
    dashboard: ['Tổng quan', 'Tạo khách hàng', 'Tạo dự án'],
    documents: ['Requirements', 'URS', 'Blueprints', 'Config Specs'],
    issues:    ['Danh sách', 'Fix Proposals', 'Test Plans', 'Release Drafts'],
    ai:        ['Đề xuất AI', 'AI Runs', 'Prompt Templates'],
    apply:     ['Dry Run / Apply', 'Apply Runs', 'Regression', 'Snapshots'],
    audit:     ['Traceability', 'Sign-offs', 'Audit Logs'],
  };

  const pageTitles: Record<Tab, [string, string]> = {
    dashboard: ['Dashboard', `Dự liệu từ API · cập nhật ${new Date().toLocaleDateString('vi-VN')}`],
    documents: ['Vòng đời tài liệu', 'REQ → URS → Blueprint → Config Spec'],
    issues:    ['Issues & Vận hành', 'Quản lý sự cố · AI chẩn đoán · Fix proposals'],
    ai:        ['AI Ops', 'Đề xuất AI · Lịch sử chạy · Prompt templates'],
    apply:     ['Apply & Test', 'Controlled apply · Test/UAT only · Không apply production'],
    audit:     ['Audit & Traceability', 'Sign-offs · Audit logs · Traceability chains'],
  };

  const [title, subtitle] = pageTitles[tab];

  return (
    <div className="ws-shell">
      {/* ── Nav row 1 ── */}
      <div className="nav1">
        <div className="nav1-logo">
          <div className="nav1-logo-mark">
            <Building2 size={16} />
          </div>
          <span className="nav1-logo-name">HRM AI OPS</span>
        </div>

        <div className="nav1-items">
          {navMain.map(n => (
            <button key={n.id} className={`nav1-item${tab === n.id ? ' active' : ''}`} onClick={() => setTab(n.id)}>
              {n.icon}
              <span>{n.label}</span>
              {n.id === 'issues' && openIssues > 0 && <span className="badge-nav">{openIssues}</span>}
              {n.id === 'ai'     && pendingAi > 0  && <span className="badge-nav">{pendingAi}</span>}
              <ChevronDown size={11} style={{ opacity: .5 }} />
            </button>
          ))}
        </div>

        <div className="nav1-right">
          <select
            className="ctx-sel"
            value={customerId}
            onChange={e => refreshAll(e.target.value, '')}
          >
            {customers.map(c => <option key={c.id} value={c.id}>{c.code} — {c.name}</option>)}
          </select>
          <select
            className="ctx-sel"
            style={{ minWidth: 120 }}
            value={projectId}
            onChange={e => refreshAll(customerId, e.target.value)}
          >
            {projects.map(p => <option key={p.id} value={p.id}>{p.code}</option>)}
          </select>
          <div className="nav1-divider" />
          <button className="theme-toggle" onClick={onToggleDark} title={dark ? 'Chuyển sang Light' : 'Chuyển sang Dark'}>
            {dark ? <Sun size={15} /> : <Moon size={15} />}
          </button>
          <div className="nav1-user" onClick={onLogout} title="Đăng xuất">
            <div className="nav1-avatar">{engineer.name[0].toUpperCase()}</div>
            <span>{engineer.name}</span>
            <LogOut size={12} style={{ opacity: .7 }} />
          </div>
        </div>
      </div>

      {/* ── Nav row 2 ── */}
      <div className="nav2">
        {subNav[tab].map((s, i) => (
          <button key={s} className={`nav2-item${i === 0 ? ' active' : ''}`}>{s}</button>
        ))}
        <div className="nav2-actions">
          <div style={{ fontSize: 12, color: state === 'loading' ? '#60a5fa' : state === 'error' ? '#f87171' : '#6ee7b7', display: 'flex', alignItems: 'center', gap: 5 }}>
            <span style={{ width: 7, height: 7, borderRadius: '50%', background: state === 'loading' ? '#60a5fa' : state === 'error' ? '#f87171' : '#4ade80', display: 'inline-block' }} />
            {state === 'loading' ? 'Đang tải...' : state === 'error' ? 'Lỗi' : message || 'Sẵn sàng'}
          </div>
          <button className="btn-top btn-top-solid" onClick={() => refreshAll()}>
            <RefreshCw size={12} /> Làm mới
          </button>
          <button className="btn-top btn-top-outline">
            <Bell size={12} /> Thông báo
          </button>
        </div>
      </div>

      {/* ── Page header ── */}
      <div className="page-hdr">
        <div className="page-hdr-left">
          <h2>{title}</h2>
          <p>{subtitle} · <strong>{selCustomer?.name ?? '—'}</strong> / {selProject?.name ?? '—'}</p>
        </div>
      </div>

      {/* ── Content ── */}
      <div className="ws-content">
        {tab === 'dashboard' && (
          <DashboardTab
            engineer={engineer}
            selCustomer={selCustomer} selProject={selProject}
            requirements={requirements} urs={urs} blueprints={blueprints}
            configs={configs} issues={issues} applyRuns={applyRuns}
            readinessReports={readinessReports} aiProposals={aiProposals}
            audits={audits} modules={modules}
            customers={customers} projects={projects}
            customerId={customerId} projectId={projectId}
            openIssues={openIssues} pendingAi={pendingAi} approvedDocs={approvedDocs}
            submit={submit}
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
            issues={issues} fixProposals={fixProposals} testPlans={testPlans}
            releaseDrafts={releaseDrafts} configs={configs}
            customerId={customerId} projectId={projectId} submit={submit}
          />
        )}
        {tab === 'ai' && (
          <AiTab
            aiProposals={aiProposals} aiRuns={aiRuns} promptTemplates={promptTemplates}
            customerId={customerId} projectId={projectId} submit={submit}
          />
        )}
        {tab === 'apply' && (
          <ApplyTab
            applyRuns={applyRuns} snapshots={snapshots} snapshotDiffs={snapshotDiffs}
            regressionRuns={regressionRuns} readinessReports={readinessReports}
            environments={environments} connectors={connectors} fixProposals={fixProposals}
            customerId={customerId} projectId={projectId} submit={submit}
          />
        )}
        {tab === 'audit' && (
          <AuditTab audits={audits} signOffs={signOffs} traceChains={traceChains} />
        )}
      </div>
    </div>
  );
}

// ── Dashboard Tab ─────────────────────────────────────────────────
function DashboardTab({ engineer, selCustomer, selProject, requirements, urs, blueprints, configs, issues, applyRuns, readinessReports, aiProposals, audits, modules, customers, projects, customerId, projectId, openIssues, pendingAi, approvedDocs, submit }: any) {
  const totalDocs = requirements.length + urs.length + blueprints.length + configs.length;
  const closedIssues = issues.filter((i: Issue) => i.status === 'Closed').length;

  return (
    <>
      {/* Welcome + summary */}
      <div className="welcome-row">
        <div className="welcome-card">
          <div className="welcome-icon">
            <Building2 size={36} color="rgba(255,255,255,.9)" />
          </div>
          <div className="welcome-text">
            <h3>Xin chào {engineer.name}.</h3>
            <p>
              Bạn đang xem dữ liệu tổng quan về dự án{' '}
              <strong>{selProject?.name ?? '...'}</strong> tại{' '}
              <strong>{selCustomer?.name ?? '...'}</strong>.
            </p>
          </div>
        </div>
        <div className="summary-card">
          <h4>Tổng số tài liệu</h4>
          <div className="summary-big">{totalDocs}</div>
          <div className="reminder-list">
            {[
              { label: 'Requirements', val: requirements.length, color: '#22c55e' },
              { label: 'URS Documents', val: urs.length, color: '#3b82f6' },
              { label: 'Blueprints', val: blueprints.length, color: '#8b5cf6' },
              { label: 'Config Specs', val: configs.length, color: '#f59e0b' },
            ].map(r => (
              <div className="summary-row" key={r.label}>
                <div className="summary-row-dot">
                  <div className="dot" style={{ background: r.color }} />
                  {r.label}
                </div>
                <div className="summary-row-val" style={{ color: r.color }}>{r.val}</div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Stat cards */}
      <div className="stat-grid">
        <StatCard color="green" label="Tài liệu đã duyệt" period="Tất cả" value={approvedDocs}
          trend={approvedDocs > 0 ? `${approvedDocs}/${totalDocs} docs` : '0'} up={true} prev={`Tổng: ${totalDocs}`} />
        <StatCard color="yellow" label="Issues đang mở" period="Hiện tại" value={openIssues}
          trend={openIssues > 0 ? `${openIssues} chưa xử lý` : 'Tốt!'} up={false} prev={`Đã đóng: ${closedIssues}`} />
        <StatCard color="blue" label="AI Proposals" period="Tất cả" value={aiProposals.length}
          trend={`${pendingAi} chờ duyệt`} up={pendingAi === 0} prev={`Đã chấp nhận: ${aiProposals.filter((a: AiProposal) => a.status === 'Accepted').length}`} />
        <StatCard color="purple" label="Apply Runs" period="Tất cả" value={applyRuns.length}
          trend={`${readinessReports.filter((r: ReleaseReadinessReport) => r.status === 'Ready').length} release ready`} up={true} prev="Test/UAT only" />
        <StatCard color="orange" label="AI Runs" period="Tất cả" value={aiProposals.length}
          trend={`Provider: LocalStub`} up={true} prev="Đã mask context" />
        <StatCard color="red" label="Audit Logs" period="Tất cả" value={audits.length}
          trend="Toàn bộ actions" up={true} prev="Realtime tracking" />
      </div>

      {/* Content grid */}
      <div className="g3">
        {/* HRM Modules */}
        <Card title="HRM Modules" sub="Mức độ rủi ro mặc định" icon={<ShieldCheck size={14} />}>
          <div className="reminder-list">
            {modules.map((m: HrmModule) => (
              <div className="reminder-row" key={m.id}>
                <span className="reminder-label">{m.name}</span>
                <span className={`reminder-val ${m.defaultRiskLevel === 'Critical' ? 'danger' : m.defaultRiskLevel === 'High' ? 'warn' : ''}`}>
                  {m.defaultRiskLevel}
                </span>
              </div>
            ))}
            {modules.length === 0 && <div className="empty-msg">Chưa có dữ liệu</div>}
          </div>
        </Card>

        {/* Recent issues */}
        <Card title="Issues gần đây" sub="Top 5 mới nhất" icon={<AlertTriangle size={14} />} link="Xem chi tiết >>">
          <div className="reminder-list">
            {issues.slice(0, 5).map((i: Issue) => (
              <div className="reminder-row" key={i.id}>
                <span className="reminder-label" style={{ fontSize: 12 }}>{i.issueNo} — {truncate(i.title, 30)}</span>
                {issueBadge(i.status)}
              </div>
            ))}
            {issues.length === 0 && <div className="empty-msg">Không có issue</div>}
          </div>
        </Card>

        {/* Reminders / AI pending */}
        <Card title="Nhắc việc" sub="Cần xử lý" icon={<Bell size={14} />} link="Xem chi tiết >>">
          <div className="reminder-list">
            {[
              { label: 'AI chờ duyệt', val: pendingAi, cls: pendingAi > 0 ? 'warn' : '' },
              { label: 'Issues đang mở', val: openIssues, cls: openIssues > 0 ? 'warn' : '' },
              { label: 'Docs chưa ký', val: [...requirements, ...urs, ...blueprints, ...configs].filter((d: any) => d.status === 'Draft').length, cls: '' },
              { label: 'Apply runs pending', val: applyRuns.filter((a: ApplyRun) => a.status === 'DryRunSucceeded').length, cls: '' },
              { label: 'Release ready', val: readinessReports.filter((r: ReleaseReadinessReport) => r.status === 'Ready').length, cls: 'success' },
              { label: 'Audit logs hôm nay', val: audits.length, cls: '' },
            ].map(r => (
              <div className="reminder-row" key={r.label}>
                <span className="reminder-label">{r.label}</span>
                <span className={`reminder-val ${r.cls}`}>{r.val}</span>
              </div>
            ))}
          </div>
        </Card>
      </div>

      {/* Quick create */}
      <div className="g2">
        <Card title="Thêm khách hàng mới" sub="" icon={<Users size={14} />}>
          <SmartForm
            fields={[['code','Code','ACME'],['name','Tên KH','ACME HR Co.'],['industry','Ngành','Manufacturing']]}
            submitLabel="Tạo khách hàng"
            onSubmit={b => submit(() => api.post('/api/customers', b), 'Đã tạo khách hàng')}
          />
          <ItemList empty="Khách hàng:" items={customers.slice(0, 3).map((c: Customer) => ({
            key: c.id, title: c.name, meta: `${c.code} · ${c.industry ?? 'Chưa rõ ngành'}`,
            badge: <span className="badge b-blue">{c.status}</span>
          }))} />
        </Card>
        <Card title="Thêm dự án mới" sub="" icon={<FileText size={14} />}>
          <SmartForm
            disabled={!customerId}
            fields={[['code','Code','HRM-OPS'],['name','Tên dự án','HRM Operations'],['hrmProductName','Sản phẩm HRM','Custom HRM']]}
            submitLabel="Tạo dự án"
            onSubmit={b => submit(() => api.post(`/api/customers/${customerId}/projects`, b), 'Đã tạo dự án')}
          />
          <ItemList empty="Dự án:" items={projects.slice(0, 3).map((p: Project) => ({
            key: p.id, title: p.name, meta: `${p.code} · ${p.hrmProductName ?? ''}`,
            badge: <span className="badge b-green">{p.status}</span>
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
      <div className="g2">
        <Card title="Requirements" sub={`${requirements.length} yêu cầu`} icon={<FileText size={14} />}>
          <form className="f-stack" onSubmit={e => {
            e.preventDefault();
            const fd = new FormData(e.currentTarget);
            submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/requirements/manual`, {
              title: fd.get('title'), contentText: fd.get('contentText'), createdBy: 'demo.user'
            }), 'Đã tạo requirement');
            (e.target as HTMLFormElement).reset();
          }}>
            <input name="title" placeholder="Tiêu đề requirement..." required disabled={!projectId} />
            <textarea name="contentText" placeholder="Mô tả nội dung..." disabled={!projectId} />
            <button className="btn btn-navy" disabled={!projectId}><Plus size={13} /> Thêm Requirement</button>
          </form>
          <ItemList empty="Chưa có requirement" items={requirements.map((r: Requirement) => ({
            key: r.id, title: `${r.requirementNo} v${r.version} — ${r.title}`,
            meta: `${r.status}${r.isLatest ? '' : ' · cũ'}`,
            badge: statusBadge(r.status),
            actions: (
              <div className="item-actions">
                <button className="btn btn-sm" onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/requirements/${r.id}/generate-urs`), 'Đã tạo URS proposal')}>
                  <Bot size={11} /> Gen URS
                </button>
                <button className="btn btn-sm" disabled={r.status === 'Approved'} onClick={() => signOff('requirements', r.id, 'Requirement đã ký')}>
                  <CheckCircle2 size={11} /> Ký
                </button>
              </div>
            )
          }))} />
        </Card>

        <Card title="URS Documents" sub={`${urs.length} tài liệu`} icon={<Bot size={14} />}>
          <ItemList empty="Chưa có URS" items={urs.map((u: UrsDocument) => ({
            key: u.id, title: `${u.ursNo} v${u.version} — ${u.title}`,
            meta: truncate(u.content), badge: statusBadge(u.status),
            actions: (
              <div className="item-actions">
                <button className="btn btn-sm" onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/urs/${u.id}/generate-blueprint`), 'Đã tạo Blueprint proposal')}>
                  <Bot size={11} /> Gen BP
                </button>
                <button className="btn btn-sm" disabled={u.status === 'Approved'} onClick={() => signOff('urs', u.id, 'URS đã ký')}>
                  <CheckCircle2 size={11} /> Ký
                </button>
              </div>
            )
          }))} />
        </Card>
      </div>

      <div className="g2">
        <Card title="Blueprints" sub={`${blueprints.length} blueprint`} icon={<Layers3 size={14} />}>
          <ItemList empty="Chưa có blueprint" items={blueprints.map((b: Blueprint) => ({
            key: b.id, title: `${b.blueprintNo} v${b.version} — ${b.type}`,
            meta: truncate(b.content), badge: statusBadge(b.status),
            actions: (
              <div className="item-actions">
                <button className="btn btn-sm" onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/blueprints/${b.id}/generate-config-spec`, { moduleName: 'Leave Management' }), 'Đã tạo Config Spec proposal')}>
                  <Bot size={11} /> Gen CFG
                </button>
                <button className="btn btn-sm" disabled={b.status === 'Approved'} onClick={() => signOff('blueprints', b.id, 'Blueprint đã ký')}>
                  <CheckCircle2 size={11} /> Ký
                </button>
              </div>
            )
          }))} />
        </Card>

        <Card title="Config Specifications" sub={`${configs.length} config`} icon={<ShieldCheck size={14} />}>
          <ItemList empty="Chưa có config spec" items={configs.map((c: ConfigSpec) => ({
            key: c.id, title: `${c.configNo} v${c.version} — ${c.moduleName}`,
            meta: truncate(c.content), badge: riskBadge(c.riskLevel),
            actions: (
              <div className="item-actions">
                {statusBadge(c.status)}
                <button className="btn btn-sm" disabled={c.status === 'Approved'} onClick={() => signOff('config-specs', c.id, 'Config spec đã ký')}>
                  <CheckCircle2 size={11} /> Ký
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
      <div className="g2">
        <Card title="Tạo Issue mới" sub="Báo cáo sự cố vận hành" icon={<AlertTriangle size={14} />}>
          <form className="f-stack" onSubmit={e => {
            e.preventDefault();
            const fd = new FormData(e.currentTarget);
            const lc = String(fd.get('linkedConfigId') ?? '');
            submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues`, {
              environmentId: null,
              linkedEntityType: lc ? 'ConfigSpec' : null, linkedEntityId: lc || null,
              title: fd.get('title'), description: fd.get('description'),
              category: fd.get('category') || null, severity: fd.get('severity'),
              priority: fd.get('priority'), reportedBy: fd.get('reportedBy') || 'customer.hr'
            }), 'Đã tạo issue');
            (e.target as HTMLFormElement).reset();
          }}>
            <input name="title" placeholder="Tiêu đề issue..." required disabled={!projectId} />
            <textarea name="description" placeholder="Mô tả chi tiết sự cố..." disabled={!projectId} />
            <div className="f-row3">
              <select name="severity" defaultValue="High" disabled={!projectId}>
                <option>Low</option><option>Medium</option><option>High</option><option>Critical</option>
              </select>
              <select name="priority" defaultValue="P2" disabled={!projectId}>
                <option value="P0">P0 — Critical</option><option value="P1">P1 — High</option>
                <option value="P2">P2 — Medium</option><option value="P3">P3 — Low</option>
                <option value="P4">P4 — Minimal</option>
              </select>
              <select name="category" defaultValue="" disabled={!projectId}>
                <option value="">Tự phân loại</option>
                <option>Functional</option><option>Configuration</option><option>Data</option>
                <option>Integration</option><option>Security</option><option>Permission</option>
                <option>Payroll</option><option>Performance</option><option>Other</option>
              </select>
            </div>
            <div className="f-row">
              <input name="reportedBy" placeholder="Người báo cáo" defaultValue="customer.hr" disabled={!projectId} />
              <select name="linkedConfigId" defaultValue="" disabled={!projectId}>
                <option value="">Không liên kết</option>
                {configs.map((c: ConfigSpec) => <option key={c.id} value={c.id}>{c.configNo} — {c.moduleName}</option>)}
              </select>
            </div>
            <button className="btn btn-navy" disabled={!projectId}><Plus size={13} /> Tạo Issue</button>
          </form>
        </Card>

        <Card title="Danh sách Issues" sub={`${issues.length} issue`} icon={<AlertTriangle size={14} />}>
          <ItemList empty="Không có issue nào" items={issues.map((i: Issue) => ({
            key: i.id,
            title: `${i.issueNo} — ${i.title}`,
            meta: `${i.category} · ${i.severity} · ${i.priority} · ${truncate(i.rootCauseSummary ?? i.description, 60)}`,
            badge: issueBadge(i.status),
            actions: (
              <div className="item-actions">
                <button className="btn btn-sm" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/classify`), 'Classify xong')}><Bot size={10} /> Classify</button>
                <button className="btn btn-sm" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/root-cause`), 'RCA xong')}>RCA</button>
                <button className="btn btn-sm" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/fix-proposal`), 'Fix proposal xong')}>Fix</button>
                <button className="btn btn-sm" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/change-request-draft`), 'CR xong')}>CR</button>
                <button className="btn btn-sm" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/regression-test-plan`), 'Test plan xong')}>Test</button>
                <button className="btn btn-sm btn-danger" disabled={i.status === 'Closed'} onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${i.id}/close`, { closedBy: 'support.lead', resolutionNote: 'Resolved.' }), 'Đã đóng')}>Đóng</button>
              </div>
            )
          }))} />
        </Card>
      </div>

      <div className="g2">
        <Card title="Fix Proposals" sub={`${fixProposals.length} đề xuất`} icon={<Rocket size={14} />}>
          <ItemList empty="Chưa có fix proposal" items={fixProposals.slice(0, 6).map((f: FixProposal) => ({
            key: f.id, title: f.title, meta: truncate(f.proposedSolution), badge: riskBadge(f.riskLevel)
          }))} />
        </Card>
        <Card title="Test Plans & Release Drafts" icon={<GitCompare size={14} />}>
          <ItemList empty="Chưa có test plan" items={testPlans.slice(0, 4).map((t: RegressionTestPlan) => ({
            key: t.id, title: `${t.testPlanNo} — ${t.title}`, meta: truncate(t.content), badge: riskBadge(t.riskLevel)
          }))} />
          <ItemList empty="Chưa có release draft" items={releaseDrafts.slice(0, 4).map((r: ReleaseDraft) => ({
            key: r.id, title: `${r.releaseDraftNo} — ${r.title}`, meta: truncate(r.releaseNotes), badge: riskBadge(r.riskLevel)
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
      <Card title="Đề xuất AI — Chờ duyệt" sub={`${aiProposals.filter((a: AiProposal) => a.status === 'PendingReview').length} pending`} icon={<Bot size={14} />}>
        <ItemList empty="Không có đề xuất nào" items={aiProposals.map((a: AiProposal) => ({
          key: a.id, title: `${a.taskType} — ${a.title}`, meta: truncate(a.proposedContent),
          badge: aiProposalBadge(a.status),
          actions: a.status === 'PendingReview' ? (
            <div className="item-actions">
              <button className="btn btn-sm btn-success" onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/ai-proposals/${a.id}/accept`, { reviewedBy: 'consultant', comment: 'Accepted.' }), 'Đã chấp nhận')}>
                <CheckCircle2 size={11} /> Chấp nhận
              </button>
              <button className="btn btn-sm btn-danger" onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/ai-proposals/${a.id}/reject`, { reviewedBy: 'consultant', comment: 'Rejected.' }), 'Đã từ chối')}>
                Từ chối
              </button>
            </div>
          ) : undefined
        }))} />
      </Card>

      <div className="g2">
        <Card title="Lịch sử AI Runs" sub={`${aiRuns.length} runs`} icon={<Activity size={14} />}>
          <ItemList empty="Chưa có AI run" items={aiRuns.slice(0, 10).map((r: AiRun) => ({
            key: r.id, title: `${r.runType} — ${r.status}`,
            meta: `${r.provider} · ${r.promptTemplateKey ?? 'no-template'} · ${truncate(r.maskedInputPreview ?? r.outputSummary ?? '')}`,
            badge: aiRunBadge(r.status)
          }))} />
        </Card>

        <Card title="Prompt Templates" sub={`${promptTemplates.length} templates`} icon={<Bot size={14} />}>
          <form className="f-stack" onSubmit={e => {
            e.preventDefault();
            const fd = new FormData(e.currentTarget);
            submit(() => api.post('/api/ai/prompt-templates', {
              key: fd.get('key'), name: fd.get('name'), taskType: fd.get('taskType'),
              description: fd.get('description'), systemPrompt: fd.get('systemPrompt'),
              userPromptTemplate: fd.get('userPromptTemplate'),
              outputJsonSchema: '{"type":"object","required":["title","content"],"properties":{"title":{"type":"string"},"content":{"type":"string"}}}',
              createdBy: 'platform.admin'
            }), 'Đã tạo prompt template');
            (e.target as HTMLFormElement).reset();
          }}>
            <div className="f-row">
              <input name="key" placeholder="template-key" required />
              <input name="name" placeholder="Tên template" required />
            </div>
            <select name="taskType" defaultValue="GenerateUrs">
              <option>GenerateUrs</option><option>GenerateBlueprint</option><option>GenerateConfigSpec</option>
              <option>ClassifyIssue</option><option>AnalyzeRootCause</option><option>GenerateFixProposal</option>
              <option>GenerateChangeRequest</option><option>GenerateRegressionTestPlan</option>
              <option>GenerateReleaseDraft</option><option>GenerateKnowledgeUpdate</option>
            </select>
            <input name="description" placeholder="Mô tả" required />
            <textarea name="systemPrompt" placeholder="System prompt..." required />
            <textarea name="userPromptTemplate" placeholder="User prompt template..." required />
            <button className="btn btn-primary"><Plus size={13} /> Tạo Template</button>
          </form>
          <ItemList empty="Chưa có template" items={promptTemplates.map((t: PromptTemplate) => ({
            key: t.template.id, title: `${t.template.key} — ${t.template.taskType}`,
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
      <div className="g2">
        <Card title="Dry Run / Apply" sub="Test/UAT only — không apply production" icon={<Rocket size={14} />}>
          <form className="f-stack" onSubmit={e => {
            e.preventDefault();
            const fd = new FormData(e.currentTarget);
            const cid2 = String(fd.get('connectorId') ?? '');
            submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/controlled-test-apply/dry-run`, {
              environmentId: fd.get('environmentId'), connectorId: cid2 || null,
              sourceType: 'FixProposal', sourceId: fd.get('sourceId'), requestedBy: 'test.operator'
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
            <button className="btn btn-navy" disabled={testEnvs.length === 0 || fixProposals.length === 0}>
              <Rocket size={13} /> Dry Run
            </button>
          </form>
        </Card>

        <Card title="Apply Runs" sub={`${applyRuns.length} runs`} icon={<Rocket size={14} />}>
          <ItemList empty="Chưa có apply run" items={applyRuns.slice(0, 6).map((a: ApplyRun) => ({
            key: a.id, title: `${a.applyRunNo} — ${a.sourceType}`,
            meta: `${a.riskLevel} · ${truncate(a.summary || a.rollbackRecommendation || '')}`,
            badge: applyBadge(a.status),
            actions: (
              <button className="btn btn-sm btn-primary"
                disabled={a.status !== 'DryRunSucceeded' && a.status !== 'ApprovalRequired'}
                onClick={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/apply-runs/${a.id}/execute`, { requestedBy: 'test.operator' }), 'Apply đã thực hiện')}>
                <Rocket size={11} /> Execute
              </button>
            )
          }))} />
        </Card>
      </div>

      <div className="g2">
        <Card title="Regression & Readiness" icon={<GitCompare size={14} />}>
          <ItemList empty="Chưa có readiness report" items={readinessReports.slice(0, 4).map((r: ReleaseReadinessReport) => ({
            key: r.id, title: `${r.reportNo} — ${r.status}`, meta: truncate(r.summary || r.blockers),
            badge: r.status === 'Ready' ? <span className="badge b-green">Ready</span> : <span className="badge b-red">Not Ready</span>
          }))} />
          <ItemList empty="Chưa có regression run" items={regressionRuns.slice(0, 4).map((r: RegressionTestRun) => ({
            key: r.id, title: `${r.runNo} — ${r.status}`,
            meta: `Pass: ${r.passedTests}/${r.totalTests} · ${truncate(r.summary)}`,
            badge: r.status === 'Passed' ? <span className="badge b-green">Passed</span> : <span className="badge b-red">{r.status}</span>
          }))} />
        </Card>
        <Card title="Snapshots & Diffs" icon={<GitCompare size={14} />}>
          <ItemList empty="Chưa có snapshot" items={snapshots.slice(0, 4).map((s: EnvironmentSnapshot) => ({
            key: s.id, title: `${s.snapshotNo} — ${s.stage}`, meta: truncate(s.maskedSummary),
            badge: <span className="badge b-default">{s.kind}</span>
          }))} />
          <ItemList empty="Chưa có diff" items={snapshotDiffs.slice(0, 4).map((d: SnapshotDiff) => ({
            key: d.id, title: `${d.snapshotKind} diff`, meta: truncate(d.diffSummary), badge: riskBadge(d.riskLevel)
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
      <Card title="Traceability — REQ → URS → Blueprint → Config" icon={<Shield size={14} />}>
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
              <div className={`trace-node ${c.configSpec ? 'approved' : 'gap'}`}>
                <span className="tn-lbl">Config</span>
                <strong className="tn-text">{c.configSpec ? `${c.configSpec.configNo}` : 'Chưa có'}</strong>
                <span className="tn-stat">{c.configSpec ? `${c.configSpec.riskLevel} · ${c.configSpec.status}` : 'Gap'}</span>
              </div>
            </div>
          ))}
        </div>
      </Card>

      <div className="g2">
        <Card title="Sign-offs" sub={`${signOffs.length} lần ký`} icon={<CheckCircle2 size={14} />}>
          <ItemList empty="Chưa có sign-off" items={signOffs.slice(0, 8).map((s: DocumentSignOff) => ({
            key: s.id, title: `${s.documentKind} v${s.version}`,
            meta: `${s.signedOffBy} · ${s.role ?? 'Reviewer'} · ${new Date(s.signedOffAt).toLocaleString('vi-VN')}`
          }))} />
        </Card>
        <Card title="Audit Logs" sub={`${audits.length} bản ghi`} icon={<Shield size={14} />}>
          <ItemList empty="Chưa có audit log" items={audits.slice(0, 10).map((a: AuditLog) => ({
            key: a.id, title: a.action, meta: `${a.entityType} · ${new Date(a.createdAt).toLocaleString('vi-VN')}`
          }))} />
        </Card>
      </div>
    </>
  );
}

// ── Reusable components ───────────────────────────────────────────
function StatCard({ color, label, period, value, trend, up, prev }: any) {
  return (
    <div className={`stat-card ${color}`}>
      <div className="stat-top">
        <span className="stat-label">{label}</span>
        <span className="stat-period">{period}</span>
      </div>
      <div className="stat-value">{value}</div>
      <div className={`stat-trend ${up ? 'up' : 'down'}`}>{trend}</div>
      <div className="stat-prev">{prev}</div>
    </div>
  );
}

function Card({ title, sub, icon, link, children }: { title: string; sub?: string; icon?: React.ReactNode; link?: string; children: React.ReactNode }) {
  return (
    <div className="card">
      <div className="card-header">
        <div className="card-header-left">
          {icon}
          <div>
            <h3>{title}</h3>
            {sub && <p>{sub}</p>}
          </div>
        </div>
        {link && <span className="card-link">{link}</span>}
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
    <form style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'flex-end' }}
      onSubmit={(e: FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        const fd = new FormData(e.currentTarget);
        onSubmit(Object.fromEntries(fields.map(([k]) => [k, fd.get(k)])));
        e.currentTarget.reset();
      }}>
      {fields.map(([k, , ph]) => (
        <input key={k} name={k} placeholder={ph} disabled={disabled} style={{ flex: '1 1 120px', minWidth: 100 }}
          required={k === 'code' || k === 'name'} />
      ))}
      <button disabled={disabled} className="btn btn-primary" style={{ whiteSpace: 'nowrap' }}>
        <Plus size={13} /> {submitLabel}
      </button>
    </form>
  );
}

// ── Helpers ───────────────────────────────────────────────────────
function statusBadge(s: string) {
  return <span className={`badge ${s === 'Approved' ? 'b-green' : s === 'Draft' ? 'b-default' : s === 'Archived' ? 'b-red' : 'b-yellow'}`}>{s}</span>;
}
function riskBadge(r: string) {
  return <span className={`badge ${r === 'Critical' ? 'b-red' : r === 'High' ? 'b-yellow' : r === 'Low' ? 'b-green' : 'b-blue'}`}>{r}</span>;
}
function issueBadge(s: string) {
  return <span className={`badge ${s === 'Closed' || s === 'Resolved' ? 'b-green' : s === 'InProgress' ? 'b-blue' : s === 'Cancelled' ? 'b-default' : 'b-yellow'}`}>{s}</span>;
}
function aiProposalBadge(s: string) {
  return <span className={`badge ${s === 'Accepted' ? 'b-green' : s === 'PendingReview' ? 'b-yellow' : s === 'Rejected' || s === 'FailedValidation' ? 'b-red' : 'b-default'}`}>{s}</span>;
}
function aiRunBadge(s: string) {
  return <span className={`badge ${s === 'Completed' ? 'b-green' : s === 'Running' || s === 'Queued' ? 'b-blue' : s === 'Failed' ? 'b-red' : 'b-default'}`}>{s}</span>;
}
function applyBadge(s: string) {
  return <span className={`badge ${s === 'Applied' || s === 'ReleaseReady' ? 'b-green' : s === 'Applying' || s === 'DryRunSucceeded' ? 'b-blue' : s.includes('Failed') ? 'b-red' : 'b-yellow'}`}>{s}</span>;
}
function truncate(v: string, max = 100) {
  if (!v) return '';
  return v.length > max ? v.slice(0, max) + '…' : v;
}
