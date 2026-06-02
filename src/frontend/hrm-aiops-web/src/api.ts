export const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(options?.headers ?? {})
    }
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(body || response.statusText);
  }

  return response.json() as Promise<T>;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, {
      method: 'POST',
      body: JSON.stringify(body ?? {})
    }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, {
      method: 'PUT',
      body: JSON.stringify(body ?? {})
    }),
  delete: <T>(path: string) =>
    request<T>(path, {
      method: 'DELETE'
    })
};

export type Customer = {
  id: string;
  code: string;
  name: string;
  status: string;
  industry?: string;
  timezone: string;
};

export type Project = {
  id: string;
  customerId: string;
  code: string;
  name: string;
  status: string;
  hrmProductName?: string;
};

export type Environment = {
  id: string;
  name: string;
  kind: string;
  baseUrl?: string;
  requiresApproval: boolean;
};

export type Requirement = {
  id: string;
  requirementNo: string;
  title: string;
  contentText: string;
  status: string;
  version: number;
  isLatest: boolean;
  approvedBy?: string;
  approvedAt?: string;
};

export type UrsDocument = {
  id: string;
  ursNo: string;
  title: string;
  content: string;
  status: string;
  version: number;
  isLatest: boolean;
  approvedBy?: string;
  approvedAt?: string;
};

export type Blueprint = {
  id: string;
  blueprintNo: string;
  type: string;
  content: string;
  status: string;
  version: number;
  isLatest: boolean;
  approvedBy?: string;
  approvedAt?: string;
};

export type ConfigSpec = {
  id: string;
  configNo: string;
  moduleName: string;
  riskLevel: string;
  content: string;
  status: string;
  version: number;
  isLatest: boolean;
  approvedBy?: string;
  approvedAt?: string;
};

export type HrmModule = {
  id: string;
  code: string;
  name: string;
  defaultRiskLevel: string;
  description: string;
};

export type DocumentSignOff = {
  id: string;
  documentKind: string;
  documentId: string;
  version: number;
  signedOffBy: string;
  role?: string;
  comment?: string;
  signedOffAt: string;
};

export type Issue = {
  id: string;
  customerId: string;
  projectId: string;
  environmentId?: string;
  issueNo: string;
  title: string;
  description: string;
  category: string;
  severity: string;
  priority: string;
  riskLevel: string;
  status: string;
  linkedEntityType?: string;
  linkedEntityId?: string;
  reportedBy?: string;
  rootCauseSummary?: string;
};

export type FixProposal = {
  id: string;
  issueId: string;
  title: string;
  proposedSolution: string;
  codeChangeSummary?: string;
  dbChangeSummary?: string;
  riskLevel: string;
  status: string;
};

export type RegressionTestPlan = {
  id: string;
  issueId: string;
  changeRequestId?: string;
  testPlanNo: string;
  title: string;
  content: string;
  riskLevel: string;
  status: string;
};

export type ReleaseDraft = {
  id: string;
  issueId: string;
  changeRequestId?: string;
  releaseDraftNo: string;
  title: string;
  releaseNotes: string;
  deploymentPlan: string;
  riskLevel: string;
  status: string;
};

export type CustomerConnector = {
  id: string;
  projectId?: string;
  environmentId?: string;
  permissionPolicyId?: string;
  connectorType: string;
  name: string;
  baseUrl?: string;
  secretRef: string;
  configJson: string;
  status: string;
  lastHealthStatus?: string;
  lastRunAt?: string;
};

export type ApplyRun = {
  id: string;
  environmentId: string;
  connectorId: string;
  fixProposalId?: string;
  changeRequestId?: string;
  approvalRequestId?: string;
  preSnapshotId?: string;
  postSnapshotId?: string;
  snapshotDiffId?: string;
  rollbackPlanId?: string;
  regressionTestRunId?: string;
  releaseReadinessReportId?: string;
  applyRunNo: string;
  sourceType: string;
  sourceId: string;
  riskLevel: string;
  status: string;
  requiresApproval: boolean;
  requestedBy: string;
  rollbackRecommendation: string;
  summary: string;
  startedAt: string;
  completedAt?: string;
};

export type ApplyStep = {
  id: string;
  applyRunId: string;
  stepOrder: number;
  name: string;
  status: string;
  details: string;
};

export type ApplyLog = {
  id: string;
  applyRunId: string;
  level: string;
  message: string;
  maskedPayload?: string;
  createdAt: string;
};

export type EnvironmentSnapshot = {
  id: string;
  environmentId: string;
  applyRunId?: string;
  snapshotNo: string;
  kind: string;
  stage: string;
  maskedSummary: string;
  capturedAt: string;
};

export type SnapshotDiff = {
  id: string;
  environmentId: string;
  fromSnapshotId: string;
  toSnapshotId: string;
  snapshotKind: string;
  diffSummary: string;
  riskLevel: string;
};

export type RegressionTestRun = {
  id: string;
  environmentId: string;
  applyRunId: string;
  runNo: string;
  status: string;
  totalTests: number;
  passedTests: number;
  failedTests: number;
  summary: string;
};

export type ReleaseReadinessReport = {
  id: string;
  environmentId: string;
  applyRunId: string;
  reportNo: string;
  status: string;
  summary: string;
  blockers: string;
};

export type AiRun = {
  id: string;
  runType: string;
  provider: string;
  model: string;
  promptTemplateKey?: string;
  promptVersion?: number;
  status: string;
  inputRef?: string;
  maskedInputPreview?: string;
  outputSummary?: string;
  validationErrorsJson?: string;
  startedAt: string;
};

export type AiProposal = {
  id: string;
  aiRunId: string;
  taskType: string;
  sourceEntityType: string;
  sourceEntityId: string;
  targetEntityType: string;
  targetEntityId?: string;
  status: string;
  title: string;
  proposedContent: string;
  structuredOutputJson: string;
  validationErrorsJson: string;
  reviewedBy?: string;
  reviewComment?: string;
  reviewedAt?: string;
  createdAt: string;
};

export type PromptTemplateVersion = {
  id: string;
  templateId: string;
  templateKey: string;
  version: number;
  systemPrompt: string;
  userPromptTemplate: string;
  outputJsonSchema: string;
  createdBy: string;
  isActive: boolean;
};

export type PromptTemplate = {
  template: {
    id: string;
    key: string;
    name: string;
    taskType: string;
    description: string;
    isActive: boolean;
  };
  versions: PromptTemplateVersion[];
};

export type AuditLog = {
  id: string;
  action: string;
  entityType: string;
  entityId: string;
  createdAt: string;
};

export type TraceLink = {
  id: string;
  fromEntityType: string;
  fromEntityId: string;
  toEntityType: string;
  toEntityId: string;
  relationType: string;
};

export type TraceChain = {
  requirement: {
    id: string;
    requirementNo: string;
    title: string;
    version: number;
    status: string;
    isLatest: boolean;
  };
  urs?: {
    id: string;
    ursNo: string;
    title: string;
    version: number;
    status: string;
    isLatest: boolean;
  };
  blueprint?: {
    id: string;
    blueprintNo: string;
    type: string;
    version: number;
    status: string;
    isLatest: boolean;
  };
  configSpec?: {
    id: string;
    configNo: string;
    moduleName: string;
    riskLevel: string;
    version: number;
    status: string;
    isLatest: boolean;
  };
};
