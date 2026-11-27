import { useCallback } from 'react'
import { useSearchParams } from 'react-router'
import { TASK_STATUSES, type TaskStatus } from '@/shared/api/types'

export interface TaskListState {
  projectId: string
  status: TaskStatus | ''
  search: string
  page: number
}

const isTaskStatus = (value: string | null): value is TaskStatus => value !== null && (TASK_STATUSES as readonly string[]).includes(value)

/** Task list filters stored in the URL so views are shareable and survive reloads. */
export function useTaskFilters() {
  const [params, setParams] = useSearchParams()
  const statusParam = params.get('status')

  const state: TaskListState = {
    projectId: params.get('project') ?? '',
    status: isTaskStatus(statusParam) ? statusParam : '',
    search: params.get('q') ?? '',
    page: Math.max(1, Number(params.get('page')) || 1),
  }

  const update = useCallback(
    (changes: Partial<TaskListState>) => {
      setParams(
        (current) => {
          const next = new URLSearchParams(current)
          const write = (key: string, value: string | undefined) => (value ? next.set(key, value) : next.delete(key))
          if ('projectId' in changes) write('project', changes.projectId)
          if ('status' in changes) write('status', changes.status)
          if ('search' in changes) write('q', changes.search)
          const page = changes.page ?? 1
          write('page', page > 1 ? String(page) : undefined)
          return next
        },
        { replace: true },
      )
    },
    [setParams],
  )

  const reset = useCallback(() => setParams(new URLSearchParams(), { replace: true }), [setParams])

  return { ...state, update, reset, hasFilters: Boolean(state.projectId || state.status || state.search) }
}
