import { useQuery } from '@tanstack/react-query'
import { apiRequest } from '../../shared/api/http'
import type { Project, Repository } from '../../shared/api/types'

export const projectKeys = {
  all: ['projects'] as const,
  repositories: (projectId: string) => ['projects', projectId, 'repositories'] as const,
}

export const projectsApi = {
  list: (signal?: AbortSignal) => apiRequest<Project[]>('/projects', { signal }),
  repositories: (projectId: string, signal?: AbortSignal) => apiRequest<Repository[]>(`/projects/${projectId}/repositories`, { signal }),
}

export function useProjects() {
  return useQuery({ queryKey: projectKeys.all, queryFn: ({ signal }) => projectsApi.list(signal) })
}

export function useRepositories(projectId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.repositories(projectId ?? ''),
    queryFn: ({ signal }) => projectsApi.repositories(projectId!, signal),
    enabled: Boolean(projectId),
  })
}
