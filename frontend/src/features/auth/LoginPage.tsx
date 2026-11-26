import { useState, type FormEvent } from 'react'
import { GitPullRequest, ShieldCheck, Workflow } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Spinner } from '@/components/ui/spinner'
import { FormError } from '@/shared/ui/states'
import { useAuth } from './AuthProvider'

const HIGHLIGHTS = [
  { icon: Workflow, text: 'Agent lập kế hoạch, sửa và kiểm thử trong sandbox cách ly.' },
  { icon: ShieldCheck, text: 'Thay đổi nhạy cảm luôn chờ người có quyền duyệt.' },
  { icon: GitPullRequest, text: 'Kết quả là pull request để con người review và merge.' },
]

export function LoginPage() {
  const { login } = useAuth()
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<unknown>(null)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    setPending(true)
    setError(null)
    try {
      await login(String(data.get('email')), String(data.get('password')))
    } catch (failure) {
      setError(failure)
    } finally {
      setPending(false)
    }
  }

  return (
    <main className="grid min-h-svh lg:grid-cols-[1fr_minmax(0,32rem)]">
      <section className="bg-sidebar relative hidden overflow-hidden border-r p-10 lg:flex lg:flex-col lg:justify-between">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 [background-image:linear-gradient(to_right,var(--border)_1px,transparent_1px),linear-gradient(to_bottom,var(--border)_1px,transparent_1px)] [background-size:32px_32px] [mask-image:radial-gradient(ellipse_at_top_left,black,transparent_70%)]"
        />
        <div className="relative flex items-center gap-2.5">
          <span className="bg-primary text-primary-foreground flex size-8 items-center justify-center rounded-md">
            <Workflow className="size-4" />
          </span>
          <span className="text-sm font-semibold tracking-tight">DevAgent</span>
        </div>
        <div className="relative max-w-md space-y-6">
          <h2 className="text-2xl leading-tight font-semibold tracking-tight text-balance">Từ mô tả lỗi đến pull request đã được kiểm chứng.</h2>
          <ul className="space-y-3">
            {HIGHLIGHTS.map((item) => (
              <li key={item.text} className="text-muted-foreground flex items-start gap-3 text-sm">
                <item.icon className="text-primary mt-0.5 size-4 shrink-0" />
                {item.text}
              </li>
            ))}
          </ul>
        </div>
        <p className="text-muted-foreground relative text-xs">Mọi quyết định duyệt đều được ghi audit.</p>
      </section>

      <section className="flex items-center justify-center p-6 sm:p-10">
        <div className="w-full max-w-sm space-y-6">
          <div className="space-y-1.5">
            <div className="mb-6 flex items-center gap-2.5 lg:hidden">
              <span className="bg-primary text-primary-foreground flex size-8 items-center justify-center rounded-md">
                <Workflow className="size-4" />
              </span>
              <span className="text-sm font-semibold tracking-tight">DevAgent</span>
            </div>
            <h1 className="text-xl font-semibold tracking-tight">Đăng nhập</h1>
            <p className="text-muted-foreground text-sm">Dùng tài khoản do quản trị viên cấp.</p>
          </div>
          <form onSubmit={submit} className="grid gap-5">
            <FieldGroup className="gap-4">
              <Field>
                <FieldLabel htmlFor="email">Email</FieldLabel>
                <Input id="email" name="email" type="email" autoComplete="username" required />
              </Field>
              <Field>
                <FieldLabel htmlFor="password">Mật khẩu</FieldLabel>
                <Input id="password" name="password" type="password" autoComplete="current-password" required />
              </Field>
            </FieldGroup>
            <FormError error={error} />
            <Button type="submit" size="lg" className="w-full" disabled={pending}>
              {pending && <Spinner />}
              {pending ? 'Đang đăng nhập…' : 'Đăng nhập'}
            </Button>
          </form>
        </div>
      </section>
    </main>
  )
}
