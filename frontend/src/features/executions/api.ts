import { useQuery } from '@tanstack/react-query'
import { apiRequest } from '@/shared/api/http'
import type { ExecutionDetail } from '@/shared/api/types'

export const executionsApi = {
  get: (id: string, signal?: AbortSignal) => apiRequest<ExecutionDetail>(`/executions/${id}`, { signal }),
}

export function useExecution(id: string) {
  return useQuery({
    queryKey: ['executions', id],
    queryFn: ({ signal }) => executionsApi.get(id, signal),
    refetchInterval: (query) => (query.state.data?.execution.status === 'RUNNING' ? 3_000 : false),
  })
}
