import { Ban, RotateCw } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import type { TaskDetail } from '@/shared/api/types'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { useCancelTask, useRetryTask } from '../api'

/** Only offers transitions the backend reports as allowed for the current status. */
export function TaskActions({ task }: { task: TaskDetail }) {
  const cancel = useCancelTask(task.id)
  const retry = useRetryTask(task.id)
  const canRetry = task.allowedNextStatuses.includes('PENDING')
  const canCancel = task.allowedNextStatuses.includes('CANCELLED')

  if (!canRetry && !canCancel) return null

  return (
    <>
      {canRetry && (
        <Button
          variant="outline"
          disabled={retry.isPending}
          onClick={() => retry.mutate(undefined, { onSuccess: () => toast.success('Đã xếp task vào hàng đợi để thử lại.') })}
        >
          <RotateCw /> Thử lại
        </Button>
      )}
      {canCancel && (
        <ConfirmDialog
          trigger={
            <Button variant="destructive" disabled={cancel.isPending}>
              <Ban /> Hủy task
            </Button>
          }
          title="Hủy task này?"
          description="Worker dừng ở bước kế tiếp và các yêu cầu duyệt còn mở sẽ hết hiệu lực. Không thể hoàn tác."
          confirmLabel="Hủy task"
          destructive
          onConfirm={() => cancel.mutate(undefined, { onSuccess: () => toast.success('Đã hủy task.') })}
        />
      )}
    </>
  )
}
