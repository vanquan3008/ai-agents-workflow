import { useState, type FormEvent } from 'react'
import { UserPlus } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Spinner } from '@/components/ui/spinner'
import { ApiError } from '@/shared/api/http'
import type { AdminUser } from '@/shared/api/types'
import { OptionSelect } from '@/shared/ui/option-select'
import { FormError } from '@/shared/ui/states'
import { useCreateUser } from '../api'

export function CreateUserDialog() {
  const [open, setOpen] = useState(false)
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <UserPlus /> Tạo người dùng
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Tạo người dùng</DialogTitle>
          <DialogDescription>Người dùng nhận mật khẩu tạm và phải đổi ở lần đăng nhập đầu.</DialogDescription>
        </DialogHeader>
        {open && <CreateUserForm onDone={() => setOpen(false)} />}
      </DialogContent>
    </Dialog>
  )
}

function CreateUserForm({ onDone }: { onDone: () => void }) {
  const create = useCreateUser()
  const [systemRole, setSystemRole] = useState<AdminUser['systemRole']>('USER')
  const fieldError = (field: string) => (create.error instanceof ApiError ? create.error.field(field) : undefined)

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    create.mutate(
      {
        email: String(data.get('email')).trim(),
        displayName: String(data.get('displayName')).trim(),
        systemRole,
        temporaryPassword: String(data.get('temporaryPassword')),
      },
      {
        onSuccess: (user) => {
          toast.success(`Đã tạo tài khoản ${user.email}.`)
          onDone()
        },
      },
    )
  }

  return (
    <form className="grid gap-5" onSubmit={submit}>
      <FieldGroup className="gap-4">
        <Field data-invalid={Boolean(fieldError('email')) || undefined}>
          <FieldLabel htmlFor="new-user-email">Email</FieldLabel>
          <Input id="new-user-email" name="email" type="email" required />
          <FieldError>{fieldError('email')}</FieldError>
        </Field>
        <Field data-invalid={Boolean(fieldError('displayName')) || undefined}>
          <FieldLabel htmlFor="new-user-name">Tên hiển thị</FieldLabel>
          <Input id="new-user-name" name="displayName" required maxLength={200} />
          <FieldError>{fieldError('displayName')}</FieldError>
        </Field>
        <Field>
          <FieldLabel htmlFor="new-user-role">Quyền hệ thống</FieldLabel>
          <OptionSelect
            id="new-user-role"
            value={systemRole}
            onValueChange={(value) => setSystemRole(value as AdminUser['systemRole'])}
            options={[
              { value: 'USER', label: 'User' },
              { value: 'SUPER_ADMIN', label: 'Super Admin' },
            ]}
          />
        </Field>
        <Field data-invalid={Boolean(fieldError('temporaryPassword')) || undefined}>
          <FieldLabel htmlFor="new-user-password">Mật khẩu tạm</FieldLabel>
          <Input id="new-user-password" name="temporaryPassword" type="password" autoComplete="new-password" required />
          <FieldDescription>Tối thiểu 10 ký tự, có cả chữ và số.</FieldDescription>
          <FieldError>{fieldError('temporaryPassword')}</FieldError>
        </Field>
      </FieldGroup>
      <FormError error={create.error instanceof ApiError && Object.keys(create.error.errors).length > 0 ? null : create.error} />
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onDone}>
          Hủy
        </Button>
        <Button type="submit" disabled={create.isPending}>
          {create.isPending && <Spinner />}
          Tạo tài khoản
        </Button>
      </DialogFooter>
    </form>
  )
}
