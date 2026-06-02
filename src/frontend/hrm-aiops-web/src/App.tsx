import { AlertTriangle, Bot, CheckCircle2, FileText, GitCompare, Layers3, Link2, Play, Plus, Rocket, ShieldCheck, Users } from 'lucide-react';
import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  api,
  AuditLog,
  AiProposal,
  AiRun,
  ApplyRun,
  Blueprint,
  ConfigSpec,
  Customer,
  CustomerConnector,
  DocumentSignOff,
  Environment,
  EnvironmentSnapshot,
  FixProposal,
  HrmModule,
  Issue,
  PromptTemplate,
  Project,
  Requirement,
  RegressionTestPlan,
  RegressionTestRun,
  ReleaseDraft,
  ReleaseReadinessReport,
  SnapshotDiff,
  TraceChain,
  UrsDocument
} from './api';

type LoadState = 'idle' | 'loading' | 'error';

export function App() {
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

  const selectedCustomer = customers.find((x) => x.id === customerId);
  const selectedProject = projects.find((x) => x.id === projectId);

  async function refreshAll(nextCustomerId = customerId, nextProjectId = projectId) {
    setState('loading');
    try {
      const [nextCustomers, nextModules] = await Promise.all([
        api.getList<Customer>('/api/customers'),
        api.get<HrmModule[]>('/api/hrm-modules')
      ]);
      setCustomers(nextCustomers);
      setModules(nextModules);
      setPromptTemplates(await api.get<PromptTemplate[]>('/api/ai/prompt-templates'));

      const activeCustomerId = nextCustomerId || nextCustomers[0]?.id || '';
      setCustomerId(activeCustomerId);
      if (!activeCustomerId) {
        setState('idle');
        return;
      }

      const nextProjects = await api.getList<Project>(`/api/customers/${activeCustomerId}/projects`);
      setProjects(nextProjects);
      const activeProjectId = nextProjectId || nextProjects[0]?.id || '';
      setProjectId(activeProjectId);

      const [nextAudits, nextAiRuns] = await Promise.all([
        api.getList<AuditLog>(`/api/customers/${activeCustomerId}/audit-logs`),
        api.getList<AiRun>(`/api/customers/${activeCustomerId}/ai-runs`)
      ]);
      setAudits(nextAudits);
      setAiRuns(nextAiRuns);

      if (activeProjectId) {
        const [
          nextRequirements,
          nextEnvironments,
          nextUrs,
          nextBlueprints,
          nextConfigs,
          nextIssues,
          nextFixProposals,
          nextTestPlans,
          nextReleaseDrafts,
          nextConnectors,
          nextApplyRuns,
          nextSnapshots,
          nextSnapshotDiffs,
          nextRegressionRuns,
          nextReadinessReports,
          nextSignOffs,
          nextTraceChains,
          nextAiProposals
        ] = await Promise.all([
          api.getList<Requirement>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/requirements`),
          api.get<Environment[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/environments`),
          api.get<UrsDocument[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/urs`),
          api.get<Blueprint[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/blueprints`),
          api.get<ConfigSpec[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/config-specs`),
          api.getList<Issue>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/issues`),
          api.get<FixProposal[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/fix-proposals`),
          api.get<RegressionTestPlan[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/regression-test-plans`),
          api.get<ReleaseDraft[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/release-drafts`),
          api.get<CustomerConnector[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/connectors`),
          api.get<ApplyRun[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/apply-runs`),
          api.get<EnvironmentSnapshot[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/environment-snapshots`),
          api.get<SnapshotDiff[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/snapshot-diffs`),
          api.get<RegressionTestRun[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/regression-test-runs`),
          api.get<ReleaseReadinessReport[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/release-readiness-reports`),
          api.get<DocumentSignOff[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/document-signoffs`),
          api.get<TraceChain[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/traceability/view`),
          api.get<AiProposal[]>(`/api/customers/${activeCustomerId}/projects/${activeProjectId}/ai-proposals`)
        ]);
        setRequirements(nextRequirements);
        setEnvironments(nextEnvironments);
        setUrs(nextUrs);
        setBlueprints(nextBlueprints);
        setConfigs(nextConfigs);
        setIssues(nextIssues);
        setFixProposals(nextFixProposals);
        setTestPlans(nextTestPlans);
        setReleaseDrafts(nextReleaseDrafts);
        setConnectors(nextConnectors);
        setApplyRuns(nextApplyRuns);
        setSnapshots(nextSnapshots);
        setSnapshotDiffs(nextSnapshotDiffs);
        setRegressionRuns(nextRegressionRuns);
        setReadinessReports(nextReadinessReports);
        setSignOffs(nextSignOffs);
        setTraceChains(nextTraceChains);
        setAiProposals(nextAiProposals);
      }

      setState('idle');
    } catch (error) {
      setState('error');
      setMessage(error instanceof Error ? error.message : 'Unexpected error');
    }
  }

  useEffect(() => {
    refreshAll();
  }, []);

  async function submit<T>(action: () => Promise<T>, done: string) {
    try {
      setState('loading');
      await action();
      setMessage(done);
      await refreshAll();
    } catch (error) {
      setState('error');
      setMessage(error instanceof Error ? error.message : 'Unexpected error');
    }
  }

  const metrics = useMemo(
    () => [
      { label: 'Requirements', value: requirements.length },
      { label: 'URS', value: urs.length },
      { label: 'Blueprints', value: blueprints.length },
      { label: 'Config Specs', value: configs.length },
      { label: 'Open Issues', value: issues.filter((x) => x.status !== 'Closed').length },
      { label: 'Fix Proposals', value: fixProposals.length },
      { label: 'Apply Runs', value: applyRuns.length },
      { label: 'Release Ready', value: readinessReports.filter((x) => x.status === 'Ready').length },
      { label: 'Approved Docs', value: [...requirements, ...urs, ...blueprints, ...configs].filter((x) => x.status === 'Approved').length },
      { label: 'AI Proposals', value: aiProposals.length },
      { label: 'Pending AI Review', value: aiProposals.filter((x) => x.status === 'PendingReview').length }
    ],
    [requirements, urs, blueprints, configs, issues, fixProposals, applyRuns, readinessReports, aiProposals]
  );

  return (
    <main className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">HR</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>Controlled Test Apply & Regression</span>
          </div>
        </div>

        <label>
          Customer
          <select value={customerId} onChange={(event) => refreshAll(event.target.value, '')}>
            {customers.map((customer) => (
              <option key={customer.id} value={customer.id}>
                {customer.code} - {customer.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Project
          <select value={projectId} onChange={(event) => refreshAll(customerId, event.target.value)}>
            {projects.map((project) => (
              <option key={project.id} value={project.id}>
                {project.code} - {project.name}
              </option>
            ))}
          </select>
        </label>

        <button className="primary" onClick={() => refreshAll()}>
          <Play size={16} /> Refresh
        </button>
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <p>{selectedCustomer?.name ?? 'No customer selected'}</p>
            <h2>{selectedProject?.name ?? 'No project selected'}</h2>
          </div>
          <Status state={state} message={message} />
        </header>

        <section className="metric-grid">
          {metrics.map((metric) => (
            <div className="metric" key={metric.label}>
              <FileText size={18} />
              <span>{metric.label}</span>
              <strong>{metric.value}</strong>
            </div>
          ))}
        </section>

        <section className="content-grid two">
          <Panel title="Customer / Project" icon={<Users size={18} />}>
            <CustomerForm onSubmit={(body) => submit(() => api.post('/api/customers', body), 'Customer created')} />
            <ProjectForm
              disabled={!customerId}
              onSubmit={(body) => submit(() => api.post(`/api/customers/${customerId}/projects`, body), 'Project created')}
            />
          </Panel>

          <Panel title="HRM Module Risk Defaults" icon={<ShieldCheck size={18} />}>
            <ActionList
              empty="No modules"
              items={modules.map((item) => ({
                key: item.id,
                title: `${item.code} - ${item.name}`,
                meta: `${item.defaultRiskLevel} risk / ${item.description}`
              }))}
            />
          </Panel>
        </section>

        <section className="content-grid two">
          <Panel title="Requirement Intake" icon={<FileText size={18} />}>
            <RequirementForm
              disabled={!projectId}
              onSubmit={(body) => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/requirements/manual`, body), 'Requirement created')}
            />
            <ActionList
              empty="No requirements"
              items={requirements.map((item) => ({
                key: item.id,
                title: `${item.requirementNo} v${item.version} ${item.title}`,
                meta: `${item.status}${item.isLatest ? ' / latest' : ' / superseded'}`,
                action: (
                  <DocActions
                    approved={item.status === 'Approved'}
                    onGenerate={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/requirements/${item.id}/generate-urs`), 'AI URS proposal created')}
                    onVersion={() =>
                      submit(
                        () =>
                          api.post(`/api/customers/${customerId}/projects/${projectId}/requirements/${item.id}/versions`, {
                            title: item.title,
                            contentText: `${item.contentText}\n\nRevision note: updated in Phase 2 lifecycle.`,
                            status: 'Draft',
                            updatedBy: 'demo.user'
                          }),
                        'Requirement version created'
                      )
                    }
                    onSignOff={() => signOff('requirements', item.id, 'Requirement signed off')}
                  />
                )
              }))}
            />
          </Panel>

          <Panel title="URS Management" icon={<Bot size={18} />}>
            <ActionList
              empty="No URS documents"
              items={urs.map((item) => ({
                key: item.id,
                title: `${item.ursNo} v${item.version} ${item.title}`,
                meta: `${item.status} / ${trim(item.content)}`,
                action: (
                  <DocActions
                    approved={item.status === 'Approved'}
                    onGenerate={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/urs/${item.id}/generate-blueprint`), 'AI blueprint proposal created')}
                    onVersion={() =>
                      submit(
                        () =>
                          api.post(`/api/customers/${customerId}/projects/${projectId}/urs/${item.id}/versions`, {
                            title: item.title,
                            content: `${item.content}\n\nRevision note: clarified acceptance criteria.`,
                            status: 'Draft',
                            updatedBy: 'demo.user'
                          }),
                        'URS version created'
                      )
                    }
                    onSignOff={() => signOff('urs', item.id, 'URS signed off')}
                  />
                )
              }))}
            />
          </Panel>
        </section>

        <section className="content-grid two">
          <Panel title="Blueprint Management" icon={<Layers3 size={18} />}>
            <ActionList
              empty="No blueprints"
              items={blueprints.map((item) => ({
                key: item.id,
                title: `${item.blueprintNo} v${item.version} ${item.type}`,
                meta: `${item.status} / ${trim(item.content)}`,
                action: (
                  <DocActions
                    approved={item.status === 'Approved'}
                    onGenerate={() =>
                      submit(
                        () =>
                          api.post(`/api/customers/${customerId}/projects/${projectId}/blueprints/${item.id}/generate-config-spec`, {
                            moduleName: 'Leave Management'
                          }),
                        'AI config spec proposal created'
                      )
                    }
                    onVersion={() =>
                      submit(
                        () =>
                          api.post(`/api/customers/${customerId}/projects/${projectId}/blueprints/${item.id}/versions`, {
                            title: item.type,
                            content: `${item.content}\n\nRevision note: updated workflow mapping.`,
                            status: 'Draft',
                            updatedBy: 'demo.user'
                          }),
                        'Blueprint version created'
                      )
                    }
                    onSignOff={() => signOff('blueprints', item.id, 'Blueprint signed off')}
                  />
                )
              }))}
            />
          </Panel>

          <Panel title="Config Specification" icon={<CheckCircle2 size={18} />}>
            <ActionList
              empty="No config specs"
              items={configs.map((item) => ({
                key: item.id,
                title: `${item.configNo} v${item.version} ${item.moduleName}`,
                meta: `${item.status} / ${item.riskLevel} risk / ${trim(item.content)}`,
                action: (
                  <DocActions
                    approved={item.status === 'Approved'}
                    onVersion={() =>
                      submit(
                        () =>
                          api.post(`/api/customers/${customerId}/projects/${projectId}/config-specs/${item.id}/versions`, {
                            moduleName: item.moduleName,
                            content: `${item.content}\n\nRevision note: refined configuration parameter.`,
                            status: 'Draft',
                            updatedBy: 'demo.user'
                          }),
                        'Config spec version created'
                      )
                    }
                    onSignOff={() => signOff('config-specs', item.id, 'Config spec signed off')}
                  />
                )
              }))}
            />
          </Panel>
        </section>

        <section className="content-grid two">
          <Panel title="Operation Issues" icon={<AlertTriangle size={18} />}>
            <IssueForm
              disabled={!projectId}
              configs={configs}
              onSubmit={(body) => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues`, body), 'Issue created')}
            />
            <ActionList
              empty="No operation issues"
              items={issues.map((item) => ({
                key: item.id,
                title: `${item.issueNo} / ${item.status} / ${item.riskLevel}`,
                meta: `${item.category} / ${item.severity} / ${item.title} / ${trim(item.rootCauseSummary ?? item.description)}`,
                action: (
                  <IssueAiActions
                    closed={item.status === 'Closed'}
                    onClassify={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${item.id}/classify`), 'AI issue classification proposal created')}
                    onRootCause={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${item.id}/root-cause`), 'AI root cause proposal created')}
                    onFix={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${item.id}/fix-proposal`), 'AI fix proposal created')}
                    onChange={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${item.id}/change-request-draft`), 'AI change request draft created')}
                    onTest={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${item.id}/regression-test-plan`), 'AI regression test plan created')}
                    onRelease={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${item.id}/release-draft`), 'AI release draft created')}
                    onKnowledge={() => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${item.id}/knowledge-update`), 'AI knowledge update proposal created')}
                    onClose={() =>
                      submit(
                        () =>
                          api.post(`/api/customers/${customerId}/projects/${projectId}/issues/${item.id}/close`, {
                            closedBy: 'support.lead',
                            resolutionNote: 'Closed after accepted operation workflow.'
                          }),
                        'Issue closed'
                      )
                    }
                  />
                )
              }))}
            />
          </Panel>

          <Panel title="Fix / Test / Release Drafts" icon={<Rocket size={18} />}>
            <ActionList
              empty="No fix proposals"
              items={fixProposals.slice(0, 5).map((item) => ({
                key: item.id,
                title: `FIX-${item.id.slice(0, 8)} / ${item.status} / ${item.riskLevel}`,
                meta: `${item.title} / ${trim(item.proposedSolution)}`
              }))}
            />
            <ActionList
              empty="No regression test plans"
              items={testPlans.slice(0, 4).map((item) => ({
                key: item.id,
                title: `${item.testPlanNo} / ${item.status} / ${item.riskLevel}`,
                meta: `${item.title} / ${trim(item.content)}`
              }))}
            />
            <ActionList
              empty="No release drafts"
              items={releaseDrafts.slice(0, 4).map((item) => ({
                key: item.id,
                title: `${item.releaseDraftNo} / ${item.status} / ${item.riskLevel}`,
                meta: `${item.title} / ${trim(item.releaseNotes)}`
              }))}
            />
          </Panel>
        </section>

        <section className="content-grid two">
          <Panel title="Controlled Test Apply" icon={<Rocket size={18} />}>
            <TestApplyForm
              disabled={!projectId}
              environments={environments.filter((item) => item.kind === 'Test' || item.kind === 'Uat')}
              connectors={connectors}
              fixProposals={fixProposals}
              onSubmit={(body) => submit(() => api.post(`/api/customers/${customerId}/projects/${projectId}/controlled-test-apply/dry-run`, body), 'Test apply dry run completed')}
            />
            <ActionList
              empty="No apply runs"
              items={applyRuns.slice(0, 6).map((item) => ({
                key: item.id,
                title: `${item.applyRunNo} / ${item.status} / ${item.riskLevel}`,
                meta: `${environmentName(environments, item.environmentId)} / ${item.sourceType} / ${trim(item.summary || item.rollbackRecommendation || 'Controlled Test/UAT apply run')}`,
                action: (
                  <div className="row-actions">
                    <button
                      disabled={item.status !== 'DryRunSucceeded' && item.status !== 'ApprovalRequired'}
                      onClick={() =>
                        submit(
                          () =>
                            api.post(`/api/customers/${customerId}/projects/${projectId}/apply-runs/${item.id}/execute`, {
                              requestedBy: 'test.apply.operator'
                            }),
                          'Controlled Test/UAT apply executed'
                        )
                      }
                    >
                      <Rocket size={15} /> Execute
                    </button>
                  </div>
                )
              }))}
            />
          </Panel>

          <Panel title="Regression & Readiness" icon={<GitCompare size={18} />}>
            <ActionList
              empty="No release readiness reports"
              items={readinessReports.slice(0, 4).map((item) => ({
                key: item.id,
                title: `${item.reportNo} / ${item.status}`,
                meta: `${environmentName(environments, item.environmentId)} / ${trim(item.summary || item.blockers)}`
              }))}
            />
            <ActionList
              empty="No regression runs"
              items={regressionRuns.slice(0, 4).map((item) => ({
                key: item.id,
                title: `${item.runNo} / ${item.status} / ${item.passedTests}/${item.totalTests}`,
                meta: `${environmentName(environments, item.environmentId)} / ${trim(item.summary)}`
              }))}
            />
            <ActionList
              empty="No snapshot diffs"
              items={snapshotDiffs.slice(0, 4).map((item) => ({
                key: item.id,
                title: `${item.snapshotKind} / ${item.riskLevel}`,
                meta: `${environmentName(environments, item.environmentId)} / ${trim(item.diffSummary)}`
              }))}
            />
            <ActionList
              empty="No snapshots"
              items={snapshots.slice(0, 4).map((item) => ({
                key: item.id,
                title: `${item.snapshotNo} / ${item.stage}`,
                meta: `${environmentName(environments, item.environmentId)} / ${trim(item.maskedSummary)}`
              }))}
            />
          </Panel>
        </section>

        <section className="content-grid two">
          <Panel title="AI Preview / Accept / Reject" icon={<Bot size={18} />}>
            <ActionList
              empty="No AI proposals"
              items={aiProposals.map((item) => ({
                key: item.id,
                title: `${item.taskType} / ${item.status}`,
                meta: `${item.title} / ${trim(item.proposedContent)}`,
                action:
                  item.status === 'PendingReview' ? (
                    <div className="row-actions">
                      <button
                        onClick={() =>
                          submit(
                            () =>
                              api.post(`/api/customers/${customerId}/projects/${projectId}/ai-proposals/${item.id}/accept`, {
                                reviewedBy: 'consultant.user',
                                comment: 'Accepted from AI preview.'
                              }),
                            'AI proposal accepted'
                          )
                        }
                      >
                        <CheckCircle2 size={15} /> Accept
                      </button>
                      <button
                        onClick={() =>
                          submit(
                            () =>
                              api.post(`/api/customers/${customerId}/projects/${projectId}/ai-proposals/${item.id}/reject`, {
                                reviewedBy: 'consultant.user',
                                comment: 'Rejected from AI preview.'
                              }),
                            'AI proposal rejected'
                          )
                        }
                      >
                        Reject
                      </button>
                    </div>
                  ) : undefined
              }))}
            />
          </Panel>

          <Panel title="AI Run History" icon={<ShieldCheck size={18} />}>
            <ActionList
              empty="No AI runs"
              items={aiRuns.slice(0, 8).map((item) => ({
                key: item.id,
                title: `${item.runType} / ${item.status}`,
                meta: `${item.provider} ${item.model} / ${item.promptTemplateKey ?? 'no-template'} v${item.promptVersion ?? '-'} / ${trim(item.maskedInputPreview ?? item.outputSummary ?? '')}`
              }))}
            />
          </Panel>
        </section>

        <section className="content-grid two">
          <Panel title="Traceability View" icon={<Link2 size={18} />}>
            <div className="trace-list">
              {traceChains.map((chain, index) => (
                <div className="trace-row" key={`${chain.requirement.id}-${index}`}>
                  <TraceNode label="REQ" text={`${chain.requirement.requirementNo} v${chain.requirement.version}`} status={chain.requirement.status} />
                  <TraceNode label="URS" text={chain.urs ? `${chain.urs.ursNo} v${chain.urs.version}` : 'Missing'} status={chain.urs?.status ?? 'Gap'} />
                  <TraceNode label="BP" text={chain.blueprint ? `${chain.blueprint.blueprintNo} v${chain.blueprint.version}` : 'Missing'} status={chain.blueprint?.status ?? 'Gap'} />
                  <TraceNode label="CFG" text={chain.configSpec ? `${chain.configSpec.configNo} ${chain.configSpec.riskLevel}` : 'Missing'} status={chain.configSpec?.status ?? 'Gap'} />
                </div>
              ))}
            </div>
          </Panel>

          <Panel title="Sign-off / Audit" icon={<ShieldCheck size={18} />}>
            <ActionList
              empty="No sign-offs"
              items={signOffs.slice(0, 6).map((item) => ({
                key: item.id,
                title: `${item.documentKind} v${item.version}`,
                meta: `${item.signedOffBy} / ${item.role ?? 'Reviewer'} / ${new Date(item.signedOffAt).toLocaleString()}`
              }))}
            />
            <ActionList
              empty="No audit logs"
              items={audits.slice(0, 8).map((item) => ({
                key: item.id,
                title: item.action,
                meta: `${item.entityType} / ${new Date(item.createdAt).toLocaleString()}`
              }))}
            />
          </Panel>
        </section>

        <section className="content-grid two">
          <Panel title="Prompt Template Management" icon={<Bot size={18} />}>
            <PromptTemplateForm onSubmit={(body) => submit(() => api.post('/api/ai/prompt-templates', body), 'Prompt template created')} />
            <ActionList
              empty="No prompt templates"
              items={promptTemplates.map((item) => ({
                key: item.template.id,
                title: `${item.template.key} / ${item.template.taskType}`,
                meta: `${item.versions[0]?.version ? `v${item.versions[0].version}` : 'no version'} / ${item.template.description}`
              }))}
            />
          </Panel>

          <Panel title="AI Controls" icon={<ShieldCheck size={18} />}>
            <div className="control-list">
              <span>Provider abstraction: LocalStructured now, external providers later</span>
              <span>Prompt version required for every run</span>
              <span>Masked context only, scoped by CustomerId and ProjectId</span>
              <span>JSON validation before proposal persistence</span>
              <span>Accept/Reject required before document draft creation</span>
              <span>Diagnosis, fix, release and knowledge updates stay as proposals until reviewed</span>
            </div>
          </Panel>
        </section>
      </section>
    </main>
  );

  function signOff(route: string, id: string, done: string) {
    return submit(
      () =>
        api.post(`/api/customers/${customerId}/projects/${projectId}/${route}/${id}/sign-off`, {
          signedOffBy: 'business.owner',
          role: 'Business Owner',
          comment: 'Approved in Document Lifecycle Core.'
        }),
      done
    );
  }
}

function PromptTemplateForm({ onSubmit }: { onSubmit: (body: unknown) => void }) {
  return (
    <form
      className="stack-form"
      onSubmit={(event) => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        onSubmit({
          key: form.get('key'),
          name: form.get('name'),
          taskType: form.get('taskType'),
          description: form.get('description'),
          systemPrompt: form.get('systemPrompt'),
          userPromptTemplate: form.get('userPromptTemplate'),
          outputJsonSchema: form.get('outputJsonSchema'),
          createdBy: 'platform.admin'
        });
        event.currentTarget.reset();
      }}
    >
      <input name="key" placeholder="template-key" required />
      <input name="name" placeholder="Template name" required />
      <select name="taskType" defaultValue="GenerateUrs">
        <option>GenerateUrs</option>
        <option>GenerateBlueprint</option>
        <option>GenerateConfigSpec</option>
        <option>ClassifyIssue</option>
        <option>AnalyzeRootCause</option>
        <option>GenerateFixProposal</option>
        <option>GenerateChangeRequest</option>
        <option>GenerateRegressionTestPlan</option>
        <option>GenerateReleaseDraft</option>
        <option>GenerateKnowledgeUpdate</option>
      </select>
      <input name="description" placeholder="Description" required />
      <textarea name="systemPrompt" placeholder="System prompt" required />
      <textarea name="userPromptTemplate" placeholder="User prompt template" required />
      <textarea name="outputJsonSchema" placeholder='{"type":"object","required":["title","content"]}' required />
      <button>
        <Plus size={15} /> Prompt
      </button>
    </form>
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

function Status({ state, message }: { state: LoadState; message: string }) {
  return <div className={`status ${state}`}>{state === 'loading' ? 'Working...' : message || 'Ready'}</div>;
}

function CustomerForm({ onSubmit }: { onSubmit: (body: unknown) => void }) {
  return (
    <SmartForm
      fields={[
        ['code', 'Code', 'ACME'],
        ['name', 'Name', 'ACME HR'],
        ['industry', 'Industry', 'Manufacturing']
      ]}
      submitLabel="Customer"
      onSubmit={onSubmit}
    />
  );
}

function ProjectForm({ disabled, onSubmit }: { disabled: boolean; onSubmit: (body: unknown) => void }) {
  return (
    <SmartForm
      disabled={disabled}
      fields={[
        ['code', 'Code', 'HRM-OPS'],
        ['name', 'Name', 'HRM Operations'],
        ['hrmProductName', 'HRM Product', 'Custom HRM']
      ]}
      submitLabel="Project"
      onSubmit={onSubmit}
    />
  );
}

function RequirementForm({ disabled, onSubmit }: { disabled: boolean; onSubmit: (body: unknown) => void }) {
  return (
    <form
      className="stack-form"
      onSubmit={(event) => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        onSubmit({
          title: form.get('title'),
          contentText: form.get('contentText'),
          createdBy: 'demo.user'
        });
        event.currentTarget.reset();
      }}
    >
      <input name="title" placeholder="Requirement title" disabled={disabled} required />
      <textarea name="contentText" placeholder="Requirement content" disabled={disabled} required />
      <button disabled={disabled}>
        <Plus size={15} /> Requirement
      </button>
    </form>
  );
}

function IssueForm({ disabled, configs, onSubmit }: { disabled: boolean; configs: ConfigSpec[]; onSubmit: (body: unknown) => void }) {
  return (
    <form
      className="stack-form"
      onSubmit={(event) => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        const linkedConfigId = String(form.get('linkedConfigId') ?? '');
        const category = String(form.get('category') ?? '');
        onSubmit({
          environmentId: null,
          linkedEntityType: linkedConfigId ? 'ConfigSpec' : null,
          linkedEntityId: linkedConfigId || null,
          title: form.get('title'),
          description: form.get('description'),
          category: category || null,
          severity: form.get('severity'),
          priority: form.get('priority'),
          reportedBy: form.get('reportedBy')
        });
        event.currentTarget.reset();
      }}
    >
      <input name="title" placeholder="Issue title" disabled={disabled} required />
      <textarea name="description" placeholder="Issue description" disabled={disabled} required />
      <div className="compact-form">
        <select name="category" defaultValue="" disabled={disabled}>
          <option value="">Auto category</option>
          <option>Functional</option>
          <option>Configuration</option>
          <option>Data</option>
          <option>Integration</option>
          <option>Security</option>
          <option>Permission</option>
          <option>Payroll</option>
          <option>Performance</option>
          <option>ProductionDatabase</option>
          <option>Other</option>
        </select>
        <select name="severity" defaultValue="High" disabled={disabled}>
          <option>Low</option>
          <option>Medium</option>
          <option>High</option>
          <option>Critical</option>
        </select>
        <input name="priority" placeholder="P1" defaultValue="P1" disabled={disabled} required />
        <input name="reportedBy" placeholder="reported.by" defaultValue="customer.hr" disabled={disabled} required />
      </div>
      <select name="linkedConfigId" defaultValue="" disabled={disabled}>
        <option value="">No config link</option>
        {configs.map((item) => (
          <option key={item.id} value={item.id}>
            {item.configNo} - {item.moduleName}
          </option>
        ))}
      </select>
      <button disabled={disabled}>
        <Plus size={15} /> Issue
      </button>
    </form>
  );
}

function TestApplyForm({
  disabled,
  environments,
  connectors,
  fixProposals,
  onSubmit
}: {
  disabled: boolean;
  environments: Environment[];
  connectors: CustomerConnector[];
  fixProposals: FixProposal[];
  onSubmit: (body: unknown) => void;
}) {
  const defaultEnvironmentId = environments[0]?.id ?? '';
  const availableConnectors = connectors.filter((item) => !defaultEnvironmentId || !item.environmentId || item.environmentId === defaultEnvironmentId);

  return (
    <form
      className="stack-form"
      onSubmit={(event) => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        const connectorId = String(form.get('connectorId') ?? '');
        onSubmit({
          environmentId: form.get('environmentId'),
          connectorId: connectorId || null,
          sourceType: 'FixProposal',
          sourceId: form.get('sourceId'),
          requestedBy: 'test.apply.operator'
        });
      }}
    >
      <div className="compact-form">
        <select name="environmentId" defaultValue={defaultEnvironmentId} disabled={disabled || environments.length === 0} required>
          {environments.map((item) => (
            <option key={item.id} value={item.id}>
              {item.name} - {item.kind}
            </option>
          ))}
        </select>
        <select name="connectorId" defaultValue="" disabled={disabled}>
          <option value="">Auto mock connector</option>
          {availableConnectors.map((item) => (
            <option key={item.id} value={item.id}>
              {item.name}
            </option>
          ))}
        </select>
      </div>
      <select name="sourceId" disabled={disabled || fixProposals.length === 0} required>
        {fixProposals.map((item) => (
          <option key={item.id} value={item.id}>
            {item.title} - {item.riskLevel}
          </option>
        ))}
      </select>
      <button disabled={disabled || environments.length === 0 || fixProposals.length === 0}>
        <Play size={15} /> Dry Run
      </button>
    </form>
  );
}

function SmartForm({ fields, submitLabel, disabled, onSubmit }: { fields: [string, string, string][]; submitLabel: string; disabled?: boolean; onSubmit: (body: unknown) => void }) {
  return (
    <form
      className="compact-form"
      onSubmit={(event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        const body = Object.fromEntries(fields.map(([key]) => [key, form.get(key)]));
        onSubmit(body);
        event.currentTarget.reset();
      }}
    >
      {fields.map(([key, label, placeholder]) => (
        <input key={key} name={key} aria-label={label} placeholder={placeholder} disabled={disabled} required={key === 'code' || key === 'name'} />
      ))}
      <button disabled={disabled}>
        <Plus size={15} /> {submitLabel}
      </button>
    </form>
  );
}

function DocActions({ approved, onGenerate, onVersion, onSignOff }: { approved: boolean; onGenerate?: () => void; onVersion: () => void; onSignOff: () => void }) {
  return (
    <div className="row-actions">
      {onGenerate && (
        <button onClick={onGenerate}>
          <Bot size={15} /> Generate
        </button>
      )}
      <button onClick={onVersion}>
        <Plus size={15} /> Version
      </button>
      <button disabled={approved} onClick={onSignOff}>
        <CheckCircle2 size={15} /> Sign-off
      </button>
    </div>
  );
}

function IssueAiActions({
  closed,
  onClassify,
  onRootCause,
  onFix,
  onChange,
  onTest,
  onRelease,
  onKnowledge,
  onClose
}: {
  closed: boolean;
  onClassify: () => void;
  onRootCause: () => void;
  onFix: () => void;
  onChange: () => void;
  onTest: () => void;
  onRelease: () => void;
  onKnowledge: () => void;
  onClose: () => void;
}) {
  return (
    <div className="row-actions">
      <button disabled={closed} onClick={onClassify}>
        <Bot size={15} /> Classify
      </button>
      <button disabled={closed} onClick={onRootCause}>
        RCA
      </button>
      <button disabled={closed} onClick={onFix}>
        Fix
      </button>
      <button disabled={closed} onClick={onChange}>
        CR
      </button>
      <button disabled={closed} onClick={onTest}>
        Test
      </button>
      <button disabled={closed} onClick={onRelease}>
        Release
      </button>
      <button disabled={closed} onClick={onKnowledge}>
        KB
      </button>
      <button disabled={closed} onClick={onClose}>
        Close
      </button>
    </div>
  );
}

function ActionList({ items, empty }: { items: { key: string; title: string; meta: string; action?: React.ReactNode }[]; empty: string }) {
  if (items.length === 0) {
    return <p className="empty">{empty}</p>;
  }

  return (
    <div className="action-list">
      {items.map((item) => (
        <div className="list-row" key={item.key}>
          <div>
            <strong>{item.title}</strong>
            <span>{item.meta}</span>
          </div>
          {item.action}
        </div>
      ))}
    </div>
  );
}

function TraceNode({ label, text, status }: { label: string; text: string; status: string }) {
  return (
    <div className={`trace-node ${status.toLowerCase()}`}>
      <span>{label}</span>
      <strong>{text}</strong>
      <small>{status}</small>
    </div>
  );
}

function controlClass(_: string) {
  return '';
}

function trim(value: string) {
  return value.length > 130 ? `${value.slice(0, 130)}...` : value;
}

function environmentName(environments: Environment[], environmentId: string) {
  const environment = environments.find((item) => item.id === environmentId);
  return environment ? `${environment.name} (${environment.kind})` : 'Environment';
}
