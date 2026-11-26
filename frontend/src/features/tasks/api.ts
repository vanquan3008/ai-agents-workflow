import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiRequest } from '@/shared/api/http'
import type { ExecutionSummary, PagedResult, TaskCreated, TaskDetail, TaskStatus, TaskSummary, TimelineEntry } from '@/shared/api/types'

export interface TaskFilters {
  projectId?: string
  status?: TaskStatus | ''
  search?: string
  page?: number
  pageSize?: number
}

export const taskKeys = {
  all: ['tasks'] as const,
  list: (filters: TaskFilters) => ['tasks', 'list', filters] as const,
  detail: (id: string) => ['tasks', 'detail', id] as const,
  timeline: (id: string) => ['tasks', 'timeline', id] as const,
  executions: (id: string) => ['tasks', 'executions', id] as const,
}

export interface CreateTaskInput {
  repositoryId: string
  title: string
  description: string
  idempotencyKey: string
}

export const tasksApi = {
  list: (filters: TaskFilters, signal?: AbortSignal) => apiRequest<PagedResult<TaskSummary>>('/tasks', { signal, query: { ...filters } }),
  get: (id: string, signal?: AbortSignal) => apiRequest<TaskDetail>(`/tasks/${id}`, { signal }),
  timeline: (id: string, after = 0, signal?: AbortSignal) => apiRequest<TimelineEntry[]>(`/tasks/${id}/timeline`, { signal, query: { after } }),
  executions: (id: string, signal?: AbortSignal) => apiRequest<ExecutionSummary[]>(`/tasks/${id}/executions`, { signal }),
  create: ({ idempotencyKey, ...body }: CreateTaskInput) =>
    apiRequest<TaskCreated>('/tasks', { method: 'POST', body, headers: { 'Idempotency-Key': idempotencyKey } }),
  cancel: (id: string, reason?: string) => apiRequest<{ status: TaskStatus }>(`/tasks/${id}/cancel`, { method: 'POST', body: { reason } }),
  clarify: (id: string, additionalInfo: string) =>
    apiRequest<{ status: TaskStatus }>(`/tasks/${id}/clarifications`, { method: 'POST', body: { additionalInfo } }),
  retry: (id: string) => apiRequest<{ status: TaskStatus }>(`/tasks/${id}/retry`, { method: 'POST' }),
}

export function useTasks(filters: TaskFilters, options: { refetchInterval?: number } = {}) {
  return useQuery({
    queryKey: taskKeys.list(filters),
    queryFn: ({ signal }) => tasksApi.list(filters, signal),
    placeholderData: keepPreviousData,
    refetchInterval: options.refetchInterval ?? 15_000,
  })
}

export function useTask(id: string) {
  return useQuery({ queryKey: taskKeys.detail(id), queryFn: ({ signal }) => tasksApi.get(id, signal), enabled: Boolean(id), refetchInterval: 15_000 })
}

export function useTaskTimeline(id: string) {
  return useQuery({ queryKey: taskKeys.timeline(id), queryFn: ({ signal }) => tasksApi.timeline(id, 0, signal), enabled: Boolean(id), refetchInterval: 15_000 })
}

export function useTaskExecutions(id: string) {
  return useQuery({ queryKey: taskKeys.executions(id), queryFn: ({ signal }) => tasksApi.executions(id, signal), enabled: Boolean(id), refetchInterval: 15_000 })
}

export function useCreateTask() {
  const queryClient = useQueryClient()
  return useMutation({ mutationFn: tasksApi.create, onSuccess: () => queryClient.invalidateQueries({ queryKey: taskKeys.all }) })
}

function useTaskMutation<TArgs>(fn: (args: TArgs) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({ mutationFn: fn, onSettled: () => queryClient.invalidateQueries({ queryKey: taskKeys.all }) })
}

export const useCancelTask = (id: string) => useTaskMutation((reason: string | undefined) => tasksApi.cancel(id, reason))
export const useClarifyTask = (id: string) => useTaskMutation((info: string) => tasksApi.clarify(id, info))
export const useRetryTask = (id: string) => useTaskMutation(() => tasksApi.retry(id))
