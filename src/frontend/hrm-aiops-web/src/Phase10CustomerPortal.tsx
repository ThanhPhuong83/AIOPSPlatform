import {
  BadgeDollarSign,
  BarChart3,
  ClipboardList,
  FileText,
  Gauge,
  Headphones,
  LifeBuoy,
  ReceiptText
} from 'lucide-react';
import type React from 'react';
import { useEffect, useState } from 'react';
import { pt } from './phaseI18n';

type PortalSummary = {
  subscription?: { subscriptionNo: string; status: string; billingCycle: string; currentPeriodStart: string; currentPeriodEnd: string; unitPrice: number; currency: string };
  plan?: { planCode: string; name: string; maxProjects: number; maxConnectors: number; maxAiRunsPerMonth: number; maxTicketsPerMonth: number; slaResponseHours: number; slaResolutionHours: number; enabledModulesCsv: string };
  openTickets: number;
  slaBreached: number;
  latestTickets: PortalTicket[];
  quotas: QuotaSnapshot[];
  latestServiceReport?: ServiceReport;
  billingDrafts: BillingDraft[];
  invoiceDrafts: InvoiceDraft[];
};

type PortalTicket = {
  id: string;
  ticketNo: string;
  title: string;
  severity: string;
  status: string;
  slaStatus: string;
  responseDueAt?: string;
  resolutionDueAt?: string;
  requestedBy: string;
};

type QuotaSnapshot = {
  id: string;
  metricType: string;
  usedQuantity: number;
  includedQuantity: number;
  overageQuantity: number;
  enforcementMode: string;
  blocked: boolean;
  warningMessage: string;
};

type BillingDraft = {
  id: string;
  billingDraftNo: string;
  status: string;
  subtotal: number;
  overageAmount: number;
  totalAmount: number;
  currency: string;
  periodStart: string;
  periodEnd: string;
};

type InvoiceDraft = {
  id: string;
  invoiceNo: string;
  status: string;
  totalAmount: number;
  currency: string;
  issueDate: string;
  dueDate: string;
};

type ServiceReport = {
  reportNo: string;
  issueCount: number;
  slaMetCount: number;
  slaBreachedCount: number;
  releaseCount: number;
  aiRunCount: number;
  connectorRunCount: number;
  healthScore: number;
  summary: string;
};

type LoadState = 'loading' | 'api' | 'mock';

export function Phase10CustomerPortal() {
  const [route, setRoute] = useState(() => window.location.pathname);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [summary, setSummary] = useState<PortalSummary>(() => seedPortalSummary());

  async function load() {
    try {
      const customers = await fetchJson<any[]>('/api/customers');
      const customer = customers[0];
      if (!customer) throw new Error('No customer');
      const data = await fetchJson<PortalSummary>(`/api/customers/${customer.id}/portal/summary`, {
        'X-User-Id': 'security.admin',
        'X-Customer-Id': customer.id
      });
      setSummary(data);
      setLoadState('api');
    } catch {
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

  return (
    <main className="phase7-shell">
      <aside className="sidebar phase7-nav">
        <div className="brand">
          <div className="brand-mark">P10</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>{pt('Customer Portal')}</span>
          </div>
        </div>
        <button onClick={() => navigate('/customer-portal')}>
          <Headphones size={16} /> {pt('Customer Portal')}
        </button>
        <button onClick={() => navigate('/billing-dashboard')}>
          <BadgeDollarSign size={16} /> {pt('Billing')}
        </button>
        <button onClick={() => navigate('/sla-dashboard')}>
          <Gauge size={16} /> {pt('SLA')}
        </button>
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <p>Phase 10</p>
            <h2>{route.startsWith('/billing-dashboard') ? pt('Billing & Subscription') : route.startsWith('/sla-dashboard') ? pt('SLA Dashboard') : pt('Customer Portal')}</h2>
          </div>
          <span className={`status ${loadState === 'api' ? 'ok' : 'idle'}`}>
            {loadState === 'loading' ? pt('Loading API') : loadState === 'api' ? pt('API connected') : pt('Local mock mode')}
          </span>
        </header>

        {route.startsWith('/billing-dashboard') ? (
          <BillingView summary={summary} />
        ) : route.startsWith('/sla-dashboard') ? (
          <SlaView summary={summary} />
        ) : (
          <PortalView summary={summary} />
        )}
      </section>
    </main>
  );
}

async function fetchJson<T>(path: string, headers?: Record<string, string>): Promise<T> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'}${path}`, { headers });
  if (!response.ok) throw new Error(await response.text());
  return response.json() as Promise<T>;
}

function PortalView({ summary }: { summary: PortalSummary }) {
  return (
    <>
      <MetricGrid summary={summary} />
      <section className="content-grid two">
        <Panel title={pt('Subscription')} icon={<ClipboardList size={18} />}>
          <div className="phase7-summary">
            <strong>{summary.plan?.name ?? pt('No active plan')}</strong>
            <span>{summary.subscription?.subscriptionNo} / {summary.subscription?.status} / {summary.subscription?.billingCycle}</span>
            <span>Modules: {summary.plan?.enabledModulesCsv}</span>
          </div>
        </Panel>
        <Panel title={pt('Latest Tickets')} icon={<LifeBuoy size={18} />}>
          <TicketList tickets={summary.latestTickets} />
        </Panel>
      </section>
      <section className="content-grid">
        <QuotaPanel quotas={summary.quotas} />
      </section>
    </>
  );
}

function BillingView({ summary }: { summary: PortalSummary }) {
  return (
    <section className="content-grid two">
      <Panel title={pt('Billing Drafts')} icon={<ReceiptText size={18} />}>
        <div className="action-list">
          {summary.billingDrafts.map((draft) => (
            <div className="list-row" key={draft.id}>
              <div>
                <strong>{draft.billingDraftNo} / {draft.status}</strong>
                <span>{money(draft.totalAmount, draft.currency)} / subtotal {money(draft.subtotal, draft.currency)} / overage {money(draft.overageAmount, draft.currency)}</span>
                <small>{date(draft.periodStart)} - {date(draft.periodEnd)}</small>
              </div>
              <StatusBadge text={draft.status} />
            </div>
          ))}
        </div>
      </Panel>
      <Panel title={pt('Invoice Drafts')} icon={<FileText size={18} />}>
        <div className="action-list">
          {summary.invoiceDrafts.map((invoice) => (
            <div className="list-row" key={invoice.id}>
              <div>
                <strong>{invoice.invoiceNo} / {invoice.status}</strong>
                <span>{money(invoice.totalAmount, invoice.currency)}</span>
                <small>Issue {date(invoice.issueDate)} / Due {date(invoice.dueDate)}</small>
              </div>
              <StatusBadge text={invoice.status} />
            </div>
          ))}
        </div>
      </Panel>
      <Panel title={pt('Usage Quotas')} icon={<BarChart3 size={18} />}>
        <QuotaPanel quotas={summary.quotas} />
      </Panel>
      <Panel title={pt('Payment Tracking')} icon={<BadgeDollarSign size={18} />}>
        <div className="phase7-summary">
          <strong>{pt('Draft only')}</strong>
          <span>Phase 10 records payment status references only. No card or sensitive banking data is stored.</span>
        </div>
      </Panel>
    </section>
  );
}

function SlaView({ summary }: { summary: PortalSummary }) {
  return (
    <section className="content-grid two">
      <Panel title={pt('SLA Summary')} icon={<Gauge size={18} />}>
        <div className="phase7-summary">
          <strong>{summary.slaBreached} breached / {summary.openTickets} open</strong>
          <span>Response target: {summary.plan?.slaResponseHours}h</span>
          <span>Resolution target: {summary.plan?.slaResolutionHours}h</span>
        </div>
      </Panel>
      <Panel title={pt('Customer Service Report')} icon={<FileText size={18} />}>
        {summary.latestServiceReport ? (
          <div className="phase7-summary">
            <strong>{summary.latestServiceReport.reportNo} / Health {summary.latestServiceReport.healthScore}</strong>
            <span>{summary.latestServiceReport.summary}</span>
            <span>Issues {summary.latestServiceReport.issueCount}, releases {summary.latestServiceReport.releaseCount}, AI runs {summary.latestServiceReport.aiRunCount}</span>
          </div>
        ) : <p className="empty">{pt('No service report yet.')}</p>}
      </Panel>
      <Panel title={pt('Ticket SLA Detail')} icon={<LifeBuoy size={18} />}>
        <TicketList tickets={summary.latestTickets} />
      </Panel>
    </section>
  );
}

function MetricGrid({ summary }: { summary: PortalSummary }) {
  const metrics = [
    [pt('Open tickets'), summary.openTickets],
    [pt('SLA breached'), summary.slaBreached],
    [pt('Projects quota'), `${quotaOf(summary, 'Project')}`],
    [pt('Connectors quota'), `${quotaOf(summary, 'Connector')}`],
    [pt('AI runs quota'), `${quotaOf(summary, 'AiRun')}`],
    [pt('Tickets quota'), `${quotaOf(summary, 'Ticket')}`],
    [pt('Health score'), summary.latestServiceReport?.healthScore ?? 0],
    [pt('Invoice drafts'), summary.invoiceDrafts.length]
  ];
  return (
    <section className="metric-grid phase7-metrics">
      {metrics.map(([label, value]) => (
        <div className="metric" key={label}>
          <Headphones size={18} />
          <span>{label}</span>
          <strong>{value}</strong>
        </div>
      ))}
    </section>
  );
}

function TicketList({ tickets }: { tickets: PortalTicket[] }) {
  if (tickets.length === 0) return <p className="empty">{pt('No customer tickets.')}</p>;
  return (
    <div className="action-list">
      {tickets.map((ticket) => (
        <div className="list-row" key={ticket.id}>
          <div>
            <strong>{ticket.ticketNo} / {ticket.title}</strong>
            <span>{ticket.severity} / {ticket.status} / requested by {ticket.requestedBy}</span>
            <small>Response due {date(ticket.responseDueAt)} / Resolution due {date(ticket.resolutionDueAt)}</small>
          </div>
          <StatusBadge text={ticket.slaStatus} />
        </div>
      ))}
    </div>
  );
}

function QuotaPanel({ quotas }: { quotas: QuotaSnapshot[] }) {
  return (
    <div className="action-list">
      {quotas.map((quota) => (
        <div className="list-row" key={quota.id}>
          <div>
            <strong>{quota.metricType}: {quota.usedQuantity}/{quota.includedQuantity}</strong>
            <span>{quota.warningMessage}</span>
            <small>Overage {quota.overageQuantity} / {quota.enforcementMode}</small>
          </div>
          <StatusBadge text={quota.blocked ? 'Blocked' : quota.overageQuantity > 0 ? 'Warning' : 'OnTrack'} />
        </div>
      ))}
    </div>
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

function StatusBadge({ text }: { text: string }) {
  return <span className={`phase7-badge ${text.toLowerCase()}`}>{text}</span>;
}

function quotaOf(summary: PortalSummary, metric: string) {
  const quota = summary.quotas.find((item) => item.metricType === metric);
  return quota ? `${quota.usedQuantity}/${quota.includedQuantity}` : '0/0';
}

function money(amount: number, currency: string) {
  return `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function date(value?: string) {
  return value ? new Date(value).toLocaleDateString() : 'N/A';
}

function seedPortalSummary(): PortalSummary {
  return {
    subscription: { subscriptionNo: 'SUB-00001', status: 'Active', billingCycle: 'Monthly', currentPeriodStart: new Date().toISOString(), currentPeriodEnd: new Date(Date.now() + 29 * 86400000).toISOString(), unitPrice: 2500, currency: 'USD' },
    plan: { planCode: 'ENTERPRISE-AIOPS', name: 'Enterprise AI Ops', maxProjects: 8, maxConnectors: 20, maxAiRunsPerMonth: 5000, maxTicketsPerMonth: 200, slaResponseHours: 4, slaResolutionHours: 48, enabledModulesCsv: 'DocumentLifecycle,AIOrchestrator,Operations,Connectors,ProductionRelease,Security,BillingPortal' },
    openTickets: 1,
    slaBreached: 0,
    latestTickets: [{ id: 'ticket-1', ticketNo: 'TCK-00001', title: 'Leave approval issue follow-up', severity: 'High', status: 'InProgress', slaStatus: 'OnTrack', requestedBy: 'customer.hr', responseDueAt: new Date(Date.now() + 3 * 3600000).toISOString(), resolutionDueAt: new Date(Date.now() + 47 * 3600000).toISOString() }],
    quotas: [
      quota('Project', 1, 8),
      quota('Connector', 1, 20),
      quota('AiRun', 2, 5000),
      quota('Ticket', 1, 200)
    ],
    latestServiceReport: { reportNo: 'CSR-00001', issueCount: 2, slaMetCount: 1, slaBreachedCount: 0, releaseCount: 1, aiRunCount: 2, connectorRunCount: 0, healthScore: 100, summary: 'Issues=2, SLA met/on-track=1, SLA breached=0, AI runs=2, connector runs=0, health=100.' },
    billingDrafts: [{ id: 'billing-1', billingDraftNo: 'BIL-00001', status: 'Draft', subtotal: 2500, overageAmount: 0, totalAmount: 2500, currency: 'USD', periodStart: new Date().toISOString(), periodEnd: new Date(Date.now() + 29 * 86400000).toISOString() }],
    invoiceDrafts: [{ id: 'invoice-1', invoiceNo: 'INV-00001', status: 'Draft', totalAmount: 2500, currency: 'USD', issueDate: new Date().toISOString(), dueDate: new Date(Date.now() + 15 * 86400000).toISOString() }]
  };
}

function quota(metricType: string, used: number, included: number): QuotaSnapshot {
  return { id: metricType, metricType, usedQuantity: used, includedQuantity: included, overageQuantity: Math.max(0, used - included), enforcementMode: 'WarnOnly', blocked: false, warningMessage: `${metricType} quota is within entitlement.` };
}
