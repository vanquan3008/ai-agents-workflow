import { useState, type FormEvent } from 'react'
import { KeyRound } from 'lucide-react'
import { toast } from 'sonner'
import { useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Spinner } from '@/components/ui/spinner'
import { ApiError } from '@/shared/api/http'
import { PageHeader } from '@/shared/ui/page-header'
import { Panel } from '@/shared/ui/panel'
import { FormError } from '@/shared/ui/states'
import { useAuth } from './AuthProvider'

export default function ChangePasswordPage() {
  const { user, changePassword } = useAuth()
  const navigate = useNavigate()
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [mismatch, setMismatch] = useState(false)
  const fieldError = (field: string) => (error instanceof ApiError ? error.field(field) : undefined)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const password = String(data.get('newPassword'))
    if (password !== data.get('confirmation')) {
      setMismatch(true)
      return
    }
    setMismatch(false)
    setError(null)
    setPending(true)
    try {
      await changePassword(String(data.get('currentPassword')), password)
      toast.success('Đã đổi mật khẩu. Các phiên khác đã bị đăng xuất.')
      void navigate('/', { replace: true })
    } catch (failure) {
      setError(failure)
    } finally {
      setPending(false)
    }
  }

  return (
    <div className="mx-auto max-w-lg">
      <PageHeader
        title="Đổi mật khẩu"
        description={user?.mustChangePassword ? 'Bạn đang dùng mật khẩu tạm. Đặt mật khẩu mới để tiếp tục.' : 'Đổi mật khẩu sẽ đăng xuất mọi phiên khác.'}
      />
      <Panel icon={<KeyRound />} title="Mật khẩu mới">
        <form onSubmit={submit} className="grid gap-5">
          <FieldGroup className="gap-4">
            <Field data-invalid={Boolean(fieldError('currentPassword')) || undefined}>
              <FieldLabel htmlFor="current-password">Mật khẩu hiện tại</FieldLabel>
              <Input id="current-password" name="currentPassword" type="password" autoComplete="current-password" required />
              <FieldError>{fieldError('currentPassword')}</FieldError>
            </Field>
            <Field data-invalid={Boolean(fieldError('newPassword')) || undefined}>
              <FieldLabel htmlFor="new-password">Mật khẩu mới</FieldLabel>
              <Input id="new-password" name="newPassword" type="password" autoComplete="new-password" required />
              <FieldDescription>Tối thiểu 10 ký tự, có cả chữ và số, không chứa tên email.</FieldDescription>
              <FieldError>{fieldError('newPassword')}</FieldError>
            </Field>
            <Field data-invalid={mismatch || undefined}>
              <FieldLabel htmlFor="confirm-password">Nhập lại mật khẩu mới</FieldLabel>
              <Input id="confirm-password" name="confirmation" type="password" autoComplete="new-password" required />
              <FieldError>{mismatch ? 'Hai mật khẩu mới cần giống nhau.' : undefined}</FieldError>
            </Field>
          </FieldGroup>
          <FormError error={error instanceof ApiError && Object.keys(error.errors).length > 0 ? null : error} />
          <Button type="submit" className="justify-self-start" disabled={pending}>
            {pending && <Spinner />}
            {pending ? 'Đang lưu…' : 'Đổi mật khẩu'}
          </Button>
        </form>
      </Panel>
    </div>
  )
}
