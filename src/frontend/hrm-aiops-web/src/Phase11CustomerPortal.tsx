import {
  Bell,
  BookOpen,
  Bot,
  CheckCircle2,
  ClipboardCheck,
  FileText,
  FolderKanban,
  Gauge,
  GraduationCap,
  MessageSquare,
  Paperclip,
  ReceiptText,
  Users
} from 'lucide-react';
import type React from 'react';
import { useEffect, useMemo, useState } from 'react';
import { pt } from './phaseI18n';

type LoadState = 'loading' | 'api' | 'mock';

type PortalState = {
  customerId: string;
  projectId: string;
  dashboard: any;
  projects: any[];
  workspace: any;
  requests: any[];
  approvals: any[];
  documents: any[];
  knowledge: any[];
  training: any[];
  notifications: any[];
  users: any[];
  reports: any[];
  billing: any;
  sla: any;
  chat: any;
};

const actor = 'portal.hr.manager';

export function Phase11CustomerPortal() {
  const [route, setRoute] = useState(() => window.location.pathname);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [state, setState] = useState<PortalState>(() => seedPortalState());
  const projectId = state.projectId;

  async function load() {
    try {
      const customers = await api<any[]>('/api/customers');
      const customerId = customers[0]?.id;
      if (!customerId) throw new Error('No customer');
      const dashboard = await api<any>(`/api/portal/customers/${customerId}/dashboard`, customerId);
      const projects = await api<any[]>(`/api/portal/customers/${customerId}/projects`, customerId);
      const resolvedProjectId = projects[0]?.project?.id ?? dashboard.projects?.[0]?.project?.id;
      const [
        workspace,
        requests,
        approvals,
        documents,
        knowledge,
        training,
        notifications,
        users,
        reports,
        billing,
        sla,
        sessions
      ] = await Promise.all([
        api<any>(`/api/portal/customers/${customerId}/projects/${resolvedProjectId}/workspace`, customerId),
        api<any[]>(`/api/portal/customers/${customerId}/requests`, customerId),
        api<any[]>(`/api/portal/customers/${customerId}/approvals`, customerId),
        api<any[]>(`/api/portal/customers/${customerId}/projects/${resolvedProjectId}/documents/shared`, customerId),
        api<any[]>(`/api/portal/customers/${customerId}/knowledge`, customerId),
        api<any[]>(`/api/portal/customers/${customerId}/training`, customerId),
        api<any[]>(`/api/portal/customers/${customerId}/notifications`, customerId),
        api<any[]>(`/api/portal/customers/${customerId}/users`, customerId),
        api<any[]>(`/api/portal/customers/${customerId}/service-reports`, customerId),
        api<any>(`/api/portal/customers/${customerId}/billing/summary`, customerId),
        api<any>(`/api/portal/customers/${customerId}/sla/summary`, customerId),
        api<any[]>(`/api/portal/customers/${customerId}/ai-chat/sessions`, customerId)
      ]);
      const chat = sessions[0]
        ? await api<any>(`/api/portal/customers/${customerId}/ai-chat/sessions/${sessions[0].id}`, customerId)
        : { session: null, messages: [] };
      setState({ customerId, projectId: resolvedProjectId, dashboard, projects, workspace, requests, approvals, documents, knowledge, training, notifications, users, reports, billing, sla, chat });
      setLoadState('api');
    } catch {
      setState(seedPortalState());
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

  async function approveFirst() {
    const approval = state.approvals.find((item) => item.status === 'Pending');
    if (!approval || loadState !== 'api') return;
    await api(`/api/portal/customers/${state.customerId}/approvals/${approval.id}/approve`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({ comment: 'Approved from portal UI.' })
    });
    await load();
  }

  async function markFirstRead() {
    const notification = state.notifications.find((item) => item.status === 'Unread');
    if (!notification || loadState !== 'api') return;
    await api(`/api/portal/customers/${state.customerId}/notifications/${notification.id}/read`, state.customerId, { method: 'POST' });
    await load();
  }

  async function createRequest() {
    if (loadState !== 'api') return;
    await api(`/api/portal/customers/${state.customerId}/projects/${projectId}/requests`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({ requestType: 'Support', title: 'Portal UI sample request', description: 'Created from Phase 11 portal UI.', priority: 'P2' })
    });
    await load();
  }

  async function askAi() {
    if (loadState !== 'api') return;
    let sessionId = state.chat.session?.id;
    if (!sessionId) {
      const session = await api<any>(`/api/portal/customers/${state.customerId}/projects/${projectId}/ai-chat/sessions`, state.customerId, {
        method: 'POST',
        body: JSON.stringify({ title: 'Portal self-service question' })
      });
      sessionId = session.id;
    }
    await api(`/api/portal/customers/${state.customerId}/ai-chat/sessions/${sessionId}/messages`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({ message: 'What customer-visible leave management guidance is available?' })
    });
    await load();
  }

  const title = useMemo(() => titleFor(route), [route]);

  return (
    <main className="phase7-shell">
      <aside className="sidebar phase7-nav">
        <div className="brand">
          <div className="brand-mark">P11</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>{pt('Portal Enhancement')}</span>
          </div>
        </div>
        {navItems.map((item) => (
          <button key={item.route} onClick={() => navigate(item.route)}>
            {item.icon} {pt(item.label)}
          </button>
        ))}
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <p>Phase 11</p>
            <h2>{pt(title)}</h2>
          </div>
          <span className={`status ${loadState === 'api' ? 'ok' : 'idle'}`}>
            {loadState === 'loading' ? pt('Loading API') : loadState === 'api' ? pt('API connected') : pt('Local mock mode')}
          </span>
        </header>

        {route.endsWith('/projects') ? <ProjectsView state={state} /> :
          route.includes('/requests') ? <RequestsView state={state} onCreate={createRequest} /> :
            route.includes('/requirement-intake') ? <RequirementView state={state} /> :
              route.includes('/documents') ? <DocumentsView state={state} /> :
                route.includes('/approvals') ? <ApprovalsView state={state} onApprove={approveFirst} /> :
                  route.includes('/sla') ? <SlaView state={state} /> :
                    route.includes('/knowledge') ? <KnowledgeView state={state} /> :
                      route.includes('/training') ? <TrainingView state={state} /> :
                        route.includes('/ai-chat') ? <AiChatView state={state} onAsk={askAi} /> :
                          route.includes('/notifications') ? <NotificationsView state={state} onRead={markFirstRead} /> :
                            route.includes('/service-reports') ? <ReportsView state={state} /> :
                              route.includes('/billing') ? <BillingView state={state} /> :
                                route.includes('/users') ? <UsersView state={state} /> :
                                  <DashboardView state={state} />}
      </section>
    </main>
  );
}

const navItems = [
  { route: '/customer-portal', label: 'Dashboard', icon: <Gauge size={16} /> },
  { route: '/customer-portal/projects', label: 'Projects', icon: <FolderKanban size={16} /> },
  { route: '/customer-portal/requests', label: 'Requests', icon: <MessageSquare size={16} /> },
  { route: '/customer-portal/requirement-intake', label: 'Requirement', icon: <ClipboardCheck size={16} /> },
  { route: '/customer-portal/documents', label: 'Documents', icon: <FileText size={16} /> },
  { route: '/customer-portal/approvals', label: 'Approvals', icon: <CheckCircle2 size={16} /> },
  { route: '/customer-portal/sla', label: 'SLA', icon: <Gauge size={16} /> },
  { route: '/customer-portal/knowledge', label: 'Knowledge', icon: <BookOpen size={16} /> },
  { route: '/customer-portal/training', label: 'Training', icon: <GraduationCap size={16} /> },
  { route: '/customer-portal/ai-chat', label: 'AI Chat', icon: <Bot size={16} /> },
  { route: '/customer-portal/notifications', label: 'Notifications', icon: <Bell size={16} /> },
  { route: '/customer-portal/service-reports', label: 'Reports', icon: <FileText size={16} /> },
  { route: '/customer-portal/billing', label: 'Billing', icon: <ReceiptText size={16} /> },
  { route: '/customer-portal/users', label: 'Users', icon: <Users size={16} /> }
];

function DashboardView({ state }: { state: PortalState }) {
  const dashboard = state.dashboard;
  return (
    <>
      <section className="metric-grid phase7-metrics">
        <Metric label={pt('Open tickets')} value={dashboard.openTickets} />
        <Metric label={pt('Pending requests')} value={dashboard.pendingRequests} />
        <Metric label={pt('Pending approvals')} value={dashboard.pendingApprovals} />
        <Metric label={pt('Unread notifications')} value={dashboard.unreadNotifications} />
        <Metric label={pt('Shared documents')} value={dashboard.sharedDocuments} />
        <Metric label={pt('Knowledge')} value={dashboard.knowledgeArticles} />
        <Metric label={pt('Training')} value={dashboard.trainingSections} />
        <Metric label={pt('Health')} value={dashboard.customerPortalSummary?.latestServiceReport?.healthScore ?? 0} />
      </section>
      <section className="content-grid two">
        <Panel title={pt('Latest Requests')} icon={<MessageSquare size={18} />}>
          <Rows items={dashboard.latestRequests ?? []} primary="title" secondary="status" />
        </Panel>
        <Panel title={pt('Latest Notifications')} icon={<Bell size={18} />}>
          <Rows items={dashboard.latestNotifications ?? []} primary="title" secondary="status" />
        </Panel>
      </section>
    </>
  );
}

function ProjectsView({ state }: { state: PortalState }) {
  return <Panel title={pt('Project Workspace Access')} icon={<FolderKanban size={18} />}><Rows items={state.projects.map((x) => ({ ...x.project, status: `${x.openRequests} requests / ${x.sharedDocuments} docs / ${x.pendingApprovals} approvals` }))} primary="name" secondary="status" /></Panel>;
}

function RequestsView({ state, onCreate }: { state: PortalState; onCreate: () => void }) {
  return <Panel title={pt('Portal Requests')} icon={<MessageSquare size={18} />} action={<button onClick={onCreate}>{pt('Create Sample')}</button>}><Rows items={state.requests} primary="title" secondary="status" /></Panel>;
}

function RequirementView({ state }: { state: PortalState }) {
  return <Panel title={pt('Requirement Intake')} icon={<ClipboardCheck size={18} />}><Rows items={state.workspace.requirementIntakes ?? []} primary="title" secondary="status" /></Panel>;
}

function DocumentsView({ state }: { state: PortalState }) {
  return <Panel title={pt('Shared Documents')} icon={<FileText size={18} />}><Rows items={state.documents.map((x) => ({ id: x.share.id, title: x.title, status: `${x.share.documentType} v${x.share.documentVersion} / ${x.documentStatus}` }))} primary="title" secondary="status" /></Panel>;
}

function ApprovalsView({ state, onApprove }: { state: PortalState; onApprove: () => void }) {
  return <Panel title={pt('Approval Center')} icon={<CheckCircle2 size={18} />} action={<button onClick={onApprove}>{pt('Approve First')}</button>}><Rows items={state.approvals.map((x) => ({ ...x, title: `${x.approvalType} / ${x.sourceEntityType}` }))} primary="title" secondary="status" /></Panel>;
}

function SlaView({ state }: { state: PortalState }) {
  return <Panel title={pt('SLA Center')} icon={<Gauge size={18} />}><Rows items={state.sla.tickets ?? []} primary="title" secondary="slaStatus" /></Panel>;
}

function KnowledgeView({ state }: { state: PortalState }) {
  return <Panel title={pt('Knowledge Base')} icon={<BookOpen size={18} />}><Rows items={state.knowledge} primary="title" secondary="category" /></Panel>;
}

function TrainingView({ state }: { state: PortalState }) {
  return <Panel title={pt('Training Center')} icon={<GraduationCap size={18} />}><Rows items={state.training} primary="title" secondary="moduleName" /></Panel>;
}

function AiChatView({ state, onAsk }: { state: PortalState; onAsk: () => void }) {
  return <Panel title={pt('Portal AI Chat')} icon={<Bot size={18} />} action={<button onClick={onAsk}>{pt('Ask Sample')}</button>}><Rows items={state.chat.messages ?? []} primary="message" secondary="senderType" /></Panel>;
}

function NotificationsView({ state, onRead }: { state: PortalState; onRead: () => void }) {
  return <Panel title={pt('Notifications')} icon={<Bell size={18} />} action={<button onClick={onRead}>{pt('Mark First Read')}</button>}><Rows items={state.notifications} primary="title" secondary="status" /></Panel>;
}

function ReportsView({ state }: { state: PortalState }) {
  return <Panel title={pt('Service Reports')} icon={<FileText size={18} />}><Rows items={state.reports.map((x) => ({ ...x, title: x.reportNo, status: `Health ${x.healthScore} / SLA breached ${x.slaBreachedCount}` }))} primary="title" secondary="status" /></Panel>;
}

function BillingView({ state }: { state: PortalState }) {
  return <section className="content-grid two"><Panel title={pt('Invoice Drafts')} icon={<ReceiptText size={18} />}><Rows items={state.billing.invoiceDrafts ?? []} primary="invoiceNo" secondary="status" /></Panel><Panel title={pt('Billing Drafts')} icon={<ReceiptText size={18} />}><Rows items={state.billing.billingDrafts ?? []} primary="billingDraftNo" secondary="status" /></Panel></section>;
}

function UsersView({ state }: { state: PortalState }) {
  return <Panel title={pt('Portal User Management')} icon={<Users size={18} />}><Rows items={state.users.map((x) => ({ ...x, status: `${x.roleKey} / billing ${x.canViewBilling ? 'yes' : 'no'} / reports ${x.canViewReports ? 'yes' : 'no'}` }))} primary="displayName" secondary="status" /></Panel>;
}

function Metric({ label, value }: { label: string; value: any }) {
  return <div className="metric"><Gauge size={18} /><span>{label}</span><strong>{value ?? 0}</strong></div>;
}

function Panel({ title, icon, action, children }: { title: string; icon: React.ReactNode; action?: React.ReactNode; children: React.ReactNode }) {
  return <section className="panel"><header>{icon}<h3>{title}</h3>{action}</header>{children}</section>;
}

function Rows({ items, primary, secondary }: { items: any[]; primary: string; secondary: string }) {
  if (!items?.length) return <p className="empty">{pt('No portal data.')}</p>;
  return <div className="action-list">{items.map((item, index) => <div className="list-row" key={item.id ?? index}><div><strong>{item[primary] ?? item.title ?? item.name}</strong><span>{item[secondary] ?? item.status}</span><small>{item.requestNo ?? item.ticketNo ?? item.createdAt ?? ''}</small></div><StatusBadge text={String(item.status ?? item[secondary] ?? 'Active')} /></div>)}</div>;
}

function StatusBadge({ text }: { text: string }) {
  return <span className={`phase7-badge ${text.toLowerCase().replaceAll(' ', '-')}`}>{text}</span>;
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

function titleFor(route: string) {
  const found = navItems.find((item) => route === item.route);
  return found?.label ?? 'Customer Portal';
}

function seedPortalState(): PortalState {
  const request = { id: 'preq-1', requestNo: 'PREQ-00001', title: 'Leave carry-forward visibility', status: 'Submitted', createdAt: new Date().toISOString() };
  const approval = { id: 'appr-1', approvalType: 'UrsSignoff', sourceEntityType: 'UrsDocument', status: 'Pending' };
  const notification = { id: 'notif-1', title: 'URS sign-off required', status: 'Unread', createdAt: new Date().toISOString() };
  return {
    customerId: 'demo',
    projectId: 'project-1',
    dashboard: { openTickets: 1, pendingRequests: 1, pendingApprovals: 1, unreadNotifications: 2, sharedDocuments: 3, knowledgeArticles: 1, trainingSections: 1, latestRequests: [request], latestNotifications: [notification], customerPortalSummary: { latestServiceReport: { healthScore: 98 } } },
    projects: [{ project: { id: 'project-1', name: 'HRM AI Ops Pilot', status: 'Active' }, openRequests: 1, sharedDocuments: 3, pendingApprovals: 1 }],
    workspace: { requirementIntakes: [{ id: 'intake-1', title: 'Leave carry-forward visibility', status: 'InReview' }] },
    requests: [request],
    approvals: [approval],
    documents: [{ share: { id: 'share-1', documentType: 'UrsDocument', documentVersion: 1 }, title: 'Leave Management URS', documentStatus: 'Approved' }],
    knowledge: [{ id: 'kb-1', title: 'Leave approval balance troubleshooting', category: 'Leave Management', status: 'Published' }],
    training: [{ id: 'tr-1', title: 'Leave Management ESS checklist', moduleName: 'Leave Management', status: 'Published' }],
    notifications: [notification],
    users: [{ id: 'user-1', displayName: 'HR Manager Portal', roleKey: 'portal.approver', canViewBilling: true, canViewReports: true, status: 'Active' }],
    reports: [{ id: 'report-1', reportNo: 'CSR-00001', healthScore: 98, slaBreachedCount: 0, status: 'Shared' }],
    billing: { invoiceDrafts: [{ id: 'inv-1', invoiceNo: 'INV-00001', status: 'Draft' }], billingDrafts: [{ id: 'bil-1', billingDraftNo: 'BIL-00001', status: 'Draft' }] },
    sla: { tickets: [{ id: 'ticket-1', title: 'Leave approval issue follow-up', slaStatus: 'OnTrack', status: 'InProgress' }] },
    chat: { session: { id: 'session-1' }, messages: [{ id: 'msg-1', senderType: 'AI', message: 'Customer-visible context only. Internal comments excluded.', status: 'Active' }] }
  };
}
