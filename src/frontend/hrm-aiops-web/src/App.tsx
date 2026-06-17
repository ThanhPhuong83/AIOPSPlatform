import {
  Activity, AlertTriangle, Bot, Building2, CheckCircle2, ChevronDown,
  FileText, GitCompare, Key, Layers3, LogOut, Moon, Plus, RefreshCw,
  Rocket, Shield, ShieldCheck, Sun, Trash2, UserCheck, UserCog,
  UserMinus, UserPlus, Users, Lock, Bell, LayoutGrid, ExternalLink
} from 'lucide-react';
import { MODULE_LINKS } from './modules';
import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  api,
  AuditLog, AiProposal, AiRun, ApplyRun, Blueprint, ConfigSpec,
  Customer, CustomerConnector, DocumentSignOff, Environment,
  EnvironmentSnapshot, FixProposal, HrmModule, Issue, PromptTemplate,
  Project, Requirement, RegressionTestPlan, RegressionTestRun,
  ReleaseDraft, ReleaseReadinessReport, SnapshotDiff, TraceChain, UrsDocument
} from './api';
import { Lang, LANGS, makeT } from './i18n';
import { getSession, saveSession, clearSession } from './session';

type LoadState = 'idle' | 'loading' | 'error';
type Tab = 'dashboard' | 'documents' | 'issues' | 'ai' | 'apply' | 'audit' | 'users';
type Engineer = { userId: string; name: string };

// ── Root ──────────────────────────────────────────────────────────
export function App() {
  const [engineer, setEngineer] = useState<Engineer | null>(() => getSession());
  const [dark, setDark]   = useState(() => localStorage.getItem('theme') === 'dark');
  const [lang, setLang]   = useState<Lang>(() => (localStorage.getItem('lang') as Lang) || 'vi');

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
    localStorage.setItem('theme', dark ? 'dark' : 'light');
  }, [dark]);

  useEffect(() => { localStorage.setItem('lang', lang); }, [lang]);

  const t = useMemo(() => makeT(lang), [lang]);

  function login(e: Engineer) { saveSession(e); setEngineer(e); }
  function logout() { clearSession(); setEngineer(null); }

  if (!engineer)
    return <LoginPage onLogin={login} dark={dark} onToggleDark={() => setDark(d => !d)} lang={lang} setLang={setLang} t={t} />;
  return <Workspace engineer={engineer} onLogout={logout} dark={dark} onToggleDark={() => setDark(d => !d)} lang={lang} setLang={setLang} t={t} />;
}

// ── Login ─────────────────────────────────────────────────────────
function LoginPage({ onLogin, dark, onToggleDark, lang, setLang, t }: {
  onLogin: (e: Engineer) => void; dark: boolean; onToggleDark: () => void;
  lang: Lang; setLang: (l: Lang) => void; t: (k: string) => string;
}) {
  const [err, setErr] = useState('');
  function handle(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    const name = String(fd.get('name') ?? '').trim();
    const pass = String(fd.get('password') ?? '').trim();
    if (!name || !pass) { setErr(t('login.error')); return; }
    const userId = name.toLowerCase().replace(/\s+/g, '.');
    onLogin({ userId, name });
  }
  return (
    <div className="login-bg">
      <div className="login-card">
        {/* Language picker */}
        <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
          {LANGS.map(l => (
            <button key={l.code} onClick={() => setLang(l.code)}
              style={{ padding: '4px 10px', borderRadius: 20, fontSize: 12, fontWeight: 600,
                border: `1px solid ${lang === l.code ? 'var(--primary)' : 'var(--border-2)'}`,
                background: lang === l.code ? 'var(--primary)' : 'transparent',
                color: lang === l.code ? '#fff' : 'var(--text-2)', cursor: 'pointer' }}>
              {l.flag} {l.code.toUpperCase()}
            </button>
          ))}
        </div>

        <div className="login-logo">
          <div className="login-logo-mark">
            <Building2 size={26} color="#fff" />
          </div>
          <div>
            <h1>{t('login.title')}</h1>
            <p>{t('login.subtitle')}</p>
          </div>
        </div>
        <div className="login-heading">
          <h2>{t('login.heading')}</h2>
          <p>{t('login.subheading')}</p>
        </div>
        <form className="login-form" onSubmit={handle}>
          <div>
            <label className="f-label">{t('login.name')}</label>
            <input name="name" placeholder={t('login.name.ph')} autoFocus />
          </div>
          <div>
            <label className="f-label">{t('login.password')}</label>
            <input name="password" type="password" placeholder={t('login.password.ph')} />
          </div>
          {err && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{err}</p>}
          <button type="submit" className="login-btn"><Lock size={15} /> {t('login.btn')}</button>
        </form>
        <p className="login-note">{t('login.footer')}</p>
        <button className="theme-toggle-login" onClick={onToggleDark}>
          {dark ? <Sun size={16} /> : <Moon size={16} />}
          {dark ? t('login.light') : t('login.dark')}
        </button>
      </div>
    </div>
  );
}

// ── Workspace ─────────────────────────────────────────────────────
function Workspace({ engineer, onLogout, dark, onToggleDark, lang, setLang, t }: {
  engineer: Engineer; onLogout: () => void; dark: boolean; onToggleDark: () => void;
  lang: Lang; setLang: (l: Lang) => void; t: (k: string) => string;
}) {
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
    { id: 'dashboard', label: t('nav.dashboard'), icon: <Activity size={13} /> },
    { id: 'documents', label: t('nav.documents'), icon: <FileText size={13} /> },
    { id: 'issues',    label: t('nav.issues'),    icon: <AlertTriangle size={13} /> },
    { id: 'ai',        label: t('nav.ai'),         icon: <Bot size={13} /> },
    { id: 'apply',     label: t('nav.apply'),      icon: <Rocket size={13} /> },
    { id: 'audit',     label: t('nav.audit'),      icon: <Shield size={13} /> },
    { id: 'users',     label: t('nav.users'),      icon: <UserCog size={13} /> },
  ];

  const subNav: Record<Tab, string[]> = {
    dashboard: [0,1,2].map(i => t(`subnav.dashboard.${i}`)),
    documents: [0,1,2,3].map(i => t(`subnav.documents.${i}`)),
    issues:    [0,1,2,3].map(i => t(`subnav.issues.${i}`)),
    ai:        [0,1,2].map(i => t(`subnav.ai.${i}`)),
    apply:     [0,1,2,3].map(i => t(`subnav.apply.${i}`)),
    audit:     [0,1,2].map(i => t(`subnav.audit.${i}`)),
    users:     [t('users.access'), t('users.roles.title')],
  };

  const pageTitles: Record<Tab, [string, string]> = {
    dashboard: [t('page.dashboard.title'), `${t('page.dashboard.sub')} ${new Date().toLocaleDateString()}`],
    documents: [t('page.documents.title'), t('page.documents.sub')],
    issues:    [t('page.issues.title'),    t('page.issues.sub')],
    ai:        [t('page.ai.title'),        t('page.ai.sub')],
    apply:     [t('page.apply.title'),     t('page.apply.sub')],
    audit:     [t('page.audit.title'),     t('page.audit.sub')],
    users:     [t('page.users.title'),     t('page.users.sub')],
  };

  const [title, subtitle] = pageTitles[tab];

  return (
    <div className="ws-shell">
      {/* ── Topbar (horizontal) ── */}
      <div className="ws-topbar">
        <div className="ws-topbar-left">
          <span className="ctx-lbl">{t('common.customer')}</span>
          <select className="ctx-sel" value={customerId} onChange={e => refreshAll(e.target.value, '')}>
            {customers.map(c => <option key={c.id} value={c.id}>{c.code} — {c.name}</option>)}
          </select>
          <span className="ctx-lbl">{t('common.project')}</span>
          <select className="ctx-sel" value={projectId} onChange={e => refreshAll(customerId, e.target.value)}>
            {projects.map(p => <option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}
          </select>
        </div>

        <div className="ws-topbar-right">
          {/* Status */}
          <div className={`top-pill ${state}`}>
            <span style={{ width:6,height:6,borderRadius:'50%',background:state==='loading'?'#60a5fa':state==='error'?'#f87171':'#4ade80',display:'inline-block',marginRight:5 }} />
            {state==='loading' ? t('top.loading') : state==='error' ? t('top.error') : message || t('top.ready')}
          </div>

          <button className="btn-top btn-top-solid" onClick={() => refreshAll()}>
            <RefreshCw size={11} /> {t('top.refresh')}
          </button>

          <div className="top-divider" />

          {/* Language selector */}
          {LANGS.map(l => (
            <button key={l.code} onClick={() => setLang(l.code)}
              style={{ padding:'3px 8px', borderRadius:4, cursor:'pointer', fontSize:11,
                fontWeight: lang===l.code ? 700 : 400, border:'none',
                background: lang===l.code ? 'rgba(255,255,255,.25)' : 'rgba(255,255,255,.08)',
                color: lang===l.code ? '#fff' : 'rgba(255,255,255,.55)' }}>
              {l.flag} {l.code.toUpperCase()}
            </button>
          ))}

          <div className="top-divider" />

          {/* Theme toggle */}
          <button className="theme-toggle" onClick={onToggleDark} title={dark ? t('top.light') : t('top.dark')}>
            {dark ? <Sun size={14} /> : <Moon size={14} />}
          </button>

          {/* User */}
          <div className="nav1-user" onClick={onLogout} title="Logout">
            <div className="nav1-avatar">{engineer.name[0].toUpperCase()}</div>
            <span style={{ maxWidth:100, overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap' }}>{engineer.name}</span>
            <LogOut size={11} style={{ opacity:.7 }} />
          </div>
        </div>
      </div>

      {/* ── Body: left sidebar + right ── */}
      <div className="ws-body">
        {/* ── Left sidebar (vertical nav) ── */}
        <div className="ws-left">
          {/* Logo */}
          <div className="ws-left-logo">
            <div className="ws-left-logo-mark">
              <Building2 size={18} />
            </div>
            <div>
              <div className="ws-left-logo-name">HRM AI Ops</div>
              <div className="ws-left-logo-sub">v1.0 · Phase 1–17</div>
            </div>
          </div>

          {/* Nav items */}
          <div className="ws-left-section">Menu</div>
          {navMain.map(n => (
            <button key={n.id} className={`ws-nav-item${tab === n.id ? ' active' : ''}`} onClick={() => setTab(n.id)}>
              {n.icon}
              <span style={{ flex:1 }}>{n.label}</span>
              {n.id === 'issues' && openIssues > 0 && <span className="ws-nav-badge">{openIssues}</span>}
              {n.id === 'ai'     && pendingAi  > 0 && <span className="ws-nav-badge">{pendingAi}</span>}
            </button>
          ))}

          {/* Extended modules (Phase 7–17 standalone pages) */}
          <div className="ws-left-section">{t('nav.modules')}</div>
          {MODULE_LINKS.filter(m => m.path !== '/').map(m => (
            <button key={m.path} className="ws-nav-item" onClick={() => { window.location.href = m.path; }}>
              <LayoutGrid size={15} />
              <span style={{ flex:1 }}>{m.label}</span>
              <ExternalLink size={11} style={{ opacity:.4 }} />
            </button>
          ))}

          {/* Context info */}
          <div className="ws-left-section" style={{ marginTop:'auto' }}>{t('common.customer')}</div>
          <div style={{ padding:'4px 16px 4px', fontSize:12, color:'rgba(255,255,255,.6)' }}>
            {selCustomer?.name ?? '—'}
          </div>
          <div style={{ padding:'0 16px 16px', fontSize:11, color:'rgba(255,255,255,.35)' }}>
            {selProject?.name ?? '—'}
          </div>
        </div>

        {/* ── Right: page header + content ── */}
        <div className="ws-right">
          {/* Page header */}
          <div className="page-hdr">
            <div className="page-hdr-left">
              <h2>{title}</h2>
              <p>{subtitle} · <strong>{selCustomer?.name ?? '—'}</strong> / {selProject?.name ?? '—'}</p>
            </div>
            <div className="page-hdr-right">
              <span style={{ fontSize:11, color:'var(--text-3)' }}>{new Date().toLocaleDateString()}</span>
            </div>
          </div>

          {/* Tab content */}
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
                submit={submit} t={t}
              />
            )}
            {tab === 'documents' && (
              <DocumentsTab
                requirements={requirements} urs={urs} blueprints={blueprints} configs={configs}
                customerId={customerId} projectId={projectId}
                submit={submit} signOff={signOff} t={t}
              />
            )}
            {tab === 'issues' && (
              <IssuesTab
                issues={issues} fixProposals={fixProposals} testPlans={testPlans}
                releaseDrafts={releaseDrafts} configs={configs}
                customerId={customerId} projectId={projectId} submit={submit} t={t}
              />
            )}
            {tab === 'ai' && (
              <AiTab
                aiProposals={aiProposals} aiRuns={aiRuns} promptTemplates={promptTemplates}
                customerId={customerId} projectId={projectId} submit={submit} t={t}
              />
            )}
            {tab === 'apply' && (
              <ApplyTab
                applyRuns={applyRuns} snapshots={snapshots} snapshotDiffs={snapshotDiffs}
                regressionRuns={regressionRuns} readinessReports={readinessReports}
                environments={environments} connectors={connectors} fixProposals={fixProposals}
                customerId={customerId} projectId={projectId} submit={submit} t={t}
              />
            )}
            {tab === 'audit' && (
              <AuditTab audits={audits} signOffs={signOffs} traceChains={traceChains} t={t} />
            )}
            {tab === 'users' && (
              <UsersTab customerId={customerId} engineer={engineer} t={t} />
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

// ── Dashboard Tab ─────────────────────────────────────────────────
function DashboardTab({ engineer, selCustomer, selProject, requirements, urs, blueprints, configs, issues, applyRuns, readinessReports, aiProposals, audits, modules, customers, projects, customerId, projectId, openIssues, pendingAi, approvedDocs, submit, t }: any) {
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
              { label: t('doc.req.title'), val: requirements.length, color: '#22c55e' },
              { label: t('doc.urs.title'), val: urs.length, color: '#3b82f6' },
              { label: t('doc.bp.title'), val: blueprints.length, color: '#8b5cf6' },
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
              { label: t('dash.rem.ai'), val: pendingAi, cls: pendingAi > 0 ? 'warn' : '' },
              { label: t('dash.stat.issues'), val: openIssues, cls: openIssues > 0 ? 'warn' : '' },
              { label: t('dash.rem.drafts'), val: [...requirements, ...urs, ...blueprints, ...configs].filter((d: any) => d.status === 'Draft').length, cls: '' },
              { label: t('dash.rem.apply'), val: applyRuns.filter((a: ApplyRun) => a.status === 'DryRunSucceeded').length, cls: '' },
              { label: t('dash.rem.release'), val: readinessReports.filter((r: ReleaseReadinessReport) => r.status === 'Ready').length, cls: 'success' },
              { label: t('dash.rem.audit'), val: audits.length, cls: '' },
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
            fields={[[t('common.code'),'Code','ACME'],[t('common.name'),t('common.name'),'ACME HR Co.'],[t('common.industry'),t('common.industry'),'Manufacturing']]}
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
            fields={[[t('common.code'),'Code','HRM-OPS'],[t('common.name'),t('common.name'),'HRM Operations'],[t('common.hrm'),t('common.hrm'),'Custom HRM']]}
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
function DocumentsTab({ requirements, urs, blueprints, configs, customerId, projectId, submit, signOff, t }: any) {
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
                <button className="btn btn-sm" disabled={r.status === 'Approved'} onClick={() => signOff(t('doc.req.title'), r.id, 'Requirement đã ký')}>
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
                <button className="btn btn-sm" disabled={b.status === 'Approved'} onClick={() => signOff(t('doc.bp.title'), b.id, 'Blueprint đã ký')}>
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
function IssuesTab({ issues, fixProposals, testPlans, releaseDrafts, configs, customerId, projectId, submit, t }: any) {
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
function AiTab({ aiProposals, aiRuns, promptTemplates, customerId, projectId, submit, t }: any) {
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
function ApplyTab({ applyRuns, snapshots, snapshotDiffs, regressionRuns, readinessReports, environments, connectors, fixProposals, customerId, projectId, submit, t }: any) {
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
function AuditTab({ audits, signOffs, traceChains, t }: any) {
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

// ── Users Tab ────────────────────────────────────────────────────
type AccessGrant = { id: string; userId: string; roleKey: string; status: string; grantedBy?: string; expiresAt?: string; createdAt: string };
type SecurityRole = { id: string; roleKey: string; name: string; description: string };
type RolePermission = { roleKey: string; permissionKey: string };
type UserRoleAssign = { userId: string; roleKey: string };

function UsersTab({ customerId, engineer, t }: { customerId: string; engineer: any; t: (k: string) => string }) {
  const [grants, setGrants] = useState<AccessGrant[]>([]);
  const [roles, setRoles] = useState<SecurityRole[]>([]);
  const [rolePerms, setRolePerms] = useState<RolePermission[]>([]);
  const [assignments, setAssignments] = useState<UserRoleAssign[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [msg, setMsg] = useState('');

  async function load() {
    if (!customerId) return;
    setLoading(true); setError('');
    try {
      const [ga, ro] = await Promise.all([
        api.get<AccessGrant[]>(`/api/customers/${customerId}/security/tenant-access`),
        api.get<{ roles: SecurityRole[]; rolePermissions: RolePermission[]; assignments: UserRoleAssign[] }>(`/api/customers/${customerId}/security/roles`)
      ]);
      setGrants(ga);
      setRoles(ro.roles); setRolePerms(ro.rolePermissions); setAssignments(ro.assignments);
    } catch (e: any) {
      const body = typeof e?.message === 'string' ? e.message : '';
      if (body.includes('403') || body.includes('permission')) setError(t('users.no.perm'));
      else setError(body || 'Error loading users');
    }
    setLoading(false);
  }

  useEffect(() => { load(); }, [customerId]);

  async function grantAccess(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    const userId = String(fd.get('userId') ?? '').trim();
    const roleKey = String(fd.get('roleKey') ?? '').trim();
    const expiresAt = String(fd.get('expiresAt') ?? '').trim() || undefined;
    try {
      await api.post(`/api/customers/${customerId}/security/tenant-access`, { userId, roleKey, expiresAt: expiresAt || null });
      setMsg(`✓ Đã cấp quyền cho ${userId}`);
      await load();
      (e.target as HTMLFormElement).reset();
    } catch (err: any) { setMsg(`✗ ${err.message}`); }
  }

  async function revoke(grantId: string, userId: string) {
    try {
      await api.post(`/api/customers/${customerId}/security/tenant-access/${grantId}/revoke`);
      setMsg(`✓ Đã thu hồi quyền của ${userId}`);
      await load();
    } catch (err: any) { setMsg(`✗ ${err.message}`); }
  }

  async function restore(grantId: string, userId: string) {
    try {
      await api.post(`/api/customers/${customerId}/security/tenant-access/${grantId}/restore`);
      setMsg(`✓ Đã khôi phục quyền cho ${userId}`);
      await load();
    } catch (err: any) { setMsg(`✗ ${err.message}`); }
  }

  const statusColor = (s: string) => s === 'Active' ? 'b-green' : s === 'Revoked' ? 'b-red' : 'b-yellow';

  // Group permissions by role
  const rolePermMap: Record<string, string[]> = {};
  rolePerms.forEach(rp => { if (!rolePermMap[rp.roleKey]) rolePermMap[rp.roleKey] = []; rolePermMap[rp.roleKey].push(rp.permissionKey); });

  return (
    <>
      {/* Hint banner */}
      <div style={{ background:'var(--primary-bg)', border:'1px solid var(--primary-bd)', borderRadius:'var(--r-sm)', padding:'10px 14px', fontSize:12, color:'var(--primary)', display:'flex', alignItems:'center', gap:8 }}>
        <Key size={14} /> {t('users.hint')}
      </div>

      {msg && (
        <div style={{ background: msg.startsWith('✓') ? 'var(--success-bg)' : 'var(--danger-bg)', border: `1px solid ${msg.startsWith('✓') ? 'var(--success-bd)' : 'var(--danger-bd)'}`, borderRadius:'var(--r-sm)', padding:'8px 14px', fontSize:12, color: msg.startsWith('✓') ? 'var(--success)' : 'var(--danger)' }}>
          {msg}
        </div>
      )}

      <div className="g2">
        {/* Access grants table */}
        <div className="card" style={{ gridColumn: '1 / -1' }}>
          <div className="card-header">
            <div className="card-header-left">
              <Users size={14} />
              <div>
                <h3>{t('users.access')}</h3>
                <p>{t('users.access.sub')}</p>
              </div>
            </div>
            <span className="card-count">{grants.length}</span>
          </div>
          <div className="card-body" style={{ padding: 0 }}>
            {loading && <div style={{ padding:20, textAlign:'center', color:'var(--text-3)', fontSize:13 }}>Loading...</div>}
            {error && <div style={{ padding:16, color:'var(--warn)', fontSize:12 }}>{error}</div>}
            {!loading && !error && (
              <table style={{ width:'100%', borderCollapse:'collapse', fontSize:13 }}>
                <thead>
                  <tr style={{ background:'var(--bg)', borderBottom:'1px solid var(--border)' }}>
                    {[t('users.userid'), t('users.rolekey'), t('users.status'), t('users.grantedby'), t('users.expires'), t('users.actions')].map(h => (
                      <th key={h} style={{ padding:'9px 14px', textAlign:'left', fontWeight:600, fontSize:11, color:'var(--text-3)', textTransform:'uppercase', letterSpacing:'.05em' }}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {grants.length === 0 && (
                    <tr><td colSpan={6} style={{ padding:24, textAlign:'center', color:'var(--text-3)' }}>{t('users.empty')}</td></tr>
                  )}
                  {grants.map(g => (
                    <tr key={g.id} style={{ borderBottom:'1px solid var(--border)' }}
                      onMouseEnter={e => (e.currentTarget.style.background = 'var(--bg)')}
                      onMouseLeave={e => (e.currentTarget.style.background = '')}>
                      <td style={{ padding:'10px 14px' }}>
                        <div style={{ display:'flex', alignItems:'center', gap:8 }}>
                          <div style={{ width:28, height:28, borderRadius:'50%', background:'var(--primary)', color:'#fff', fontSize:11, fontWeight:700, display:'grid', placeItems:'center', flexShrink:0 }}>
                            {g.userId[0]?.toUpperCase()}
                          </div>
                          <div>
                            <div style={{ fontWeight:600 }}>{g.userId}</div>
                            {g.userId === engineer.userId && <div style={{ fontSize:10, color:'var(--primary)' }}>● You</div>}
                          </div>
                        </div>
                      </td>
                      <td style={{ padding:'10px 14px' }}>
                        <span className="badge b-blue">{g.roleKey}</span>
                      </td>
                      <td style={{ padding:'10px 14px' }}>
                        <span className={`badge ${statusColor(g.status)}`}>{g.status}</span>
                      </td>
                      <td style={{ padding:'10px 14px', color:'var(--text-3)', fontSize:12 }}>{g.grantedBy ?? '—'}</td>
                      <td style={{ padding:'10px 14px', color:'var(--text-3)', fontSize:12 }}>
                        {g.expiresAt ? new Date(g.expiresAt).toLocaleDateString() : '∞'}
                      </td>
                      <td style={{ padding:'10px 14px' }}>
                        <div style={{ display:'flex', gap:6 }}>
                          {g.status !== 'Revoked' ? (
                            <button className="btn btn-sm btn-danger" onClick={() => revoke(g.id, g.userId)}>
                              <UserMinus size={11} /> {t('users.revoke')}
                            </button>
                          ) : (
                            <button className="btn btn-sm btn-success" onClick={() => restore(g.id, g.userId)}>
                              <UserCheck size={11} /> {t('users.restore')}
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      </div>

      <div className="g2">
        {/* Grant new access form */}
        <Card title={t('users.grant.title')} icon={<UserPlus size={14} />}>
          <form className="f-stack" onSubmit={grantAccess}>
            <div className="f-field">
              <label>{t('users.userid')}</label>
              <input name="userId" placeholder={t('users.userid.ph')} required />
            </div>
            <div className="f-field">
              <label>{t('users.rolekey')}</label>
              <select name="roleKey" defaultValue="consultant">
                <option value="consultant">consultant</option>
                <option value="security.admin">security.admin</option>
                <option value="platform.admin">platform.admin</option>
                <option value="support.lead">support.lead</option>
                <option value="release.manager">release.manager</option>
                <option value="viewer">viewer</option>
                {roles.map(r => <option key={r.id} value={r.roleKey}>{r.roleKey} — {r.name}</option>)}
              </select>
            </div>
            <div className="f-field">
              <label>{t('users.expires')}</label>
              <input name="expiresAt" type="datetime-local" placeholder={t('users.expires.ph')} />
            </div>
            <button className="btn btn-navy"><UserPlus size={13} /> {t('users.grant.btn')}</button>
          </form>

          {/* Current user info */}
          <div style={{ marginTop:8, padding:'10px 12px', background:'var(--bg)', borderRadius:'var(--r-sm)', border:'1px solid var(--border)', fontSize:12 }}>
            <div style={{ fontWeight:600, marginBottom:4, color:'var(--text-2)' }}>Đang đăng nhập</div>
            <div style={{ display:'flex', alignItems:'center', gap:8 }}>
              <div style={{ width:24, height:24, borderRadius:'50%', background:'var(--primary)', color:'#fff', fontSize:10, fontWeight:700, display:'grid', placeItems:'center' }}>
                {engineer.name[0].toUpperCase()}
              </div>
              <div>
                <div style={{ fontWeight:600 }}>{engineer.name}</div>
                <div style={{ color:'var(--text-3)' }}>ID: {engineer.userId}</div>
              </div>
            </div>
          </div>
        </Card>

        {/* Roles */}
        <Card title={t('users.roles.title')} icon={<Shield size={14} />} count={roles.length}>
          {roles.length === 0 && <div className="empty-msg">{t('users.roles.empty')}</div>}
          <div className="item-list">
            {roles.map(role => (
              <div key={role.id} className="item">
                <div className="item-body">
                  <span className="item-title">{role.name}</span>
                  <span className="item-meta">
                    {role.roleKey} · {rolePermMap[role.roleKey]?.length ?? 0} permissions
                  </span>
                  <div style={{ display:'flex', flexWrap:'wrap', gap:4, marginTop:4 }}>
                    {(rolePermMap[role.roleKey] ?? []).map(p => (
                      <span key={p} className="badge b-default" style={{ fontSize:10 }}>{p}</span>
                    ))}
                  </div>
                </div>
              </div>
            ))}
          </div>

          {/* User-role assignments */}
          {assignments.length > 0 && (
            <>
              <div style={{ fontSize:12, fontWeight:600, color:'var(--text-2)', marginTop:8 }}>Phân quyền hiện tại</div>
              <div className="item-list">
                {assignments.slice(0, 10).map((a, i) => (
                  <div key={i} className="item">
                    <div className="item-body">
                      <span className="item-title">{a.userId}</span>
                      <span className="item-meta">{a.roleKey}</span>
                    </div>
                    <span className="badge b-blue">{a.roleKey}</span>
                  </div>
                ))}
              </div>
            </>
          )}
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

function Card({ title, sub, icon, link, count, children }: { title: string; sub?: string; icon?: React.ReactNode; link?: string; count?: number; children: React.ReactNode }) {
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
        {count !== undefined && <span className="card-count">{count}</span>}
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


