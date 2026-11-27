import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { projectKeys } from '@/features/projects/api'
import { apiRequest } from '@/shared/api/http'
import type { RepositoryWorkflow, WorkflowDefinition, WorkflowRevision } from '@/shared/api/types'

export const workflowKeys = {
  detail: (repositoryId: string) => ['workflows', repositoryId] as const,
  revisions: (repositoryId: string) => ['workflows', repositoryId, 'revisions'] as const,
}

export interface UpdateWorkflowInput {
  expectedVersion: number
  definition: WorkflowDefinition
  comment: string
}

export const workflowsApi = {
  get: (repositoryId: string, signal?: AbortSignal) => apiRequest<RepositoryWorkflow>(`/repositories/${repositoryId}/workflow`, { signal }),
  revisions: (repositoryId: string, signal?: AbortSignal) =>
    apiRequest<WorkflowRevision[]>(`/repositories/${repositoryId}/workflow/revisions`, { signal }),
  update: (repositoryId: string, input: UpdateWorkflowInput) =>
    apiRequest<RepositoryWorkflow>(`/repositories/${repositoryId}/workflow`, { method: 'PUT', body: input }),
}

export function useWorkflow(repositoryId: string) {
  return useQuery({ queryKey: workflowKeys.detail(repositoryId), queryFn: ({ signal }) => workflowsApi.get(repositoryId, signal) })
}

export function useWorkflowRevisions(repositoryId: string) {
  return useQuery({ queryKey: workflowKeys.revisions(repositoryId), queryFn: ({ signal }) => workflowsApi.revisions(repositoryId, signal) })
}

export function useUpdateWorkflow(repositoryId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: UpdateWorkflowInput) => workflowsApi.update(repositoryId, input),
    onSuccess: (data) => {
      queryClient.setQueryData(workflowKeys.detail(repositoryId), data)
      void queryClient.invalidateQueries({ queryKey: workflowKeys.revisions(repositoryId) })
      void queryClient.invalidateQueries({ queryKey: projectKeys.all })
    },
  })
}
