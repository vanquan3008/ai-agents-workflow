import { describe, expect, it } from 'vitest'
import type { Approval, ExecutionSummary, TaskDetail, TaskStatus } from '@/shared/api/types'
import { primaryAction } from './actions'

const task = (status: TaskStatus, overrides: Partial<TaskDetail> = {}) =>
  ({ id: 't1', status, pullRequest: null, ...overrides }) as TaskDetail

describe('primaryAction', () => {
  it('sends a requester to the clarification form', () => {
    expect(primaryAction(task('NEEDS_CLARIFICATION'))).toEqual({ label: 'Bổ sung thông tin', to: '/tasks/t1' })
  })

  it('links the pending approval when one is known', () => {
    const approvals = [{ id: 'a1', status: 'PENDING' }] as Approval[]

    expect(primaryAction(task('WAITING_FOR_APPROVAL'), approvals).to).toBe('/approvals/a1')
    expect(primaryAction(task('WAITING_FOR_APPROVAL')).to).toBe('/approvals')
  })

  it('opens the pull request as an external link', () => {
    const pullRequest = { number: 7, url: 'https://github.com/acme/app/pull/7', headBranch: 'b', headCommitSha: 'sha' }

    expect(primaryAction(task('COMPLETED', { pullRequest }))).toEqual({ label: 'Mở pull request', to: pullRequest.url, external: true })
  })

  it('shows the latest test run while testing', () => {
    const executions = [
      { id: 'e1', step: 'TESTING' },
      { id: 'e2', step: 'TESTING' },
    ] as ExecutionSummary[]

    expect(primaryAction(task('TESTING'), [], executions).to).toBe('/executions/e2')
  })

  it('falls back to the task page', () => {
    expect(primaryAction(task('PLANNING')).to).toBe('/tasks/t1')
  })
})
