import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { useAuth } from './AuthProvider'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { FormError, PageHeader } from '@/shared/ui/page'

export default function ChangePasswordPage() {
  const { user, changePassword } = useAuth()
  const navigate = useNavigate()
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [validation, setValidation] = useState('')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const password = String(data.get('newPassword'))
    if (password !== data.get('confirmation')) {
      setValidation('Hai mật khẩu mới cần giống nhau.')
      return
    }
    setValidation('')
    setError(null)
    setPending(true)
    try {
      await changePassword(String(data.get('currentPassword')), password)
      navigate('/', { replace: true })
    } catch (failure) {
      setError(failure)
    } finally {
      setPending(false)
    }
  }

  return (
    <section className="max-w-lg">
      <PageHeader title="Đổi mật khẩu" description={user?.mustChangePassword ? 'Đổi mật khẩu tạm để tiếp tục sử dụng.' : undefined} />
      <form onSubmit={submit} className="space-y-4">
        <label className="grid gap-2">
          Mật khẩu hiện tại
          <Input name="currentPassword" type="password" autoComplete="current-password" required />
        </label>
        <label className="grid gap-2">
          Mật khẩu mới
          <Input name="newPassword" type="password" autoComplete="new-password" required />
        </label>
        <label className="grid gap-2">
          Nhập lại mật khẩu mới
          <Input name="confirmation" type="password" autoComplete="new-password" required />
        </label>
        {validation && (
          <p role="alert" className="text-sm text-destructive">
            {validation}
          </p>
        )}
        <FormError error={error} />
        <Button type="submit" disabled={pending}>
          {pending ? 'Đang lưu…' : 'Đổi mật khẩu'}
        </Button>
      </form>
    </section>
  )
}
