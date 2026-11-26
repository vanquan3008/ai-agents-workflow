import { statusLabel } from '../lib/format'

const TONE: Record<string, 'neutral' | 'info' | 'warning' | 'success' | 'danger'> = {
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

export function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`badge badge--${TONE[status] ?? 'neutral'}`} data-status={status}>
      {statusLabel(status)}
    </span>
  )
}
