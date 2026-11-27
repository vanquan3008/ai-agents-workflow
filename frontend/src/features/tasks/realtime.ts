import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { taskHub } from '@/shared/realtime/taskHub'
import { taskKeys } from './api'

/**
 * Live updates for one task. Events are only refresh hints: data is always re-read from the API, so after a
 * reconnect the timeline is restored completely and in order from persisted history.
 */
export function useTaskRealtime(taskId: string) {
  const queryClient = useQueryClient()

  useEffect(() => {
    const refresh = () => {
      void queryClient.invalidateQueries({ queryKey: taskKeys.detail(taskId) })
      void queryClient.invalidateQueries({ queryKey: taskKeys.timeline(taskId) })
      void queryClient.invalidateQueries({ queryKey: taskKeys.executions(taskId) })
    }

    const offEvent = taskHub.onEvent((event) => {
      if (event.taskId === taskId) refresh()
    })
    const offReconnect = taskHub.onReconnected(refresh)
    taskHub.subscribeTask(taskId).catch(() => {
      // Realtime unavailable: the screen keeps using API data with periodic refetching.
    })

    return () => {
      offEvent()
      offReconnect()
      void taskHub.unsubscribeTask(taskId).catch(() => undefined)
    }
  }, [queryClient, taskId])
}
