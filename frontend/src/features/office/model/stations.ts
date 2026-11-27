import type { ExecutionSummary, TaskStatus, TimelineEntry } from '@/shared/api/types'
import { WORKFLOW_STATIONS, type WorkflowStation } from '../types'

/** How a station relates to the selected task. Every state has a text label so color is never the only signal. */
export type StationState = 'active' | 'waiting' | 'failed' | 'stopped' | 'done' | 'passed' | 'idle'

export const STATION_LABELS: Record<WorkflowStation, string> = {
  PLANNING: 'Tiếp nhận & lập kế hoạch',
  DEVELOPMENT: 'Phát triển',
  TESTING: 'Kiểm thử',
  REVIEW: 'Rà soát',
  APPROVAL: 'Phê duyệt',
  PUBLISHING: 'Xuất bản',
}

export const STATION_DESCRIPTIONS: Record<WorkflowStation, string> = {
  PLANNING: 'Planner đọc yêu cầu và snapshot repository, đề xuất kế hoạch có phiên bản.',
  DEVELOPMENT: 'Coding chuẩn bị bản vá trong workspace cô lập, trong phạm vi đã được duyệt.',
  TESTING: 'Chạy build và test đã đăng ký trong sandbox với commit ứng viên.',
  REVIEW: 'Reviewer đọc diff thực tế cùng bằng chứng kiểm thử.',
  APPROVAL: 'Người có quyền duyệt đúng phiên bản kế hoạch hoặc commit SHA.',
  PUBLISHING: 'Publisher mở hoặc khôi phục draft pull request kèm bằng chứng.',
}

export const STATE_LABELS: Record<StationState, string> = {
  active: 'Đang chạy',
  waiting: 'Đang chờ',
  failed: 'Đã dừng do lỗi',
  stopped: 'Đã dừng',
  done: 'Hoàn tất',
  passed: 'Đã qua',
  idle: 'Chưa tới',
}

/** Which execution step records the evidence of each station; approvals come from the approvals API instead. */
export const STATION_EXECUTION_STEP: Record<WorkflowStation, ExecutionSummary['step'] | null> = {
  PLANNING: 'PLANNER',
  DEVELOPMENT: 'CODING',
  TESTING: 'TESTING',
  REVIEW: 'REVIEWER',
  APPROVAL: null,
  PUBLISHING: 'PUBLISHER',
}

/** Stations the Worker executes today. The others show "Chưa triển khai" instead of simulated progress. */
export const IMPLEMENTED_STATIONS: ReadonlySet<WorkflowStation> = new Set<WorkflowStation>(['PLANNING', 'APPROVAL'])

const STATION_BY_STATUS: Partial<Record<TaskStatus, WorkflowStation>> = {
  PENDING: 'PLANNING',
  PLANNING: 'PLANNING',
  NEEDS_CLARIFICATION: 'PLANNING',
  WAITING_FOR_APPROVAL: 'APPROVAL',
  IN_PROGRESS: 'DEVELOPMENT',
  TESTING: 'TESTING',
  REVIEWING: 'REVIEW',
  PUBLISHING: 'PUBLISHING',
  COMPLETED: 'PUBLISHING',
}

const STATE_BY_STATUS: Record<TaskStatus, StationState> = {
  PENDING: 'waiting',
  PLANNING: 'active',
  NEEDS_CLARIFICATION: 'waiting',
  WAITING_FOR_APPROVAL: 'waiting',
  IN_PROGRESS: 'active',
  TESTING: 'active',
  REVIEWING: 'active',
  PUBLISHING: 'active',
  COMPLETED: 'done',
  REJECTED: 'failed',
  FAILED: 'failed',
  BLOCKED: 'failed',
  TIMED_OUT: 'failed',
  CANCELLED: 'stopped',
}

export interface TaskPosition {
  station: WorkflowStation
  state: StationState
}

/**
 * Where a task stands on the floor. A stopped task stays at the station it was in when it stopped,
 * read from the persisted transition into the stopped status.
 */
export function taskPosition(status: TaskStatus, timeline: readonly TimelineEntry[] = []): TaskPosition {
  const state = STATE_BY_STATUS[status]
  const direct = STATION_BY_STATUS[status]
  if (direct) return { station: direct, state }

  const stop = [...timeline].sort((a, b) => b.sequence - a.sequence).find((entry) => entry.to === status)
  const previous = stop?.from ? STATION_BY_STATUS[stop.from] : undefined
  return { station: previous ?? 'PLANNING', state }
}

/**
 * State of every station for one task: stations the persisted timeline visited are "passed", the current one
 * carries the task's state, the rest are idle. Approval can happen before development (plan) or after review (diff),
 * so the history, not a fixed order, decides what was passed.
 */
export function stationStates(status: TaskStatus, timeline: readonly TimelineEntry[] = []): Record<WorkflowStation, StationState> {
  const states = Object.fromEntries(WORKFLOW_STATIONS.map((station) => [station, 'idle'])) as Record<WorkflowStation, StationState>
  for (const entry of timeline) {
    const visited = STATION_BY_STATUS[entry.to]
    if (visited) states[visited] = 'passed'
  }
  const current = taskPosition(status, timeline)
  states[current.station] = current.state
  return states
}

/** Who must act when a task is waiting; null when nobody outside the Worker is needed. */
export function waitingFor(status: TaskStatus): string | null {
  switch (status) {
    case 'NEEDS_CLARIFICATION':
      return 'Người yêu cầu cần bổ sung thông tin.'
    case 'WAITING_FOR_APPROVAL':
      return 'Approver hoặc Owner cần ra quyết định.'
    case 'PENDING':
      return 'Đang chờ Worker nhận việc.'
    default:
      return null
  }
}
