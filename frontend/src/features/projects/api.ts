import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiRequest } from '@/shared/api/http'
import type { Project, ProjectMember, ProjectRole, Repository } from '@/shared/api/types'

export const projectKeys = {
  all: ['projects'] as const,
  detail: (projectId: string) => ['projects', projectId] as const,
  repositories: (projectId: string) => ['projects', projectId, 'repositories'] as const,
  members: (projectId: string) => ['projects', projectId, 'members'] as const,
}

export interface CreateProjectInput {
  name: string
  slug: string
  ownerEmail?: string
}

export interface RegisterRepositoryInput {
  installationId: number
  owner: string
  name: string
  defaultBranch: string
  buildCommand: string
  testCommand: string
}

export const projectsApi = {
  list: (signal?: AbortSignal) => apiRequest<Project[]>('/projects', { signal }),
  get: (projectId: string, signal?: AbortSignal) => apiRequest<Project>(`/projects/${projectId}`, { signal }),
  create: (input: CreateProjectInput) => apiRequest<Project>('/projects', { method: 'POST', body: input }),
  repositories: (projectId: string, signal?: AbortSignal) => apiRequest<Repository[]>(`/projects/${projectId}/repositories`, { signal }),
  registerRepository: (projectId: string, input: RegisterRepositoryInput) =>
    apiRequest<Repository>(`/projects/${projectId}/repositories`, { method: 'POST', body: input }),
  members: (projectId: string, signal?: AbortSignal) => apiRequest<ProjectMember[]>(`/projects/${projectId}/members`, { signal }),
  upsertMember: (projectId: string, email: string, role: ProjectRole) =>
    apiRequest<void>(`/projects/${projectId}/members`, { method: 'PUT', body: { email, role } }),
  removeMember: (projectId: string, userId: string) => apiRequest<void>(`/projects/${projectId}/members/${userId}`, { method: 'DELETE' }),
}

export function useProjects() {
  return useQuery({ queryKey: projectKeys.all, queryFn: ({ signal }) => projectsApi.list(signal) })
}

export function useProject(projectId: string) {
  return useQuery({ queryKey: projectKeys.detail(projectId), queryFn: ({ signal }) => projectsApi.get(projectId, signal), enabled: Boolean(projectId) })
}

export function useRepositories(projectId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.repositories(projectId ?? ''),
    queryFn: ({ signal }) => projectsApi.repositories(projectId!, signal),
    enabled: Boolean(projectId),
  })
}

export function useMembers(projectId: string) {
  return useQuery({ queryKey: projectKeys.members(projectId), queryFn: ({ signal }) => projectsApi.members(projectId, signal) })
}

export function useCreateProject() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: projectsApi.create,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.all }),
  })
}

export function useRegisterRepository(projectId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: RegisterRepositoryInput) => projectsApi.registerRepository(projectId, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.all }),
  })
}

export function useUpsertMember(projectId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ email, role }: { email: string; role: ProjectRole }) => projectsApi.upsertMember(projectId, email, role),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.all }),
  })
}

export function useRemoveMember(projectId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) => projectsApi.removeMember(projectId, userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.all }),
  })
}
