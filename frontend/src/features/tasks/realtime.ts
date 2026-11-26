import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { taskHub } from '../../shared/realtime/taskHub'
import { taskKeys } from './api'

/**
 * Nhận cập nhật realtime cho một task. Mỗi sự kiện chỉ là tín hiệu: dữ liệu luôn đọc lại từ API,
 * nên sau khi mất kết nối, timeline được khôi phục đầy đủ và đúng thứ tự từ lịch sử đã lưu.
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
      // Không kết nối được realtime: màn hình vẫn dùng dữ liệu API (có thể làm mới thủ công).
    })

    return () => {
      offEvent()
      offReconnect()
      void taskHub.unsubscribeTask(taskId).catch(() => undefined)
    }
  }, [queryClient, taskId])
}
