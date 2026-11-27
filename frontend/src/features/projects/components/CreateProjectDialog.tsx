import { useState, type FormEvent } from 'react'
import { Plus } from 'lucide-react'
import { useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Spinner } from '@/components/ui/spinner'
import { ApiError } from '@/shared/api/http'
import { FormError } from '@/shared/ui/states'
import { useCreateProject } from '../api'

const SLUG_PATTERN = '[a-z0-9]+(-[a-z0-9]+)*'

const toSlug = (name: string) =>
  name
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/gi, 'd')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 64)

export function CreateProjectDialog() {
  const [open, setOpen] = useState(false)
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus /> Tạo project
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Tạo project</DialogTitle>
          <DialogDescription>Project là ranh giới phân quyền cho repository, task và người duyệt.</DialogDescription>
        </DialogHeader>
        {open && <CreateProjectForm onCancel={() => setOpen(false)} />}
      </DialogContent>
    </Dialog>
  )
}

function CreateProjectForm({ onCancel }: { onCancel: () => void }) {
  const navigate = useNavigate()
  const create = useCreateProject()
  const [name, setName] = useState('')
  const [slug, setSlug] = useState('')
  const [slugEdited, setSlugEdited] = useState(false)
  const fieldError = (field: string) => (create.error instanceof ApiError ? create.error.field(field) : undefined)

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    create.mutate(
      { name: name.trim(), slug: slug.trim(), ownerEmail: String(data.get('ownerEmail')).trim() || undefined },
      { onSuccess: (project) => void navigate(`/projects/${project.id}`) },
    )
  }

  return (
    <form className="grid gap-5" onSubmit={submit}>
      <FieldGroup className="gap-4">
        <Field>
          <FieldLabel htmlFor="project-name">Tên project</FieldLabel>
          <Input
            id="project-name"
            required
            maxLength={200}
            value={name}
            onChange={(event) => {
              setName(event.target.value)
              if (!slugEdited) setSlug(toSlug(event.target.value))
            }}
          />
        </Field>
        <Field data-invalid={Boolean(fieldError('slug')) || undefined}>
          <FieldLabel htmlFor="project-slug">Slug</FieldLabel>
          <Input
            id="project-slug"
            required
            pattern={SLUG_PATTERN}
            maxLength={64}
            className="font-mono"
            value={slug}
            onChange={(event) => {
              setSlugEdited(true)
              setSlug(event.target.value)
            }}
          />
          <FieldDescription>Chữ thường, số và dấu gạch ngang.</FieldDescription>
          <FieldError>{fieldError('slug')}</FieldError>
        </Field>
        <Field data-invalid={Boolean(fieldError('ownerEmail')) || undefined}>
          <FieldLabel htmlFor="project-owner">Email Owner</FieldLabel>
          <Input id="project-owner" name="ownerEmail" type="email" placeholder="Để trống để bạn làm Owner" />
          <FieldError>{fieldError('ownerEmail')}</FieldError>
        </Field>
      </FieldGroup>
      <FormError error={create.error instanceof ApiError && Object.keys(create.error.errors).length > 0 ? null : create.error} />
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onCancel}>
          Hủy
        </Button>
        <Button type="submit" disabled={create.isPending}>
          {create.isPending && <Spinner />}
          Tạo project
        </Button>
      </DialogFooter>
    </form>
  )
}
