import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { taskKeys } from '@/features/tasks/api'
import { apiRequest } from '@/shared/api/http'
import type { Approval, ApprovalStatus, PagedResult } from '@/shared/api/types'

export const approvalKeys = {
  all: ['approvals'] as const,
  list: (status: ApprovalStatus | '', page: number) => ['approvals', 'list', status, page] as const,
  detail: (id: string) => ['approvals', 'detail', id] as const,
}

export const approvalsApi = {
  list: (status: ApprovalStatus | '', page: number, signal?: AbortSignal) =>
    apiRequest<PagedResult<Approval>>('/approvals', { signal, query: { status, page } }),
  get: (id: string, signal?: AbortSignal) => apiRequest<Approval>(`/approvals/${id}`, { signal }),
  approve: (id: string, comment?: string) => apiRequest<Approval>(`/approvals/${id}/approve`, { method: 'POST', body: { comment } }),
  reject: (id: string, comment: string) => apiRequest<Approval>(`/approvals/${id}/reject`, { method: 'POST', body: { comment } }),
}

export function useApprovals(status: ApprovalStatus | '', page = 1) {
  return useQuery({
    queryKey: approvalKeys.list(status, page),
    queryFn: ({ signal }) => approvalsApi.list(status, page, signal),
    refetchInterval: 20_000,
  })
}

export function useApproval(id: string) {
  return useQuery({ queryKey: approvalKeys.detail(id), queryFn: ({ signal }) => approvalsApi.get(id, signal) })
}

export function useDecideApproval(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { decision: 'approve' | 'reject'; comment: string }) =>
      input.decision === 'approve' ? approvalsApi.approve(id, input.comment || undefined) : approvalsApi.reject(id, input.comment),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: approvalKeys.all })
      void queryClient.invalidateQueries({ queryKey: taskKeys.all })
    },
  })
}
