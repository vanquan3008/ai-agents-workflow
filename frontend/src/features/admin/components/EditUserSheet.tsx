import { useState, type FormEvent } from 'react'
import { KeyRound } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Spinner } from '@/components/ui/spinner'
import { Switch } from '@/components/ui/switch'
import { ApiError } from '@/shared/api/http'
import type { AdminUser } from '@/shared/api/types'
import { formatDateTime } from '@/shared/lib/format'
import { OptionSelect } from '@/shared/ui/option-select'
import { FormError } from '@/shared/ui/states'
import { useResetPassword, useUpdateUser } from '../api'

const SYSTEM_ROLE_OPTIONS = [
  { value: 'USER', label: 'User' },
  { value: 'SUPER_ADMIN', label: 'Super Admin' },
]

interface EditUserSheetProps {
  user: AdminUser | null
  onOpenChange: (open: boolean) => void
}

export function EditUserSheet({ user, onOpenChange }: EditUserSheetProps) {
  return (
    <Sheet open={user !== null} onOpenChange={onOpenChange}>
      <SheetContent className="w-full gap-0 overflow-y-auto sm:max-w-md">
        {user && (
          <>
            <SheetHeader className="border-b">
              <SheetTitle>{user.displayName}</SheetTitle>
              <SheetDescription>
                {user.email} · đăng nhập gần nhất {formatDateTime(user.lastLoginAt)}
              </SheetDescription>
            </SheetHeader>
            <div className="space-y-6 p-4">
              <UpdateUserForm key={user.id} user={user} />
              <Separator />
              <ResetPasswordForm key={`${user.id}-reset`} userId={user.id} />
            </div>
          </>
        )}
      </SheetContent>
    </Sheet>
  )
}

function UpdateUserForm({ user }: { user: AdminUser }) {
  const update = useUpdateUser()
  const [systemRole, setSystemRole] = useState<AdminUser['systemRole']>(user.systemRole)
  const [isActive, setIsActive] = useState(user.isActive)

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const displayName = String(new FormData(event.currentTarget).get('displayName')).trim()
    update.mutate({ id: user.id, displayName, systemRole, isActive }, { onSuccess: () => toast.success('Đã cập nhật tài khoản.') })
  }

  return (
    <form className="grid gap-4" onSubmit={submit}>
      <h3 className="text-sm font-medium">Thông tin tài khoản</h3>
      <FieldGroup className="gap-4">
        <Field>
          <FieldLabel htmlFor="edit-display-name">Tên hiển thị</FieldLabel>
          <Input id="edit-display-name" name="displayName" defaultValue={user.displayName} required maxLength={200} />
        </Field>
        <Field>
          <FieldLabel htmlFor="edit-system-role">Quyền hệ thống</FieldLabel>
          <OptionSelect
            id="edit-system-role"
            value={systemRole}
            onValueChange={(value) => setSystemRole(value as AdminUser['systemRole'])}
            options={SYSTEM_ROLE_OPTIONS}
          />
        </Field>
        <Field orientation="horizontal" className="justify-between">
          <div>
            <FieldLabel htmlFor="edit-active">Đang hoạt động</FieldLabel>
            <FieldDescription>Vô hiệu hóa sẽ thu hồi mọi phiên đăng nhập.</FieldDescription>
          </div>
          <Switch id="edit-active" checked={isActive} onCheckedChange={setIsActive} />
        </Field>
      </FieldGroup>
      <FormError error={update.error} />
      <Button type="submit" className="justify-self-start" disabled={update.isPending}>
        {update.isPending && <Spinner />}
        Lưu thay đổi
      </Button>
    </form>
  )
}

function ResetPasswordForm({ userId }: { userId: string }) {
  const reset = useResetPassword()
  const passwordError = reset.error instanceof ApiError ? reset.error.field('temporaryPassword') : undefined

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    reset.mutate(
      { id: userId, temporaryPassword: String(new FormData(form).get('temporaryPassword')) },
      {
        onSuccess: () => {
          form.reset()
          toast.success('Đã đặt mật khẩu tạm. Người dùng cần đổi khi đăng nhập.')
        },
      },
    )
  }

  return (
    <form className="grid gap-4" onSubmit={submit}>
      <h3 className="text-sm font-medium">Đặt lại mật khẩu</h3>
      <Field data-invalid={Boolean(passwordError) || undefined}>
        <FieldLabel htmlFor="reset-password">Mật khẩu tạm mới</FieldLabel>
        <Input id="reset-password" name="temporaryPassword" type="password" autoComplete="new-password" required />
        <FieldDescription>Mọi phiên hiện tại bị thu hồi; người dùng phải đổi mật khẩu ở lần đăng nhập tới.</FieldDescription>
        <FieldError>{passwordError}</FieldError>
      </Field>
      <FormError error={passwordError ? null : reset.error} />
      <Button type="submit" variant="outline" className="justify-self-start" disabled={reset.isPending}>
        {reset.isPending ? <Spinner /> : <KeyRound />}
        Đặt lại mật khẩu
      </Button>
    </form>
  )
}
