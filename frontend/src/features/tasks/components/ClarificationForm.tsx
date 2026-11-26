import { useState, type FormEvent } from 'react'
import { MessageSquareWarning } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field'
import { Spinner } from '@/components/ui/spinner'
import { Textarea } from '@/components/ui/textarea'
import { Panel } from '@/shared/ui/panel'
import { FormError } from '@/shared/ui/states'
import { useClarifyTask } from '../api'

const ADDITIONAL_INFO_MAX_LENGTH = 20_000

export function ClarificationForm({ taskId, question }: { taskId: string; question: string | null }) {
  const clarify = useClarifyTask(taskId)
  const [additionalInfo, setAdditionalInfo] = useState('')

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    clarify.mutate(additionalInfo.trim(), { onSuccess: () => setAdditionalInfo('') })
  }

  return (
    <Panel
      title="Agent cần thêm thông tin"
      icon={<MessageSquareWarning className="text-warning" />}
      className="border-warning/40 bg-warning/[0.04]"
    >
      <form className="grid gap-3" onSubmit={submit}>
        {question && <p className="text-sm font-medium text-pretty">{question}</p>}
        <Field>
          <FieldLabel htmlFor="additional-info">Bổ sung thông tin</FieldLabel>
          <Textarea
            id="additional-info"
            value={additionalInfo}
            onChange={(event) => setAdditionalInfo(event.target.value)}
            required
            rows={4}
            maxLength={ADDITIONAL_INFO_MAX_LENGTH}
          />
          <FieldDescription>Thông tin được nối vào mô tả và task quay lại bước lập kế hoạch.</FieldDescription>
        </Field>
        <FormError error={clarify.error} />
        <Button className="justify-self-start" disabled={clarify.isPending || !additionalInfo.trim()} type="submit">
          {clarify.isPending && <Spinner />}
          Gửi bổ sung
        </Button>
      </form>
    </Panel>
  )
}
