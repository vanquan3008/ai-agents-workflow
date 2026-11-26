import { useState, type FormEvent } from 'react'
import { Save, Undo2 } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Field, FieldLabel } from '@/components/ui/field'
import { Spinner } from '@/components/ui/spinner'
import { Textarea } from '@/components/ui/textarea'
import type { RepositoryWorkflow, WorkflowDefinition } from '@/shared/api/types'
import { FormError } from '@/shared/ui/states'
import { useUpdateWorkflow } from '../api'
import { ApprovalSettingsSection } from './ApprovalSettingsSection'
import { ModelOverridesSection } from './ModelOverridesSection'
import { NotificationsSection } from './NotificationsSection'
import { PullRequestSection } from './PullRequestSection'

const COMMENT_MAX_LENGTH = 2000

/** Edits a draft copy of the workflow; saving creates a new immutable version guarded by the expected version. */
export function WorkflowEditor({ workflow }: { workflow: RepositoryWorkflow }) {
  const update = useUpdateWorkflow(workflow.repositoryId)
  const [definition, setDefinition] = useState<WorkflowDefinition>(() => structuredClone(workflow.definition))
  const [comment, setComment] = useState('')
  const isDirty = JSON.stringify(definition) !== JSON.stringify(workflow.definition)

  const patch = (changes: Partial<WorkflowDefinition>) => setDefinition((current) => ({ ...current, ...changes }))

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    update.mutate(
      { expectedVersion: workflow.version, definition, comment: comment.trim() },
      { onSuccess: (saved) => toast.success(`Đã lưu quy trình v${saved.version}.`) },
    )
  }

  return (
    <form className="min-w-0 space-y-4" onSubmit={submit}>
      {/* Fieldsets default to min-width: min-content, which would stretch the page on narrow screens. */}
      <fieldset className="min-w-0 space-y-4" disabled={update.isPending}>
        <ApprovalSettingsSection definition={definition} limits={workflow.limits} onChange={patch} />
        <PullRequestSection definition={definition} onChange={patch} />
        <ModelOverridesSection definition={definition} onChange={patch} />
        <NotificationsSection definition={definition} limits={workflow.limits} onChange={patch} />
      </fieldset>

      <div className="bg-card/95 supports-backdrop-filter:bg-card/80 sticky bottom-3 z-10 space-y-3 rounded-lg border p-4 shadow-md backdrop-blur">
        <Field>
          <FieldLabel htmlFor="workflow-comment">Lý do thay đổi</FieldLabel>
          <Textarea
            id="workflow-comment"
            value={comment}
            onChange={(event) => setComment(event.target.value)}
            required
            rows={2}
            maxLength={COMMENT_MAX_LENGTH}
            placeholder="Ví dụ: luôn duyệt kế hoạch trong giai đoạn pilot"
          />
        </Field>
        <FormError error={update.error} />
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-muted-foreground text-xs">{isDirty ? 'Có thay đổi chưa lưu.' : 'Chưa có thay đổi.'}</p>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="ghost"
              disabled={!isDirty || update.isPending}
              onClick={() => setDefinition(structuredClone(workflow.definition))}
            >
              <Undo2 /> Hoàn tác
            </Button>
            <Button type="submit" disabled={update.isPending || !isDirty || !comment.trim()}>
              {update.isPending ? <Spinner /> : <Save />}
              Lưu phiên bản v{workflow.version + 1}
            </Button>
          </div>
        </div>
      </div>
    </form>
  )
}
