import { useState } from 'react'
import { Check, Gavel, X } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field'
import { Spinner } from '@/components/ui/spinner'
import { Textarea } from '@/components/ui/textarea'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { Panel } from '@/shared/ui/panel'
import { FormError } from '@/shared/ui/states'
import { useDecideApproval } from '../api'

const COMMENT_MAX_LENGTH = 2000

export function ApprovalDecisionPanel({ approvalId, planVersion }: { approvalId: string; planVersion: number | null }) {
  const decide = useDecideApproval(approvalId)
  const [comment, setComment] = useState('')
  const trimmed = comment.trim()

  return (
    <Panel title="Quyết định" icon={<Gavel />} className="border-primary/30">
      <div className="grid gap-3">
        <Field>
          <FieldLabel htmlFor="approval-comment">Nhận xét</FieldLabel>
          <Textarea
            id="approval-comment"
            value={comment}
            onChange={(event) => setComment(event.target.value)}
            maxLength={COMMENT_MAX_LENGTH}
            rows={4}
            placeholder="Lý do duyệt hoặc điều cần sửa…"
          />
          <FieldDescription>Bắt buộc khi từ chối. Quyết định được ghi audit kèm tên bạn.</FieldDescription>
        </Field>
        <FormError error={decide.error} />
        <div className="grid grid-cols-2 gap-2">
          <ConfirmDialog
            trigger={
              <Button variant="destructive" disabled={decide.isPending || !trimmed}>
                <X /> Từ chối
              </Button>
            }
            title="Từ chối kế hoạch này?"
            description="Task sẽ kết thúc ở trạng thái Bị từ chối kèm lý do của bạn."
            confirmLabel="Từ chối"
            destructive
            onConfirm={() =>
              decide.mutate({ decision: 'reject', comment: trimmed }, { onSuccess: () => toast.success('Đã từ chối yêu cầu.') })
            }
          />
          <Button
            disabled={decide.isPending}
            onClick={() => decide.mutate({ decision: 'approve', comment: trimmed }, { onSuccess: () => toast.success('Đã duyệt. Agent tiếp tục xử lý.') })}
          >
            {decide.isPending ? <Spinner /> : <Check />}
            {planVersion ? `Duyệt v${planVersion}` : 'Duyệt'}
          </Button>
        </div>
      </div>
    </Panel>
  )
}
