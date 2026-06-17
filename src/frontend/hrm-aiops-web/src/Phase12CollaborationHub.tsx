import {
  Bell,
  CalendarClock,
  CheckSquare,
  GitBranch,
  History,
  MailCheck,
  Megaphone,
  Play,
  Route,
  TimerReset
} from 'lucide-react';
import type React from 'react';
import { useEffect, useState } from 'react';
import { pt } from './phaseI18n';

type LoadState = 'loading' | 'api' | 'mock';

type HubState = {
  customerId: string;
  projectId: string;
  dashboard: any;
  templates: any[];
  deliveries: any[];
  rules: any[];
  runs: any[];
  tasks: any[];
  reminders: any[];
  escalations: any[];
  timeline: any[];
};

const actor = 'security.admin';

export function Phase12CollaborationHub() {
  const [route, setRoute] = useState(() => window.location.pathname);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [state, setState] = useState<HubState>(() => seedHubState());

  async function load() {
    try {
      const customers = await api<any[]>('/api/customers');
      const customerId = customers[0]?.id;
      if (!customerId) throw new Error('No customer');
      const projects = await api<any[]>(`/api/customers/${customerId}/projects`, customerId);
      const projectId = projects[0]?.id;
      const [dashboard, templates, deliveries, rules, runs, tasks, reminders, escalations, timeline] = await Promise.all([
        api<any>(`/api/customers/${customerId}/collaboration/dashboard`, customerId),
        api<any[]>(`/api/customers/${customerId}/notification-templates`, customerId),
        api<any[]>(`/api/customers/${customerId}/notification-deliveries`, customerId),
        api<any[]>(`/api/customers/${customerId}/workflow-rules`, customerId),
        api<any[]>(`/api/customers/${customerId}/workflow-runs`, customerId),
        api<any[]>(`/api/customers/${customerId}/collaboration/tasks`, customerId),
        api<any[]>(`/api/customers/${customerId}/collaboration/reminders`, customerId),
        api<any[]>(`/api/customers/${customerId}/collaboration/escalations`, customerId),
        api<any[]>(`/api/customers/${customerId}/collaboration/timeline`, customerId)
      ]);
      setState({ customerId, projectId, dashboard, templates, deliveries, rules, runs, tasks, reminders, escalations, timeline });
      setLoadState('api');
    } catch {
      setState(seedHubState());
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

  async function triggerWorkflow() {
    if (loadState !== 'api') return;
    await api(`/api/customers/${state.customerId}/workflow-events`, state.customerId, {
      method: 'POST',
      body: JSON.stringify({ projectId: state.projectId, triggerEvent: 'CustomerRequestSubmitted', sourceEntityType: 'PortalRequest', sourceEntityId: null, message: 'Manual smoke workflow event from Collaboration Hub UI.' })
    });
    await load();
  }

  async function runReminders() {
    if (loadState !== 'api') return;
    await api(`/api/customers/${state.customerId}/collaboration/reminders/run-due`, state.customerId, { method: 'POST' });
    await load();
  }

  async function completeTask() {
    const task = state.tasks.find((item) => item.status !== 'Completed');
    if (!task || loadState !== 'api') return;
    await api(`/api/customers/${state.customerId}/collaboration/tasks/${task.id}/complete`, state.customerId, { method: 'POST' });
    await load();
  }

  return (
    <main className="phase7-shell">
      <aside className="sidebar phase7-nav">
        <div className="brand">
          <div className="brand-mark">P12</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>{pt('Collaboration Hub')}</span>
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
            <p>Phase 12</p>
            <h2>{pt(title(route))}</h2>
          </div>
          <span className={`status ${loadState === 'api' ? 'ok' : 'idle'}`}>
            {loadState === 'loading' ? pt('Loading API') : loadState === 'api' ? pt('API connected') : pt('Local mock mode')}
          </span>
        </header>

        {route.includes('/notifications') ? <Notifications state={state} /> :
          route.includes('/workflow') ? <Workflow state={state} onTrigger={triggerWorkflow} /> :
            route.includes('/tasks') ? <Tasks state={state} onComplete={completeTask} /> :
              route.includes('/reminders') ? <Reminders state={state} onRun={runReminders} /> :
                route.includes('/escalations') ? <Escalations state={state} /> :
                  route.includes('/timeline') ? <Timeline state={state} /> :
                    <Dashboard state={state} />}
      </section>
    </main>
  );
}

const nav = [
  { route: '/collaboration-hub', label: 'Dashboard', icon: <Route size={16} /> },
  { route: '/collaboration-hub/notifications', label: 'Notifications', icon: <Bell size={16} /> },
  { route: '/collaboration-hub/workflow', label: 'Workflow', icon: <GitBranch size={16} /> },
  { route: '/collaboration-hub/tasks', label: 'Tasks', icon: <CheckSquare size={16} /> },
  { route: '/collaboration-hub/reminders', label: 'Reminders', icon: <CalendarClock size={16} /> },
  { route: '/collaboration-hub/escalations', label: 'Escalations', icon: <Megaphone size={16} /> },
  { route: '/collaboration-hub/timeline', label: 'Timeline', icon: <History size={16} /> }
];

function Dashboard({ state }: { state: HubState }) {
  const data = state.dashboard;
  return (
    <>
      <section className="metric-grid phase7-metrics">
        <Metric label={pt('Unread notifications')} value={data.unreadNotifications} />
        <Metric label={pt('Delivery failures')} value={data.deliveryFailures} />
        <Metric label={pt('Active rules')} value={data.activeWorkflowRules} />
        <Metric label={pt('Runs today')} value={data.workflowRunsToday} />
        <Metric label={pt('Open tasks')} value={data.openTasks} />
        <Metric label={pt('Due reminders')} value={data.dueReminders} />
        <Metric label={pt('Open escalations')} value={data.openEscalations} />
        <Metric label={pt('Deliveries')} value={state.deliveries.length} />
      </section>
      <section className="content-grid two">
        <Panel title={pt('Latest Timeline')} icon={<History size={18} />}>
          <Rows items={data.latestTimeline ?? state.timeline} primary="title" secondary="itemType" />
        </Panel>
        <Panel title={pt('Latest Deliveries')} icon={<MailCheck size={18} />}>
          <Rows items={data.latestDeliveries ?? state.deliveries} primary="recipientRef" secondary="status" />
        </Panel>
      </section>
    </>
  );
}

function Notifications({ state }: { state: HubState }) {
  return <section className="content-grid two"><Panel title={pt('Notification Templates')} icon={<Bell size={18} />}><Rows items={state.templates.map((x) => ({ ...x.template, status: `${x.template.channel} / ${x.template.notificationType}` }))} primary="name" secondary="status" /></Panel><Panel title={pt('Delivery Logs')} icon={<MailCheck size={18} />}><Rows items={state.deliveries} primary="recipientRef" secondary="status" /></Panel></section>;
}

function Workflow({ state, onTrigger }: { state: HubState; onTrigger: () => void }) {
  return <section className="content-grid two"><Panel title={pt('Workflow Rules')} icon={<GitBranch size={18} />} action={<button onClick={onTrigger}><Play size={14} /> {pt('Trigger Sample')}</button>}><Rows items={state.rules} primary="name" secondary="triggerEvent" /></Panel><Panel title={pt('Workflow Runs')} icon={<Route size={18} />}><Rows items={state.runs.map((x) => ({ ...x.run, title: x.run.triggerEvent }))} primary="title" secondary="status" /></Panel></section>;
}

function Tasks({ state, onComplete }: { state: HubState; onComplete: () => void }) {
  return <Panel title={pt('Task Assignment')} icon={<CheckSquare size={18} />} action={<button onClick={onComplete}>{pt('Complete First')}</button>}><Rows items={state.tasks} primary="title" secondary="status" /></Panel>;
}

function Reminders({ state, onRun }: { state: HubState; onRun: () => void }) {
  return <Panel title={pt('Reminder Queue')} icon={<TimerReset size={18} />} action={<button onClick={onRun}>{pt('Run Due')}</button>}><Rows items={state.reminders.map((x) => ({ ...x, title: x.reminderType }))} primary="title" secondary="status" /></Panel>;
}

function Escalations({ state }: { state: HubState }) {
  return <Panel title={pt('Escalation Center')} icon={<Megaphone size={18} />}><Rows items={state.escalations.map((x) => ({ ...x, title: x.reason }))} primary="title" secondary="status" /></Panel>;
}

function Timeline({ state }: { state: HubState }) {
  return <Panel title={pt('Activity Timeline')} icon={<History size={18} />}><Rows items={state.timeline} primary="title" secondary="itemType" /></Panel>;
}

function Metric({ label, value }: { label: string; value: any }) {
  return <div className="metric"><Route size={18} /><span>{label}</span><strong>{value ?? 0}</strong></div>;
}

function Panel({ title, icon, action, children }: { title: string; icon: React.ReactNode; action?: React.ReactNode; children: React.ReactNode }) {
  return <section className="panel"><header>{icon}<h3>{title}</h3>{action}</header>{children}</section>;
}

function Rows({ items, primary, secondary }: { items: any[]; primary: string; secondary: string }) {
  if (!items?.length) return <p className="empty">{pt('No collaboration data.')}</p>;
  return <div className="action-list">{items.map((item, index) => <div className="list-row" key={item.id ?? index}><div><strong>{item[primary] ?? item.title ?? item.name}</strong><span>{item[secondary] ?? item.status}</span><small>{item.createdAt ?? item.startedAt ?? item.dueAt ?? ''}</small></div><StatusBadge text={String(item.status ?? item[secondary] ?? 'Active')} /></div>)}</div>;
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

function title(route: string) {
  return nav.find((item) => item.route === route)?.label ?? 'Collaboration Hub';
}

function seedHubState(): HubState {
  return {
    customerId: 'demo',
    projectId: 'project-1',
    dashboard: { unreadNotifications: 3, deliveryFailures: 0, activeWorkflowRules: 3, workflowRunsToday: 2, openTasks: 2, dueReminders: 1, openEscalations: 1, latestTimeline: [], latestDeliveries: [] },
    templates: [{ template: { id: 'tpl-1', name: 'Approval Required', channel: 'Email', notificationType: 'ApprovalRequired', status: 'Active' } }],
    deliveries: [{ id: 'del-1', recipientRef: 'consultant.lead', status: 'Delivered', channel: 'Email' }],
    rules: [{ id: 'rule-1', name: 'Approval pending creates task and reminder', triggerEvent: 'ApprovalPending', status: 'Active' }],
    runs: [{ run: { id: 'run-1', triggerEvent: 'ApprovalPending', status: 'Completed', startedAt: new Date().toISOString() } }],
    tasks: [{ id: 'task-1', title: 'Prepare customer UAT sign-off pack', status: 'Open', priority: 'High' }],
    reminders: [{ id: 'rem-1', reminderType: 'TaskDue', status: 'Scheduled', remindAt: new Date().toISOString() }],
    escalations: [{ id: 'esc-1', reason: 'SLA breached by workflow automation.', status: 'Notified' }],
    timeline: [{ id: 'time-1', title: 'Workflow executed', itemType: 'System', status: 'Completed' }]
  };
}
