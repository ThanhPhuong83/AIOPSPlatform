import {
  DatabaseZap,
  FileCheck2,
  KeyRound,
  LockKeyhole,
  ShieldCheck,
  SlidersHorizontal,
  UserRoundCog
} from 'lucide-react';
import { useEffect, useState } from 'react';
import { api } from './api';
import { pt } from './phaseI18n';

type SecurityDashboard = {
  actor: string;
  tenantAccessGrants: number;
  roles: number;
  permissions: number;
  dataClassificationRules: number;
  aiAccessPolicies: number;
  connectorSecurityPolicies: number;
  approvalGovernanceRules: number;
  multiStepApprovalRules: number;
  complianceEvidence: number;
  deniedSecretAccess: number;
  secretAccessCount: number;
  immutableAuditLogs: number;
  controls: string[];
};

type RoleBundle = {
  roles: Array<{ id: string; roleKey: string; name: string; description: string }>;
  permissions: Array<{ id: string; permissionKey: string; resource: string; action: string; sensitive: boolean }>;
  rolePermissions: Array<{ id: string; roleKey: string; permissionKey: string; effect: string }>;
  assignments: Array<{ id: string; userId: string; roleKey: string; status: string }>;
};

type ApprovalRule = {
  id: string;
  ruleKey: string;
  moduleName: string;
  minimumRiskLevel?: string;
  appliesToProduction: boolean;
  requiredApprovalSteps: number;
  approverRolesCsv: string;
  requiresSecurityApproval: boolean;
  reason: string;
};

type ClassificationRule = {
  id: string;
  resourceType: string;
  fieldName: string;
  classification: string;
  maskingStrategy: string;
  applyToAiPrompt: boolean;
};

type LoadState = 'loading' | 'api' | 'mock';

export function Phase9SecurityGovernance() {
  const [route, setRoute] = useState(() => window.location.pathname);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [dashboard, setDashboard] = useState<SecurityDashboard>(() => seedDashboard());
  const [roles, setRoles] = useState<RoleBundle>(() => seedRoles());
  const [approvalRules, setApprovalRules] = useState<ApprovalRule[]>(() => seedApprovalRules());
  const [classificationRules, setClassificationRules] = useState<ClassificationRule[]>(() => seedClassificationRules());

  async function load() {
    try {
      const customers = await api.get<any[]>('/api/customers');
      const customer = customers[0];
      if (!customer) throw new Error('No customer');
      const projects = await api.get<any[]>(`/api/customers/${customer.id}/projects`);
      const project = projects[0];
      if (!project) throw new Error('No project');
      const headers = { 'X-User-Id': 'security.admin', 'X-Customer-Id': customer.id };
      const [dashboardData, roleData, approvalData, classificationData] = await Promise.all([
        fetchJson<SecurityDashboard>(`/api/customers/${customer.id}/security/dashboard?projectId=${project.id}`, headers),
        fetchJson<RoleBundle>(`/api/customers/${customer.id}/security/roles`, headers),
        fetchJson<ApprovalRule[]>(`/api/customers/${customer.id}/projects/${project.id}/security/approval-governance-rules`, headers),
        fetchJson<ClassificationRule[]>(`/api/customers/${customer.id}/security/data-classification?projectId=${project.id}`, headers)
      ]);
      setDashboard(dashboardData);
      setRoles(roleData);
      setApprovalRules(approvalData);
      setClassificationRules(classificationData);
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
          <div className="brand-mark">P9</div>
          <div>
            <h1>HRM AI Ops</h1>
            <span>{pt('Enterprise Security')}</span>
          </div>
        </div>
        <button onClick={() => navigate('/security-dashboard')}>
          <ShieldCheck size={16} /> {pt('Security Dashboard')}
        </button>
        <button onClick={() => navigate('/tenant-security')}>
          <UserRoundCog size={16} /> {pt('Tenant & RBAC')}
        </button>
        <button onClick={() => navigate('/compliance-evidence')}>
          <FileCheck2 size={16} /> {pt('Compliance')}
        </button>
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <p>Phase 9</p>
            <h2>{route.startsWith('/tenant-security') ? pt('Tenant Security & RBAC') : route.startsWith('/compliance-evidence') ? pt('Compliance Evidence') : pt('Security Dashboard')}</h2>
          </div>
          <span className={`status ${loadState === 'api' ? 'ok' : 'idle'}`}>
            {loadState === 'loading' ? pt('Loading API') : loadState === 'api' ? pt('API connected') : pt('Local mock mode')}
          </span>
        </header>

        {route.startsWith('/tenant-security') ? (
          <TenantSecurityView roles={roles} classificationRules={classificationRules} />
        ) : route.startsWith('/compliance-evidence') ? (
          <ComplianceView dashboard={dashboard} approvalRules={approvalRules} />
        ) : (
          <SecurityDashboardView dashboard={dashboard} approvalRules={approvalRules} classificationRules={classificationRules} />
        )}
      </section>
    </main>
  );
}

async function fetchJson<T>(path: string, headers: Record<string, string>): Promise<T> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'}${path}`, { headers });
  if (!response.ok) throw new Error(await response.text());
  return response.json() as Promise<T>;
}

function SecurityDashboardView({ dashboard, approvalRules, classificationRules }: { dashboard: SecurityDashboard; approvalRules: ApprovalRule[]; classificationRules: ClassificationRule[] }) {
  const metrics = [
    [pt('Tenant grants'), dashboard.tenantAccessGrants],
    [pt('Roles'), dashboard.roles],
    [pt('Permissions'), dashboard.permissions],
    [pt('AI policies'), dashboard.aiAccessPolicies],
    [pt('Connector policies'), dashboard.connectorSecurityPolicies],
    [pt('Multi-step approval rules'), dashboard.multiStepApprovalRules],
    [pt('Secret access events'), dashboard.secretAccessCount],
    [pt('Immutable audit logs'), dashboard.immutableAuditLogs]
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
        <Panel title={pt('Security Controls')} icon={<LockKeyhole size={18} />}>
          <div className="control-list">
            {dashboard.controls.map((control) => <span key={control}>{control}</span>)}
          </div>
        </Panel>
        <Panel title={pt('Data Classification')} icon={<DatabaseZap size={18} />}>
          <div className="action-list">
            {classificationRules.map((rule) => (
              <div className="list-row" key={rule.id}>
                <div>
                  <strong>{rule.resourceType}.{rule.fieldName}</strong>
                  <span>{rule.classification} / {rule.maskingStrategy} / AI prompt: {rule.applyToAiPrompt ? 'Masked' : 'Not applied'}</span>
                </div>
                <StatusBadge text={rule.classification} />
              </div>
            ))}
          </div>
        </Panel>
      </section>
      <section className="content-grid">
        <ApprovalRulePanel approvalRules={approvalRules} />
      </section>
    </>
  );
}

function TenantSecurityView({ roles, classificationRules }: { roles: RoleBundle; classificationRules: ClassificationRule[] }) {
  return (
    <section className="content-grid two">
      <Panel title={pt('RBAC Roles')} icon={<UserRoundCog size={18} />}>
        <div className="action-list">
          {roles.roles.map((role) => (
            <div className="list-row" key={role.id}>
              <div>
                <strong>{role.roleKey}</strong>
                <span>{role.name} / {role.description}</span>
              </div>
            </div>
          ))}
        </div>
      </Panel>
      <Panel title={pt('Sensitive Permissions')} icon={<KeyRound size={18} />}>
        <div className="action-list">
          {roles.permissions.filter((item) => item.sensitive).map((permission) => (
            <div className="list-row" key={permission.id}>
              <div>
                <strong>{permission.permissionKey}</strong>
                <span>{permission.resource} / {permission.action}</span>
              </div>
              <StatusBadge text="Sensitive" />
            </div>
          ))}
        </div>
      </Panel>
      <Panel title={pt('Role Assignments')} icon={<ShieldCheck size={18} />}>
        <div className="action-list">
          {roles.assignments.map((assignment) => (
            <div className="list-row" key={assignment.id}>
              <div>
                <strong>{assignment.userId}</strong>
                <span>{assignment.roleKey} / {assignment.status}</span>
              </div>
            </div>
          ))}
        </div>
      </Panel>
      <Panel title={pt('AI Masking Rules')} icon={<SlidersHorizontal size={18} />}>
        <div className="control-list">
          {classificationRules.map((rule) => <span key={rule.id}>{rule.resourceType}.{rule.fieldName}: {rule.maskingStrategy}</span>)}
        </div>
      </Panel>
    </section>
  );
}

function ComplianceView({ dashboard, approvalRules }: { dashboard: SecurityDashboard; approvalRules: ApprovalRule[] }) {
  return (
    <section className="content-grid two">
      <Panel title={pt('Compliance Coverage')} icon={<FileCheck2 size={18} />}>
        <div className="phase7-summary">
          <strong>{dashboard.complianceEvidence} evidence item(s)</strong>
          <span>{dashboard.immutableAuditLogs} immutable audit log(s) available for evidence trace.</span>
          <span>{dashboard.multiStepApprovalRules} high-risk approval governance rule(s) enforce multi-step review.</span>
        </div>
      </Panel>
      <ApprovalRulePanel approvalRules={approvalRules} />
    </section>
  );
}

function ApprovalRulePanel({ approvalRules }: { approvalRules: ApprovalRule[] }) {
  return (
    <Panel title={pt('Approval Governance Rules')} icon={<FileCheck2 size={18} />}>
      <div className="action-list">
        {approvalRules.map((rule) => (
          <div className="list-row" key={rule.id}>
            <div>
              <strong>{rule.moduleName} / {rule.minimumRiskLevel ?? 'Any'} / {rule.requiredApprovalSteps} steps</strong>
              <span>{rule.approverRolesCsv}</span>
              <small>{rule.reason}</small>
            </div>
            <StatusBadge text={rule.requiresSecurityApproval ? 'Security approval' : 'Standard'} />
          </div>
        ))}
      </div>
    </Panel>
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
  return <span className={`phase7-badge ${text.toLowerCase().replaceAll(' ', '-')}`}>{text}</span>;
}

function seedDashboard(): SecurityDashboard {
  return {
    actor: 'security.admin',
    tenantAccessGrants: 2,
    roles: 5,
    permissions: 11,
    dataClassificationRules: 2,
    aiAccessPolicies: 2,
    connectorSecurityPolicies: 2,
    approvalGovernanceRules: 5,
    multiStepApprovalRules: 5,
    complianceEvidence: 0,
    deniedSecretAccess: 0,
    secretAccessCount: 0,
    immutableAuditLogs: 2,
    controls: ['TENANT_ISOLATION_HEADER_GUARD', 'RBAC_POLICY_AUTHORIZATION', 'SECRET_REF_ONLY', 'AI_MASKING_REQUIRED', 'CONNECTOR_PERMISSION_ENFORCED', 'PRODUCTION_APPROVAL_GOVERNANCE']
  };
}

function seedRoles(): RoleBundle {
  return {
    roles: [
      { id: 'role-tenant-admin', roleKey: 'tenant.admin', name: 'Tenant Admin', description: 'Full tenant security administrator.' },
      { id: 'role-security', roleKey: 'security.officer', name: 'Security Officer', description: 'Security and compliance manager.' },
      { id: 'role-release', roleKey: 'release.manager', name: 'Release Manager', description: 'Production release governance owner.' }
    ],
    permissions: [
      { id: 'perm-security', permissionKey: 'security.manage', resource: 'Security', action: 'Manage', sensitive: true },
      { id: 'perm-secret', permissionKey: 'secret.read', resource: 'Secret', action: 'Read', sensitive: true },
      { id: 'perm-prod', permissionKey: 'production.deploy', resource: 'Production', action: 'Deploy', sensitive: true }
    ],
    rolePermissions: [],
    assignments: [
      { id: 'assign-admin', userId: 'security.admin', roleKey: 'tenant.admin', status: 'Active' },
      { id: 'assign-release', userId: 'release.manager', roleKey: 'release.manager', status: 'Active' }
    ]
  };
}

function seedApprovalRules(): ApprovalRule[] {
  return ['Payroll', 'Permission', 'Security', 'Integration', 'Production Database'].map((moduleName) => ({
    id: moduleName,
    ruleKey: `approval.${moduleName.toLowerCase().replaceAll(' ', '-')}`,
    moduleName,
    minimumRiskLevel: 'High',
    appliesToProduction: true,
    requiredApprovalSteps: 2,
    approverRolesCsv: 'release.manager,security.officer,business.owner',
    requiresSecurityApproval: true,
    reason: `${moduleName} changes require multi-step enterprise approval.`
  }));
}

function seedClassificationRules(): ClassificationRule[] {
  return [
    { id: 'class-issue', resourceType: 'Issue', fieldName: 'employeeName', classification: 'Restricted', maskingStrategy: 'Redact', applyToAiPrompt: true },
    { id: 'class-payroll', resourceType: 'Payroll', fieldName: 'salary', classification: 'Secret', maskingStrategy: 'Tokenize', applyToAiPrompt: true }
  ];
}
