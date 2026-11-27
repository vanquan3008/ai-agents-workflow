import { useState, type FormEvent } from 'react'
import { Plus } from 'lucide-react'
import { useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel, FieldLegend, FieldSet } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Spinner } from '@/components/ui/spinner'
import { cn } from '@/lib/utils'
import { useRepositories } from '@/features/projects/api'
import { ApiError } from '@/shared/api/http'
import { FormError, LoadingBlock } from '@/shared/ui/states'
import { useCreateDepartment, useDepartmentOptions } from '../../api'
import { slugify } from '../../model/layout'
import { themeStyle } from '../../model/presentation'

export function CreateDepartmentDialog({ projectId }: { projectId: string }) {
  const [open, setOpen] = useState(false)
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm">
          <Plus /> Tạo phòng ban
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Tạo phòng ban</DialogTitle>
          <DialogDescription>Bắt đầu từ một mẫu có đủ sáu bước quy trình. Phòng ban mới là bản nháp riêng cho tới khi bạn xuất bản.</DialogDescription>
        </DialogHeader>
        {open && <CreateDepartmentForm projectId={projectId} onCancel={() => setOpen(false)} />}
      </DialogContent>
    </Dialog>
  )
}

function CreateDepartmentForm({ projectId, onCancel }: { projectId: string; onCancel: () => void }) {
  const navigate = useNavigate()
  const options = useDepartmentOptions(projectId)
  const repositories = useRepositories(projectId)
  const create = useCreateDepartment(projectId)
  const [templateKey, setTemplateKey] = useState<string>()
  const [name, setName] = useState('')
  const [slug, setSlug] = useState('')
  const [slugEdited, setSlugEdited] = useState(false)
  const [selectedRepositories, setSelectedRepositories] = useState<Set<string>>()

  if (options.isPending || repositories.isPending) return <LoadingBlock rows={4} />
  if (options.isError || repositories.isError) return <FormError error={options.error ?? repositories.error} />

  const template = options.data.templates.find((t) => t.key === templateKey) ?? options.data.templates[0]
  // Every active repository is followed until the user narrows the selection.
  const chosen = selectedRepositories ?? new Set(repositories.data.filter((r) => r.isActive).map((r) => r.id))
  const effectiveName = name.trim() || template?.name || ''
  const effectiveSlug = slugEdited ? slug : slugify(effectiveName)
  const error = create.error instanceof ApiError ? create.error : null

  const toggleRepository = (id: string, checked: boolean) => {
    const next = new Set(chosen)
    if (checked) next.add(id)
    else next.delete(id)
    setSelectedRepositories(next)
  }

  const onSubmit = (event: FormEvent) => {
    event.preventDefault()
    if (!template) return
    create.mutate(
      { template: template.key, name: effectiveName, slug: effectiveSlug, repositoryIds: [...chosen] },
      { onSuccess: (created) => void navigate(`/projects/${projectId}/office/${created.id}/settings`) },
    )
  }

  return (
    <form onSubmit={onSubmit} className="space-y-5">
      <FieldGroup>
        <FieldSet>
          <FieldLegend variant="label">Mẫu</FieldLegend>
          <div className="grid gap-2 sm:grid-cols-3">
            {options.data.templates.map((t) => (
              <label
                key={t.key}
                style={themeStyle(t.config.theme)}
                className={cn(
                  'has-focus-visible:ring-ring cursor-pointer rounded-lg border p-3 transition-colors has-focus-visible:ring-[3px]',
                  t.key === template?.key ? 'border-[color:var(--office-accent)] bg-accent/60' : 'hover:bg-accent/40',
                )}
              >
                <input
                  type="radio"
                  name="department-template"
                  value={t.key}
                  checked={t.key === template?.key}
                  onChange={() => setTemplateKey(t.key)}
                  className="sr-only"
                />
                <span className="mb-1 flex items-center gap-2 text-sm font-medium">
                  <span className="size-2.5 rounded-full bg-[color:var(--office-accent)]" aria-hidden="true" />
                  {t.name}
                </span>
                <span className="text-muted-foreground block text-xs leading-snug">{t.description}</span>
              </label>
            ))}
          </div>
        </FieldSet>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field data-invalid={Boolean(error?.field('name'))}>
            <FieldLabel htmlFor="department-name">Tên phòng ban</FieldLabel>
            <Input
              id="department-name"
              value={name}
              placeholder={template?.name}
              maxLength={options.data.nameMaxLength}
              onChange={(event) => setName(event.target.value)}
            />
          </Field>
          <Field data-invalid={Boolean(error?.field('slug')) || error?.code === 'department.slug_taken'}>
            <FieldLabel htmlFor="department-slug">Slug</FieldLabel>
            <Input
              id="department-slug"
              value={effectiveSlug}
              required
              maxLength={64}
              pattern="[a-z0-9]+(-[a-z0-9]+)*"
              className="font-mono"
              onChange={(event) => {
                setSlugEdited(true)
                setSlug(event.target.value.toLowerCase())
              }}
            />
            <FieldDescription>Chữ thường, số và dấu gạch ngang. Không đổi sau khi tạo.</FieldDescription>
            {error?.field('slug') && <FieldError>{error.field('slug')}</FieldError>}
          </Field>
        </div>

        <FieldSet>
          <FieldLegend variant="label">Repository theo dõi</FieldLegend>
          {repositories.data.length === 0 ? (
            <FieldDescription>Project chưa có repository. Bạn vẫn có thể tạo bản nháp và chọn repository sau.</FieldDescription>
          ) : (
            <ul className="max-h-44 space-y-2 overflow-y-auto rounded-md border p-3">
              {repositories.data.map((repository) => (
                <li key={repository.id} className="flex items-center gap-2">
                  <Checkbox
                    id={`repo-${repository.id}`}
                    checked={chosen.has(repository.id)}
                    onCheckedChange={(checked) => toggleRepository(repository.id, checked === true)}
                  />
                  <label htmlFor={`repo-${repository.id}`} className="min-w-0 truncate text-sm">
                    {repository.fullName}
                    {!repository.isActive && <span className="text-muted-foreground"> · ngừng hoạt động</span>}
                  </label>
                </li>
              ))}
            </ul>
          )}
        </FieldSet>
      </FieldGroup>

      {create.error && !error?.field('slug') && <FormError error={create.error} />}

      <DialogFooter>
        <Button type="button" variant="outline" onClick={onCancel}>
          Hủy bỏ
        </Button>
        <Button type="submit" disabled={create.isPending || !template || !effectiveSlug}>
          {create.isPending && <Spinner />} Tạo và mở trình chỉnh sửa
        </Button>
      </DialogFooter>
    </form>
  )
}
