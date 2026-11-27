import type { ApprovalStatus } from '@/shared/api/types'
import { StatusBadge } from '@/shared/ui/status-badge'

/** A pending approval is waiting on a human, so it reads as "Chờ duyệt" with a warning tone rather than a queued task. */
export function ApprovalStatusBadge({ status }: { status: ApprovalStatus }) {
  return status === 'PENDING' ? <StatusBadge status={status} label="Chờ duyệt" tone="warning" /> : <StatusBadge status={status} />
}
