import {
  BarChart3,
  BrainCircuit,
  CheckCircle2,
  FileText,
  Lightbulb,
  RefreshCw,
  Search,
  ShieldCheck,
  XCircle
} from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { api } from './api';

type KnowledgeStatus = 'Draft' | 'PendingReview' | 'Approved' | 'Rejected' | 'Superseded' | 'Expired';
type RiskLevel = 'Low' | 'Medium' | 'High' | 'Critical';

type KnowledgeLearningItem = {
  id: string;
  knowledgeNo: string;
  title: string;
  category: string;
  moduleName: string;
  content: string;
  lessonsLearned: string;
  riskLevel: RiskLevel;
  status: KnowledgeStatus;
  version: number;
  sourceEntityType: string;
  sourceEntityId: string;
  sourceSummary: string;
  explainabilityJson: string;
  createdAt: string;
};

type GovernanceScore = {
  id: string;
  scoreType: string;
  moduleName?: string;
  score: number;
  trend: string;
  formula: string;
  explanation: string;
  calculatedAt: string;
};

type RepeatedIssuePattern = {
  id: string;
  moduleName: string;
  category: string;
  issueCount: number;
  riskLevel: RiskLevel;
  summary: string;
  recommendation: string;
};

type GovernanceInsight = {
  id: string;
  insightType: string;
  moduleName: string;
  riskLevel: RiskLevel;
  title: string;
  summary: string;
  recommendation: string;
};

type AiPerformance = {
  qualityScore: number;
  totalRuns: number;
  completedRuns: number;
  failedRuns: number;
  acceptedOutputs: number;
  rejectedOutputs: number;
  failedValidationOutputs: number;
  formula: string;
  explanation: string;
};

type AnalyticsPayload = {
  summary: {
    totalIssues: number;
    openIssues: number;
    approvedKnowledge: number;
    pendingKnowledge: number;
    repeatedPatterns: number;
    scoreCount: number;
  };
  scores: GovernanceScore[];
  repeatedIssuePatterns: RepeatedIssuePattern[];
  insights: GovernanceInsight[];
  knowledge: KnowledgeLearningItem[];
  aiPerformance?: AiPerformance;
  formulas: Record<string, string>;
};

type LoadState = 'loading' | 'api' | 'mock';

export function Phase8GovernanceAnalytics() {
  const [route, setRoute] = useState(() => window.location.pathname);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [context, setContext] = useState<{ customerId: string; projectId: string } | null>(null);
  const [analytics, setAnalytics] = useState<AnalyticsPayload>(() => seedAnalytics());
  const [moduleFilter, setModuleFilter] = useState('');

  async function load(moduleName = moduleFilter) {
    try {
      const customers = await api.get<any[]>('/api/customers');
      const customer = customers[0];
      if (!customer) throw new Error('No customer');
      const projects = await api.get<any[]>(`/api/customers/${customer.id}/projects`);
      const project = projects[0];
      if (!project) throw new Error('No project');
      const suffix = moduleName ? `?module=${encodeURIComponent(moduleName)}` : '';
      const data = await api.get<AnalyticsPayload>(`/api/customers/${customer.id}/projects/${project.id}/governance-analytics${suffix}`);
      setContext({ customerId: customer.id, projectId: project.id });
      setAnalytics(data);
      setLoadState('api');
    } catch {
      setLoadState('mock');
    }
  }

  useEffect(() => {
    load('');
  }, []);

  function navigate(nextRoute: string) {
    window.history.pushState(null, '', nextRoute);
    setRoute(nextRoute);
  }

  async function generateLearning() {
    if (!context) {
      setAnalytics((current) => ({ ...current, knowledge: [...current.knowledge, ...seedGeneratedKnowledge()] }));
      return;
    }
    await api.post(`/api/customers/${context.customerId}/projects/${context.projectId}/knowledge-learning/generate-from-operations`, {
      allowLowRiskAutoApprove: false,
      requestedBy: 'governance.user'
    });
    await load();
  }

  async function recalculate() {
    if (!context) {
      setAnalytics(seedAnalytics());
      return;
    }
    await api.post(`/api/customers/${context.customerId}/projects/${context.projectId}/governance-analytics/recalculate`, {
      moduleName: moduleFilter || null,
      requestedBy: 'governance.user'
    });
    await load();
  }

  async function approveKnowledge(item: KnowledgeLearningItem) {
    if (!context) {
      setAnalytics((current) => ({
        ...current,
        knowledge: current.knowledge.map((row) => row.id === item.id ? { ...row, status: 'Approved' } : row)
      }));
      return;
    }
    await api.post(`/api/customers/${context.customerId}/projects/${context.projectId}/knowledge-learning/${item.id}/approve`, {
      reviewedBy: 'knowledge.owner',
      comment: 'Approved from Phase 8 dashboard.'
    });
    await load();
  }

  async function rejectKnowledge(item: KnowledgeLearningItem) {
    if (!context) {
      setAnalytics((current) => ({
        ...current,
        knowledge: current.knowledge.map((row) => row.id === item.id ? { ...row, status: 'Rejected' } : row)
      }));
      return;
    }
    await api.post(`/api/customers/${context.customerId}/projects/${context.projectId}/knowledge-learning/${item.id}/reject`, {
      reviewedBy: 'knowledge.owner',
      comment: 'Rejected from Phase 8 dashboard.'
    });
    await load();
  }

  const filteredKnowledge = useMemo(
    () => analytics.knowledge.filter((item) => !moduleFilter || item.moduleName.toLowerCase().includes(moduleFilter.toLowerCase())),
    [analytics.knowledge, moduleFilter]
  );

  return (
    <main className="phase7-shell">
      <aside className="sidebar phase7-nav">
        <div className="brand">
          <div className="brand-mark">P8</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>Learning & Governance</span>
          </div>
        </div>
        <button onClick={() => navigate('/governance-analytics')}>
          <BarChart3 size={16} /> Governance Analytics
        </button>
        <button onClick={() => navigate('/knowledge-learning')}>
          <BrainCircuit size={16} /> Knowledge Learning
        </button>
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <p>Phase 8</p>
            <h2>{route.startsWith('/knowledge-learning') ? 'Knowledge Learning' : 'Governance Analytics'}</h2>
          </div>
          <span className={`status ${loadState === 'api' ? 'ok' : 'idle'}`}>
            {loadState === 'loading' ? 'Loading API' : loadState === 'api' ? 'API connected' : 'Local mock mode'}
          </span>
        </header>

        <section className="panel">
          <header>
            <Search size={18} />
            <h3>Analytics Filters</h3>
          </header>
          <div className="compact-form phase7-filter-grid">
            <input value={moduleFilter} onChange={(event) => setModuleFilter(event.target.value)} placeholder="Module, e.g. Leave Management" />
            <button onClick={() => load(moduleFilter)}>
              <Search size={15} /> Apply Filter
            </button>
            <button onClick={recalculate}>
              <RefreshCw size={15} /> Recalculate Scores
            </button>
            <button className="primary" onClick={generateLearning}>
              <BrainCircuit size={15} /> Generate Learning
            </button>
          </div>
        </section>

        {route.startsWith('/knowledge-learning') ? (
          <KnowledgeLearningView items={filteredKnowledge} onApprove={approveKnowledge} onReject={rejectKnowledge} />
        ) : (
          <GovernanceAnalyticsView analytics={analytics} />
        )}
      </section>
    </main>
  );
}

function GovernanceAnalyticsView({ analytics }: { analytics: AnalyticsPayload }) {
  const metrics = [
    ['Total issues', analytics.summary.totalIssues],
    ['Open issues', analytics.summary.openIssues],
    ['Approved knowledge', analytics.summary.approvedKnowledge],
    ['Pending knowledge', analytics.summary.pendingKnowledge],
    ['Repeated patterns', analytics.summary.repeatedPatterns],
    ['Score snapshots', analytics.summary.scoreCount]
  ];

  return (
    <>
      <section className="metric-grid phase7-metrics">
        {metrics.map(([label, value]) => (
          <div className="metric" key={label}>
            <ShieldCheck size={18} />
            <span>{label}</span>
            <strong>{value}</strong>
          </div>
        ))}
      </section>

      <section className="content-grid two">
        <Panel title="Governance Scores" icon={<BarChart3 size={18} />}>
          <div className="action-list">
            {analytics.scores.map((score) => (
              <div className="list-row" key={score.id}>
                <div>
                  <strong>{score.scoreType}: {score.score.toFixed(1)}</strong>
                  <span>{score.explanation}</span>
                  <small>{score.formula}</small>
                </div>
                <StatusBadge text={score.trend} />
              </div>
            ))}
          </div>
        </Panel>

        <Panel title="Repeated Issue Patterns" icon={<RefreshCw size={18} />}>
          <div className="action-list">
            {analytics.repeatedIssuePatterns.map((pattern) => (
              <div className="list-row" key={pattern.id}>
                <div>
                  <strong>{pattern.moduleName} / {pattern.category} / {pattern.issueCount} issues</strong>
                  <span>{pattern.summary}</span>
                  <small>{pattern.recommendation}</small>
                </div>
                <RiskBadge risk={pattern.riskLevel} />
              </div>
            ))}
          </div>
        </Panel>
      </section>

      <section className="content-grid two">
        <Panel title="Governance Insights" icon={<Lightbulb size={18} />}>
          <div className="action-list">
            {analytics.insights.map((insight) => (
              <div className="list-row" key={insight.id}>
                <div>
                  <strong>{insight.title}</strong>
                  <span>{insight.summary}</span>
                  <small>{insight.recommendation}</small>
                </div>
                <RiskBadge risk={insight.riskLevel} />
              </div>
            ))}
          </div>
        </Panel>

        <Panel title="AI Performance Quality" icon={<BrainCircuit size={18} />}>
          {analytics.aiPerformance ? (
            <div className="phase7-summary">
              <strong>{analytics.aiPerformance.qualityScore.toFixed(1)}</strong>
              <span>{analytics.aiPerformance.explanation}</span>
              <span>Runs: {analytics.aiPerformance.completedRuns}/{analytics.aiPerformance.totalRuns}, failed {analytics.aiPerformance.failedRuns}</span>
              <small>{analytics.aiPerformance.formula}</small>
            </div>
          ) : (
            <p className="empty">No AI performance metric yet.</p>
          )}
        </Panel>
      </section>
    </>
  );
}

function KnowledgeLearningView({ items, onApprove, onReject }: { items: KnowledgeLearningItem[]; onApprove: (item: KnowledgeLearningItem) => void; onReject: (item: KnowledgeLearningItem) => void }) {
  return (
    <section className="content-grid">
      <Panel title="Knowledge Learning Items" icon={<FileText size={18} />}>
        <div className="action-list">
          {items.map((item) => (
            <div className="list-row" key={item.id}>
              <div>
                <strong>{item.knowledgeNo} / v{item.version} / {item.title}</strong>
                <span>{item.moduleName} / {item.category} / Source: {item.sourceSummary}</span>
                <span>{item.lessonsLearned}</span>
                <small>{item.explainabilityJson}</small>
                <div className="badge-row">
                  <StatusBadge text={item.status} />
                  <RiskBadge risk={item.riskLevel} />
                </div>
              </div>
              <div className="row-actions">
                <button disabled={item.status === 'Approved'} onClick={() => onApprove(item)}>
                  <CheckCircle2 size={15} /> Approve
                </button>
                <button disabled={item.status === 'Rejected'} onClick={() => onReject(item)}>
                  <XCircle size={15} /> Reject
                </button>
              </div>
            </div>
          ))}
        </div>
      </Panel>
    </section>
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

function RiskBadge({ risk }: { risk: RiskLevel }) {
  return <span className={`phase7-badge risk-${risk.toLowerCase()}`}>{risk}</span>;
}

function seedAnalytics(): AnalyticsPayload {
  const knowledge = seedGeneratedKnowledge();
  return {
    summary: {
      totalIssues: 2,
      openIssues: 2,
      approvedKnowledge: 0,
      pendingKnowledge: 2,
      repeatedPatterns: 1,
      scoreCount: 5
    },
    scores: [
      score('CustomerHealth', 96, 'Improving', '100 - criticalOpen*15 - highOpen*8 - validationFailed*10 - rollbackRequested*12 - totalIssues*2 + closedRelease*5'),
      score('ModuleRisk', 10, 'Declining', 'criticalOpen*25 + highOpen*15 + totalIssues*5 + validationFailed*15 + rollbackRequested*20'),
      score('ConfigRisk', 20, 'Declining', 'highRiskConfigSpec*12 + configLinkedIssues*10 + validationFailed*15'),
      score('ProjectDeliveryQuality', 94, 'Improving', '100 - validationFailed*20 - rollbackRequested*20 - openIssues*3 + readyOrClosedRelease*10'),
      score('AiPerformanceQuality', 100, 'Improving', 'completedRate*40 + acceptedOutputRate*40 + schemaValidationPassRate*20')
    ],
    repeatedIssuePatterns: [
      {
        id: 'pattern-leave',
        moduleName: 'Leave Management',
        category: 'Functional',
        issueCount: 2,
        riskLevel: 'Medium',
        summary: '2 repeated Functional issues detected in Leave Management.',
        recommendation: 'Create preventive checklist, add regression tests and review related config specification.'
      }
    ],
    insights: [
      {
        id: 'insight-quality',
        insightType: 'QualitySignal',
        moduleName: 'All modules',
        riskLevel: 'Medium',
        title: 'Governance score recalculated',
        summary: 'Customer health 96, delivery quality 94, AI quality 100.',
        recommendation: 'Maintain governance controls and monitor repeated issue trends.'
      }
    ],
    knowledge,
    aiPerformance: {
      qualityScore: 100,
      totalRuns: 2,
      completedRuns: 2,
      failedRuns: 0,
      acceptedOutputs: 0,
      rejectedOutputs: 0,
      failedValidationOutputs: 0,
      formula: 'completedRate*40 + acceptedOutputRate*40 + schemaValidationPassRate*20',
      explanation: 'AI quality is based on run completion, human acceptance and schema validation quality.'
    },
    formulas: {}
  };
}

function seedGeneratedKnowledge(): KnowledgeLearningItem[] {
  return [
    knowledge('KLI-00001', 'Leave balance recalculation delayed for backdated approval'),
    knowledge('KLI-00002', 'Leave balance not updated after manager approval')
  ];
}

function knowledge(no: string, title: string): KnowledgeLearningItem {
  return {
    id: crypto.randomUUID(),
    knowledgeNo: no,
    title: `Lesson learned - ${title}`,
    category: 'Functional',
    moduleName: 'Leave Management',
    content: 'Root cause and fix proposal are summarized from masked operational context.',
    lessonsLearned: 'Validate similar scenarios earlier and add regression coverage for Leave Management approval paths.',
    riskLevel: 'Medium',
    status: 'PendingReview',
    version: 1,
    sourceEntityType: 'Issue',
    sourceEntityId: crypto.randomUUID(),
    sourceSummary: `Issue: ${title}`,
    explainabilityJson: '{"formula":"Knowledge proposed from masked issue, root cause and fix proposal; human review required."}',
    createdAt: new Date().toISOString()
  };
}

function score(scoreType: string, value: number, trend: string, formula: string): GovernanceScore {
  return {
    id: crypto.randomUUID(),
    scoreType,
    score: value,
    trend,
    formula,
    explanation: 'Explainable score calculated from project-scoped operational metrics.',
    calculatedAt: new Date().toISOString()
  };
}
