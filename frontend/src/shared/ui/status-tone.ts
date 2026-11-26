export type StatusTone = 'neutral' | 'info' | 'warning' | 'success' | 'danger'

const TONE_BY_STATUS: Record<string, StatusTone> = {
  PENDING: 'neutral',
  PLANNING: 'info',
  IN_PROGRESS: 'info',
  TESTING: 'info',
  REVIEWING: 'info',
  PUBLISHING: 'info',
  RUNNING: 'info',
  NEEDS_CLARIFICATION: 'warning',
  WAITING_FOR_APPROVAL: 'warning',
  BLOCKED: 'warning',
  COMPLETED: 'success',
  APPROVED: 'success',
  SUCCEEDED: 'success',
  FAILED: 'danger',
  REJECTED: 'danger',
  TIMED_OUT: 'danger',
  DENIED: 'danger',
  BUDGET_EXCEEDED: 'danger',
  CANCELLED: 'neutral',
  EXPIRED: 'neutral',
  SUPERSEDED: 'neutral',
}

export const toneOf = (status: string): StatusTone => TONE_BY_STATUS[status] ?? 'neutral'

/** Statuses the worker is actively processing; shown with a live indicator. */
export const isLiveStatus = (status: string) => toneOf(status) === 'info'
