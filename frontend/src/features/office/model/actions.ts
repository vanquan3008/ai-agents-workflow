import type { Approval, ExecutionSummary, TaskDetail } from '@/shared/api/types'

export interface PrimaryAction {
  label: string
  to: string
  external?: boolean
}

/** The one next step the viewer can take for a task, reusing the existing permission-aware pages. */
export function primaryAction(task: TaskDetail, approvals: readonly Approval[] = [], executions: readonly ExecutionSummary[] = []): PrimaryAction {
  if (task.status === 'NEEDS_CLARIFICATION') return { label: 'Bổ sung thông tin', to: `/tasks/${task.id}` }
  if (task.status === 'WAITING_FOR_APPROVAL') {
    const pending = approvals.find((approval) => approval.status === 'PENDING')
    return { label: 'Xem yêu cầu duyệt', to: pending ? `/approvals/${pending.id}` : '/approvals' }
  }
  if (task.pullRequest) return { label: 'Mở pull request', to: task.pullRequest.url, external: true }
  const testing = [...executions].reverse().find((execution) => execution.step === 'TESTING')
  if (task.status === 'TESTING' && testing) return { label: 'Xem kết quả kiểm thử', to: `/executions/${testing.id}` }
  return { label: 'Xem chi tiết task', to: `/tasks/${task.id}` }
}
