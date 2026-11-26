import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiRequest } from '@/shared/api/http'
import type { AdminUser, PagedResult } from '@/shared/api/types'

export const userKeys = {
  all: ['admin-users'] as const,
  list: (search: string, page: number) => ['admin-users', search, page] as const,
}

export interface CreateUserInput {
  email: string
  displayName: string
  systemRole: 'USER' | 'SUPER_ADMIN'
  temporaryPassword: string
}

export interface UpdateUserInput {
  displayName?: string
  systemRole?: 'USER' | 'SUPER_ADMIN'
  isActive?: boolean
}

export const usersApi = {
  list: (search: string, page: number, signal?: AbortSignal) =>
    apiRequest<PagedResult<AdminUser>>('/admin/users', { signal, query: { search, page, pageSize: 20 } }),
  create: (input: CreateUserInput) => apiRequest<AdminUser>('/admin/users', { method: 'POST', body: input }),
  update: (id: string, input: UpdateUserInput) => apiRequest<AdminUser>(`/admin/users/${id}`, { method: 'PATCH', body: input }),
  resetPassword: (id: string, temporaryPassword: string) =>
    apiRequest<void>(`/admin/users/${id}/reset-password`, { method: 'POST', body: { temporaryPassword } }),
}

export function useUsers(search: string, page: number) {
  return useQuery({
    queryKey: userKeys.list(search, page),
    queryFn: ({ signal }) => usersApi.list(search, page, signal),
    placeholderData: keepPreviousData,
  })
}

function useUserMutation<TArgs, TResult>(fn: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries({ queryKey: userKeys.all }) })
}

export const useCreateUser = () => useUserMutation(usersApi.create)

export const useUpdateUser = () => useUserMutation(({ id, ...input }: UpdateUserInput & { id: string }) => usersApi.update(id, input))

export const useResetPassword = () =>
  useUserMutation(({ id, temporaryPassword }: { id: string; temporaryPassword: string }) => usersApi.resetPassword(id, temporaryPassword))
