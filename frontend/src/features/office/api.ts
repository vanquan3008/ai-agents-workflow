import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiRequest } from '@/shared/api/http'
import type { Approval, PagedResult, TaskDetail, TaskStatus, TaskSummary } from '@/shared/api/types'
import type {
  DepartmentConfig,
  DepartmentDirectory,
  DepartmentDraft,
  DepartmentEditorOptions,
  DepartmentEditResult,
  DepartmentRevision,
  PublishedDepartment,
} from './types'

export interface DepartmentTaskFilters {
  status?: TaskStatus | ''
  search?: string
  page?: number
  pageSize?: number
}

export interface CreateDepartmentInput {
  template: string
  name?: string
  slug: string
  repositoryIds: string[]
}

// Keys nest under the project and department so a publication can invalidate exactly one office.
export const officeKeys = {
  project: (projectId: string) => ['office', projectId] as const,
  directory: (projectId: string, includeArchived: boolean) => ['office', projectId, 'directory', includeArchived] as const,
  options: (projectId: string) => ['office', projectId, 'options'] as const,
  department: (projectId: string, departmentId: string) => ['office', projectId, departmentId] as const,
  published: (projectId: string, departmentId: string) => ['office', projectId, departmentId, 'published'] as const,
  draft: (projectId: string, departmentId: string) => ['office', projectId, departmentId, 'draft'] as const,
  revisions: (projectId: string, departmentId: string) => ['office', projectId, departmentId, 'revisions'] as const,
  tasks: (projectId: string, departmentId: string, filters: DepartmentTaskFilters) =>
    ['office', projectId, departmentId, 'tasks', filters] as const,
  task: (projectId: string, departmentId: string, taskId: string) => ['office', projectId, departmentId, 'task', taskId] as const,
  taskApprovals: (taskId: string) => ['office', 'approvals', taskId] as const,
}

const base = (projectId: string) => `/projects/${projectId}/departments`
const one = (projectId: string, departmentId: string) => `${base(projectId)}/${departmentId}`

export const officeApi = {
  directory: (projectId: string, includeArchived: boolean, signal?: AbortSignal) =>
    apiRequest<DepartmentDirectory>(base(projectId), { signal, query: { includeArchived: includeArchived || undefined } }),
  options: (projectId: string, signal?: AbortSignal) => apiRequest<DepartmentEditorOptions>(`${base(projectId)}/options`, { signal }),
  create: (projectId: string, input: CreateDepartmentInput) =>
    apiRequest<DepartmentEditResult>(base(projectId), { method: 'POST', body: input }),
  published: (projectId: string, departmentId: string, signal?: AbortSignal) =>
    apiRequest<PublishedDepartment>(one(projectId, departmentId), { signal }),
  tasks: (projectId: string, departmentId: string, filters: DepartmentTaskFilters, signal?: AbortSignal) =>
    apiRequest<PagedResult<TaskSummary>>(`${one(projectId, departmentId)}/tasks`, { signal, query: { ...filters } }),
  task: (projectId: string, departmentId: string, taskId: string, signal?: AbortSignal) =>
    apiRequest<TaskDetail>(`${one(projectId, departmentId)}/tasks/${taskId}`, { signal }),
  revisions: (projectId: string, departmentId: string, signal?: AbortSignal) =>
    apiRequest<DepartmentRevision[]>(`${one(projectId, departmentId)}/revisions`, { signal }),
  draft: (projectId: string, departmentId: string, signal?: AbortSignal) =>
    apiRequest<DepartmentDraft>(`${one(projectId, departmentId)}/draft`, { signal }),
  saveDraft: (projectId: string, departmentId: string, expectedEditVersion: number, config: DepartmentConfig) =>
    apiRequest<DepartmentDraft>(`${one(projectId, departmentId)}/draft`, { method: 'PUT', body: { expectedEditVersion, config } }),
  publish: (projectId: string, departmentId: string, expectedEditVersion: number) =>
    apiRequest<DepartmentEditResult>(`${one(projectId, departmentId)}/publish`, { method: 'POST', body: { expectedEditVersion } }),
  restore: (projectId: string, departmentId: string, version: number, expectedEditVersion: number) =>
    apiRequest<DepartmentDraft>(`${one(projectId, departmentId)}/revisions/${version}/restore`, {
      method: 'POST',
      body: { expectedEditVersion },
    }),
  archive: (projectId: string, departmentId: string, expectedEditVersion: number) =>
    apiRequest<DepartmentEditResult>(`${one(projectId, departmentId)}/archive`, { method: 'POST', body: { expectedEditVersion } }),
  taskApprovals: (taskId: string, signal?: AbortSignal) =>
    apiRequest<PagedResult<Approval>>('/approvals', { signal, query: { taskId, pageSize: 10 } }),
}

const LIVE_REFRESH_MS = 15_000

export function useDepartmentDirectory(projectId: string, includeArchived = false) {
  return useQuery({
    queryKey: officeKeys.directory(projectId, includeArchived),
    queryFn: ({ signal }) => officeApi.directory(projectId, includeArchived, signal),
    enabled: Boolean(projectId),
  })
}

export function useDepartmentOptions(projectId: string) {
  // Limits and templates are static per deployment.
  return useQuery({ queryKey: officeKeys.options(projectId), queryFn: ({ signal }) => officeApi.options(projectId, signal), staleTime: Infinity })
}

export function usePublishedDepartment(projectId: string, departmentId: string) {
  return useQuery({
    queryKey: officeKeys.published(projectId, departmentId),
    queryFn: ({ signal }) => officeApi.published(projectId, departmentId, signal),
    enabled: Boolean(projectId && departmentId),
  })
}

export function useDepartmentTasks(projectId: string, departmentId: string, filters: DepartmentTaskFilters, enabled = true) {
  return useQuery({
    queryKey: officeKeys.tasks(projectId, departmentId, filters),
    queryFn: ({ signal }) => officeApi.tasks(projectId, departmentId, filters, signal),
    placeholderData: keepPreviousData,
    refetchInterval: LIVE_REFRESH_MS,
    enabled: enabled && Boolean(projectId && departmentId),
  })
}

export function useDepartmentTask(projectId: string, departmentId: string, taskId: string | undefined) {
  return useQuery({
    queryKey: officeKeys.task(projectId, departmentId, taskId ?? ''),
    queryFn: ({ signal }) => officeApi.task(projectId, departmentId, taskId!, signal),
    enabled: Boolean(projectId && departmentId && taskId),
    refetchInterval: LIVE_REFRESH_MS,
    // A task outside the department (404) or revoked access (403) will not recover by retrying.
    retry: (count, error) => count < 2 && !(error instanceof ApiError && (error.isNotFound || error.isForbidden)),
  })
}

export function useTaskApprovals(taskId: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: officeKeys.taskApprovals(taskId ?? ''),
    queryFn: ({ signal }) => officeApi.taskApprovals(taskId!, signal),
    enabled: enabled && Boolean(taskId),
  })
}

export function useDepartmentRevisions(projectId: string, departmentId: string) {
  return useQuery({
    queryKey: officeKeys.revisions(projectId, departmentId),
    queryFn: ({ signal }) => officeApi.revisions(projectId, departmentId, signal),
  })
}

export function useDepartmentDraft(projectId: string, departmentId: string) {
  return useQuery({
    queryKey: officeKeys.draft(projectId, departmentId),
    queryFn: ({ signal }) => officeApi.draft(projectId, departmentId, signal),
    // The editor owns its local copy; background refetches must not overwrite unsaved work.
    refetchOnWindowFocus: false,
    staleTime: Infinity,
  })
}

export function useCreateDepartment(projectId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: CreateDepartmentInput) => officeApi.create(projectId, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: officeKeys.project(projectId) }),
  })
}

/** Draft, publish, restore and archive share one invalidation rule: everything about this department and the directory. */
export function useDepartmentCommands(projectId: string, departmentId: string) {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: officeKeys.project(projectId) })
  const setDraft = (draft: DepartmentDraft) => queryClient.setQueryData(officeKeys.draft(projectId, departmentId), draft)

  return {
    save: useMutation({
      mutationFn: ({ editVersion, config }: { editVersion: number; config: DepartmentConfig }) =>
        officeApi.saveDraft(projectId, departmentId, editVersion, config),
      onSuccess: (draft) => {
        setDraft(draft)
        void queryClient.invalidateQueries({ queryKey: officeKeys.directory(projectId, false) })
      },
    }),
    publish: useMutation({
      mutationFn: (editVersion: number) => officeApi.publish(projectId, departmentId, editVersion),
      onSuccess: invalidate,
    }),
    restore: useMutation({
      mutationFn: ({ version, editVersion }: { version: number; editVersion: number }) =>
        officeApi.restore(projectId, departmentId, version, editVersion),
      onSuccess: (draft) => {
        setDraft(draft)
        void invalidate()
      },
    }),
    archive: useMutation({
      mutationFn: (editVersion: number) => officeApi.archive(projectId, departmentId, editVersion),
      onSuccess: invalidate,
    }),
  }
}
