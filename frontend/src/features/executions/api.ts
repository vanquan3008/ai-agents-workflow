import { useQuery } from '@tanstack/react-query'
import { apiRequest } from '@/shared/api/http'
import type { ExecutionDetail } from '@/shared/api/types'

export const executionsApi = {
  get: (id: string, signal?: AbortSignal) => apiRequest<ExecutionDetail>(`/executions/${id}`, { signal }),
}

const RUNNING_REFRESH_MS = 3_000

export const executionKeys = {
  detail: (id: string) => ['executions', id] as const,
}

export function useExecution(id: string) {
  return useQuery({
    queryKey: executionKeys.detail(id),
    queryFn: ({ signal }) => executionsApi.get(id, signal),
    refetchInterval: (query) => (query.state.data?.execution.status === 'RUNNING' ? RUNNING_REFRESH_MS : false),
  })
}
