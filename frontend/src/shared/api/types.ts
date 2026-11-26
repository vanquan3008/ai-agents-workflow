/**
 * Kiểu dữ liệu khớp DTO của backend (/api/v1). Trạng thái dùng UPPER_SNAKE như tài liệu thiết kế.
 */

export type TaskStatus =
  | 'PENDING'
  | 'PLANNING'
  | 'NEEDS_CLARIFICATION'
  | 'WAITING_FOR_APPROVAL'
  | 'IN_PROGRESS'
  | 'TESTING'
  | 'REVIEWING'
  | 'PUBLISHING'
  | 'COMPLETED'
  | 'REJECTED'
  | 'FAILED'
  | 'BLOCKED'
  | 'CANCELLED'
  | 'TIMED_OUT'

export const TASK_STATUSES: readonly TaskStatus[] = [
  'PENDING',
  'PLANNING',
  'NEEDS_CLARIFICATION',
  'WAITING_FOR_APPROVAL',
  'IN_PROGRESS',
  'TESTING',
  'REVIEWING',
  'PUBLISHING',
  'COMPLETED',
  'REJECTED',
  'FAILED',
  'BLOCKED',
  'CANCELLED',
  'TIMED_OUT',
]

export type TaskChannel = 'DASHBOARD' | 'DISCORD' | 'GITHUB_ISSUE' | 'API'

export type ApprovalStatus = 'PENDING' | 'APPROVED' | 'REJECTED' | 'EXPIRED' | 'SUPERSEDED'

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasNextPage: boolean
}

export type ProjectRole = 'VIEWER' | 'REQUESTER' | 'APPROVER' | 'OWNER'

export interface Project {
  id: string
  name: string
  slug: string
  /** Vai trò của người xem; Super Admin không là thành viên thì là SUPER_ADMIN. */
  myRole: ProjectRole | 'SUPER_ADMIN'
  memberCount: number
  repositoryCount: number
  createdAt: string
}

export interface ProjectMember {
  userId: string
  displayName: string
  email: string
  role: ProjectRole
  addedAt: string
}

export interface AdminUser {
  id: string
  email: string
  displayName: string
  systemRole: 'USER' | 'SUPER_ADMIN'
  isActive: boolean
  mustChangePassword: boolean
  lockedUntil: string | null
  lastLoginAt: string | null
  projectCount: number
  createdAt: string
}

export interface Repository {
  id: string
  projectId: string
  owner: string
  name: string
  fullName: string
  defaultBranch: string
  installationId: number
  policy: {
    version: number
    autoFixPaths: string[]
    sensitivePaths: { pattern: string; area: string }[]
    buildCommand: string
    testCommand: string
    maxChangedFiles: number
  }
  workflowVersion: number
  isActive: boolean
  createdAt: string
}

export interface TaskSummary {
  id: string
  projectId: string
  repositoryId: string
  repositoryFullName: string
  title: string
  status: TaskStatus
  statusReason: string | null
  channel: TaskChannel
  pullRequestUrl: string | null
  createdAt: string
  updatedAt: string
}

export interface Usage {
  inputTokens: number
  outputTokens: number
  cacheReadTokens: number
  estimatedCostUsd: number
}

export interface TaskPlan {
  version: number
  summary: string
  steps: string[]
  acceptanceCriteria: string[]
  risks: string[]
  affectedPaths: string[]
  policyOutcome: 'AUTO_APPROVED' | 'REQUIRES_APPROVAL' | 'NEEDS_REVIEW'
  sensitiveAreas: string[]
  policyVersion: number
  policyReasons: string[]
  createdAt: string
}

export interface TaskDetail extends Omit<TaskSummary, 'pullRequestUrl'> {
  description: string
  sourceReference: string | null
  requestedBy: string
  baseCommitSha: string | null
  workBranch: string | null
  retryCount: number
  pullRequest: { number: number; url: string; headBranch: string; headCommitSha: string } | null
  currentPlan: TaskPlan | null
  usage: Usage
  allowedNextStatuses: TaskStatus[]
  completedAt: string | null
}

export interface TimelineEntry {
  sequence: number
  from: TaskStatus | null
  to: TaskStatus
  actorKind: 'USER' | 'AGENT' | 'SYSTEM'
  actorId: string
  reason: string | null
  occurredAt: string
}

export interface ExecutionSummary {
  id: string
  taskId: string
  step: 'PLANNER' | 'CODING' | 'TESTING' | 'REVIEWER' | 'PUBLISHER'
  attempt: number
  status: 'RUNNING' | 'SUCCEEDED' | 'FAILED' | 'CANCELLED' | 'TIMED_OUT' | 'BUDGET_EXCEEDED'
  model: string
  usage: Usage
  error: string | null
  startedAt: string
  finishedAt: string | null
  durationSeconds: number | null
}

export interface ToolExecution {
  id: string
  sequence: number
  toolName: string
  status: 'RUNNING' | 'SUCCEEDED' | 'FAILED' | 'DENIED'
  exitCode: number | null
  inputJson: string
  outputJson: string | null
  denialReason: string | null
  startedAt: string
  finishedAt: string | null
}

export interface ExecutionDetail {
  execution: ExecutionSummary
  checkpoint: string | null
  toolExecutions: ToolExecution[]
}

export interface Approval {
  id: string
  taskId: string
  projectId: string
  taskTitle: string
  kind: 'PLAN' | 'DIFF'
  status: ApprovalStatus
  planVersion: number | null
  commitSha: string | null
  sensitiveAreas: string[]
  policyVersion: number
  scope: string
  requestedAt: string
  expiresAt: string
  decidedBy: string | null
  decidedAt: string | null
  comment: string | null
}

export interface TaskCreated {
  taskId: string
  status: TaskStatus
  created: boolean
}

/** Sự kiện SignalR "taskEvent". */
export interface TaskRealtimeEvent {
  taskId: string
  projectId: string
  kind: 'task.status_changed' | 'task.plan_created' | 'approval.decided' | (string & {})
  sequence: number
  status: string | null
  message: string | null
  occurredAt: string
}

// ---------- Quy trình (mức 2) ----------

export type ApprovalRequirement = 'ON_SENSITIVE' | 'ALWAYS'
export type FindingSeverity = 'LOW' | 'MEDIUM' | 'HIGH'
export type NotificationEvent =
  | 'TASK_ACCEPTED'
  | 'APPROVAL_REQUIRED'
  | 'CLARIFICATION_NEEDED'
  | 'COMPLETED'
  | 'FAILED'
  | 'BLOCKED'
  | 'CANCELLED'

export interface WorkflowDefinition {
  planApproval: ApprovalRequirement
  diffApproval: ApprovalRequirement
  plannerMaxAttempts: number
  maxFixIterations: number
  reviewerBlockingSeverity: FindingSeverity
  requireRegressionTest: boolean
  pullRequest: { draft: boolean; labels: string[]; titlePrefix: string; reviewers: string[] }
  notifyOn: NotificationEvent[]
  models: { planner: string | null; coding: string | null; reviewer: string | null }
}

export interface WorkflowLimits {
  plannerMaxAttemptsCap: number
  maxFixIterationsCap: number
  approvalOptions: ApprovalRequirement[]
  severityOptions: FindingSeverity[]
  notificationOptions: NotificationEvent[]
  mandatoryNotifications: NotificationEvent[]
  invariants: string[]
}

export interface RepositoryWorkflow {
  repositoryId: string
  repositoryFullName: string
  version: number
  isDefault: boolean
  definition: WorkflowDefinition
  limits: WorkflowLimits
}

export interface WorkflowRevision {
  version: number
  changedBy: string
  changedAt: string
  comment: string | null
  definition: WorkflowDefinition
}
