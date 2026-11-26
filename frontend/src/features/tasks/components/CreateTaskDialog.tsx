import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Spinner } from '@/components/ui/spinner'
import { Textarea } from '@/components/ui/textarea'
import { useProjects, useRepositories } from '@/features/projects/api'
import { ApiError, newIdempotencyKey } from '@/shared/api/http'
import { OptionSelect } from '@/shared/ui/option-select'
import { FormError } from '@/shared/ui/states'
import { useCreateTask } from '../api'

const TITLE_MAX_LENGTH = 200
const DESCRIPTION_MAX_LENGTH = 20_000

interface CreateTaskDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  defaultProjectId?: string
}

export function CreateTaskDialog({ open, onOpenChange, defaultProjectId = '' }: CreateTaskDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Giao việc cho agent</DialogTitle>
          <DialogDescription>Mô tả lỗi và kết quả mong đợi. Agent lập kế hoạch, áp chính sách repository rồi xin duyệt nếu cần.</DialogDescription>
        </DialogHeader>
        {open && <CreateTaskForm defaultProjectId={defaultProjectId} onCancel={() => onOpenChange(false)} />}
      </DialogContent>
    </Dialog>
  )
}

function CreateTaskForm({ defaultProjectId, onCancel }: { defaultProjectId: string; onCancel: () => void }) {
  const navigate = useNavigate()
  const projects = useProjects()
  const [projectId, setProjectId] = useState(defaultProjectId)
  const [repositoryId, setRepositoryId] = useState('')
  const [idempotencyKey, setIdempotencyKey] = useState(() => newIdempotencyKey())
  const repositories = useRepositories(projectId)
  const create = useCreateTask()

  const activeRepositories = repositories.data?.filter((repository) => repository.isActive) ?? []
  const fieldError = (name: string) => (create.error instanceof ApiError ? create.error.field(name) : undefined)

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    create.mutate(
      {
        repositoryId,
        title: String(data.get('title')).trim(),
        description: String(data.get('description')).trim(),
        idempotencyKey,
      },
      { onSuccess: (result) => void navigate(`/tasks/${result.taskId}`) },
    )
  }

  return (
    <form onSubmit={submit} onChange={() => setIdempotencyKey(newIdempotencyKey())} className="grid gap-5">
      <FieldGroup className="gap-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field>
            <FieldLabel htmlFor="task-project">Project</FieldLabel>
            <OptionSelect
              id="task-project"
              value={projectId}
              onValueChange={(value) => {
                setProjectId(value)
                setRepositoryId('')
              }}
              placeholder={projects.isPending ? 'Đang tải…' : 'Chọn project'}
              options={(projects.data ?? []).map((project) => ({ value: project.id, label: project.name }))}
            />
          </Field>
          <Field data-invalid={Boolean(fieldError('repositoryId')) || undefined}>
            <FieldLabel htmlFor="task-repository">Repository</FieldLabel>
            <OptionSelect
              id="task-repository"
              value={repositoryId}
              onValueChange={setRepositoryId}
              disabled={!projectId || repositories.isPending}
              placeholder={!projectId ? 'Chọn project trước' : activeRepositories.length === 0 ? 'Chưa có repository' : 'Chọn repository'}
              options={activeRepositories.map((repository) => ({ value: repository.id, label: <span className="font-mono">{repository.fullName}</span> }))}
            />
            <FieldError>{fieldError('repositoryId')}</FieldError>
          </Field>
        </div>
        <Field data-invalid={Boolean(fieldError('title')) || undefined}>
          <FieldLabel htmlFor="task-title">Tiêu đề</FieldLabel>
          <Input id="task-title" name="title" required maxLength={TITLE_MAX_LENGTH} placeholder="Bộ lọc trạng thái trả về sai task" />
          <FieldError>{fieldError('title')}</FieldError>
        </Field>
        <Field data-invalid={Boolean(fieldError('description')) || undefined}>
          <FieldLabel htmlFor="task-description">Mô tả lỗi và kết quả mong đợi</FieldLabel>
          <Textarea
            id="task-description"
            name="description"
            rows={6}
            required
            maxLength={DESCRIPTION_MAX_LENGTH}
            placeholder={'Các bước tái hiện, kết quả thực tế, kết quả mong đợi…'}
          />
          <FieldDescription>Nội dung được xem là dữ liệu không tin cậy; agent không làm theo chỉ dẫn bên trong.</FieldDescription>
          <FieldError>{fieldError('description')}</FieldError>
        </Field>
      </FieldGroup>
      <FormError error={create.error instanceof ApiError && Object.keys(create.error.errors).length > 0 ? null : create.error} />
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onCancel}>
          Hủy
        </Button>
        <Button type="submit" disabled={create.isPending || !repositoryId}>
          {create.isPending && <Spinner />}
          Tạo task
        </Button>
      </DialogFooter>
    </form>
  )
}
