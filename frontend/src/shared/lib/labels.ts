import type { ExecutionSummary, ProjectRole, TaskChannel } from '@/shared/api/types'

const STATUS_LABELS: Record<string, string> = {
  PENDING: 'Chờ xử lý',
  PLANNING: 'Đang lập kế hoạch',
  NEEDS_CLARIFICATION: 'Cần bổ sung',
  WAITING_FOR_APPROVAL: 'Chờ duyệt',
  IN_PROGRESS: 'Đang sửa',
  TESTING: 'Đang chạy test',
  REVIEWING: 'Đang review',
  PUBLISHING: 'Đang mở PR',
  COMPLETED: 'Hoàn tất',
  REJECTED: 'Bị từ chối',
  FAILED: 'Thất bại',
  BLOCKED: 'Bị chặn',
  CANCELLED: 'Đã hủy',
  TIMED_OUT: 'Hết giờ',
  APPROVED: 'Đã duyệt',
  EXPIRED: 'Hết hạn',
  SUPERSEDED: 'Không còn hiệu lực',
  RUNNING: 'Đang chạy',
  SUCCEEDED: 'Thành công',
  DENIED: 'Bị chặn',
  BUDGET_EXCEEDED: 'Chạm trần ngân sách',
}

export const statusLabel = (status: string) => STATUS_LABELS[status] ?? status

const ROLE_LABELS: Record<ProjectRole | 'SUPER_ADMIN', string> = {
  VIEWER: 'Viewer',
  REQUESTER: 'Requester',
  APPROVER: 'Approver',
  OWNER: 'Owner',
  SUPER_ADMIN: 'Super Admin',
}

export const roleLabel = (role: ProjectRole | 'SUPER_ADMIN') => ROLE_LABELS[role] ?? role

export const PROJECT_ROLES: readonly ProjectRole[] = ['VIEWER', 'REQUESTER', 'APPROVER', 'OWNER']

const ROLE_DESCRIPTIONS: Record<ProjectRole, string> = {
  VIEWER: 'Chỉ xem task và lần chạy',
  REQUESTER: 'Giao việc, hủy và thử lại task',
  APPROVER: 'Như Requester, thêm quyền duyệt thay đổi',
  OWNER: 'Toàn quyền: repository, quy trình, thành viên',
}

export const roleDescription = (role: ProjectRole) => ROLE_DESCRIPTIONS[role]

const CHANNEL_LABELS: Record<TaskChannel, string> = {
  DASHBOARD: 'Dashboard',
  DISCORD: 'Discord',
  GITHUB_ISSUE: 'GitHub Issue',
  API: 'API',
}

export const channelLabel = (channel: TaskChannel) => CHANNEL_LABELS[channel] ?? channel

const STEP_LABELS: Record<ExecutionSummary['step'], string> = {
  PLANNER: 'Planner',
  CODING: 'Coding',
  TESTING: 'Testing',
  REVIEWER: 'Reviewer',
  PUBLISHER: 'Publisher',
}

export const stepLabel = (step: ExecutionSummary['step']) => STEP_LABELS[step] ?? step

const ACTOR_LABELS: Record<string, string> = {
  USER: 'Người dùng',
  AGENT: 'Agent',
  SYSTEM: 'Hệ thống',
}

export const actorLabel = (actorKind: string) => ACTOR_LABELS[actorKind] ?? actorKind

const NOTIFICATION_LABELS: Record<string, string> = {
  TASK_ACCEPTED: 'Nhận task',
  APPROVAL_REQUIRED: 'Cần duyệt',
  CLARIFICATION_NEEDED: 'Cần bổ sung thông tin',
  COMPLETED: 'Hoàn tất',
  FAILED: 'Thất bại',
  BLOCKED: 'Bị chặn',
  CANCELLED: 'Đã hủy',
}

export const notificationLabel = (event: string) => NOTIFICATION_LABELS[event] ?? event

const SENSITIVE_AREA_LABELS: Record<string, string> = {
  AUTHENTICATION: 'Xác thực',
  AUTHORIZATION: 'Phân quyền',
  MIGRATION: 'Migration',
  CREDENTIALS: 'Credential',
  CI_CD: 'CI/CD',
  DESTRUCTIVE: 'Thao tác phá hủy',
}

export const sensitiveAreaLabel = (area: string) => SENSITIVE_AREA_LABELS[area] ?? area

const POLICY_OUTCOME_LABELS: Record<string, string> = {
  AUTO_APPROVED: 'Trong phạm vi tự động',
  REQUIRES_APPROVAL: 'Cần người duyệt',
  NEEDS_REVIEW: 'Cần xem xét',
}

export const policyOutcomeLabel = (outcome: string) => POLICY_OUTCOME_LABELS[outcome] ?? outcome
