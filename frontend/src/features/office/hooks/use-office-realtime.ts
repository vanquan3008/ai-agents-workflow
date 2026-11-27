import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { taskKeys } from '@/features/tasks/api'
import { taskHub } from '@/shared/realtime/taskHub'
import { officeKeys } from '../api'

const LIST_REFRESH_DELAY_MS = 1_000

/**
 * Live updates for the office. SignalR events are refresh hints only: the selected task is re-read from the API, and
 * bursts of events collapse into one task-list refresh. After a reconnect everything is re-read, so the floor shows
 * the latest persisted snapshot and the timeline replays missed transitions.
 */
export function useOfficeRealtime(projectId: string, departmentId: string, taskId: string | undefined) {
  const queryClient = useQueryClient()

  useEffect(() => {
    let listTimer: ReturnType<typeof setTimeout> | undefined
    const refreshList = () => {
      clearTimeout(listTimer)
      listTimer = setTimeout(() => {
        void queryClient.invalidateQueries({ queryKey: [...officeKeys.department(projectId, departmentId), 'tasks'] })
      }, LIST_REFRESH_DELAY_MS)
    }
    const refreshTask = () => {
      if (!taskId) return
      void queryClient.invalidateQueries({ queryKey: officeKeys.task(projectId, departmentId, taskId) })
      void queryClient.invalidateQueries({ queryKey: officeKeys.taskApprovals(taskId) })
      void queryClient.invalidateQueries({ queryKey: taskKeys.timeline(taskId) })
      void queryClient.invalidateQueries({ queryKey: taskKeys.executions(taskId) })
    }

    const offEvent = taskHub.onEvent((event) => {
      if (event.taskId === taskId) refreshTask()
      refreshList()
    })
    const offReconnect = taskHub.onReconnected(() => {
      refreshTask()
      refreshList()
    })
    if (taskId) {
      taskHub.subscribeTask(taskId).catch(() => {
        // Realtime unavailable: queries keep refreshing on their interval.
      })
    }

    return () => {
      clearTimeout(listTimer)
      offEvent()
      offReconnect()
      if (taskId) void taskHub.unsubscribeTask(taskId).catch(() => undefined)
    }
  }, [queryClient, projectId, departmentId, taskId])
}
