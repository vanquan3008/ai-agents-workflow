import { useState, type FormEvent } from 'react'
import { Plus } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Spinner } from '@/components/ui/spinner'
import { ApiError } from '@/shared/api/http'
import { FormError } from '@/shared/ui/states'
import { useRegisterRepository } from '../api'

export function RegisterRepositoryDialog({ projectId }: { projectId: string }) {
  const [open, setOpen] = useState(false)
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm">
          <Plus /> Đăng ký repository
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Đăng ký repository</DialogTitle>
          <DialogDescription>Repository dùng chính sách an toàn mặc định; vùng nhạy cảm như xác thực, migration, CI/CD luôn cần duyệt.</DialogDescription>
        </DialogHeader>
        {open && <RegisterRepositoryForm projectId={projectId} onDone={() => setOpen(false)} />}
      </DialogContent>
    </Dialog>
  )
}

function RegisterRepositoryForm({ projectId, onDone }: { projectId: string; onDone: () => void }) {
  const register = useRegisterRepository(projectId)
  const fieldError = (field: string) => (register.error instanceof ApiError ? register.error.field(field) : undefined)

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    register.mutate(
      {
        installationId: Number(data.get('installationId')),
        owner: String(data.get('owner')).trim(),
        name: String(data.get('name')).trim(),
        defaultBranch: String(data.get('defaultBranch')).trim(),
        buildCommand: String(data.get('buildCommand')).trim(),
        testCommand: String(data.get('testCommand')).trim(),
      },
      {
        onSuccess: (repository) => {
          toast.success(`Đã đăng ký ${repository.fullName}.`)
          onDone()
        },
      },
    )
  }

  return (
    <form onSubmit={submit} className="grid gap-5">
      <FieldGroup className="gap-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field data-invalid={Boolean(fieldError('owner')) || undefined}>
            <FieldLabel htmlFor="repo-owner">Owner trên GitHub</FieldLabel>
            <Input id="repo-owner" name="owner" required className="font-mono" placeholder="acme" />
            <FieldError>{fieldError('owner')}</FieldError>
          </Field>
          <Field data-invalid={Boolean(fieldError('name')) || undefined}>
            <FieldLabel htmlFor="repo-name">Tên repository</FieldLabel>
            <Input id="repo-name" name="name" required className="font-mono" placeholder="web-app" />
            <FieldError>{fieldError('name')}</FieldError>
          </Field>
          <Field data-invalid={Boolean(fieldError('installationId')) || undefined}>
            <FieldLabel htmlFor="repo-installation">GitHub App installation ID</FieldLabel>
            <Input id="repo-installation" name="installationId" type="number" min={1} required className="font-mono" />
            <FieldError>{fieldError('installationId')}</FieldError>
          </Field>
          <Field>
            <FieldLabel htmlFor="repo-branch">Nhánh mặc định</FieldLabel>
            <Input id="repo-branch" name="defaultBranch" defaultValue="main" required className="font-mono" />
          </Field>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field>
            <FieldLabel htmlFor="repo-build">Lệnh build</FieldLabel>
            <Input id="repo-build" name="buildCommand" defaultValue="dotnet build" required className="font-mono" />
          </Field>
          <Field>
            <FieldLabel htmlFor="repo-test">Lệnh test</FieldLabel>
            <Input id="repo-test" name="testCommand" defaultValue="dotnet test" required className="font-mono" />
          </Field>
        </div>
        <FieldDescription>Lệnh chỉ chạy trong sandbox cách ly, không bao giờ trên máy chủ.</FieldDescription>
      </FieldGroup>
      <FormError error={register.error instanceof ApiError && Object.keys(register.error.errors).length > 0 ? null : register.error} />
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onDone}>
          Hủy
        </Button>
        <Button type="submit" disabled={register.isPending}>
          {register.isPending && <Spinner />}
          Đăng ký
        </Button>
      </DialogFooter>
    </form>
  )
}
