import { useState, type FormEvent } from 'react'
import { useUsers, useCreateUser, useUpdateUser, useResetPassword } from './api'
import type { AdminUser } from '@/shared/api/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ErrorBlock, FormError, LoadingBlock, PageHeader } from '@/shared/ui/page'
import { Pagination } from '@/shared/ui/pagination'
import { formatDateTime } from '@/shared/lib/format'

function UserEditor({ user }: { user: AdminUser }) {
  const update = useUpdateUser()
  const reset = useResetPassword()
  const [saved, setSaved] = useState('')
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    setSaved('')
    update.mutate(
      {
        id: user.id,
        displayName: String(data.get('displayName')),
        systemRole: data.get('systemRole') as AdminUser['systemRole'],
        isActive: data.get('active') === 'on',
      },
      { onSuccess: () => setSaved('Đã cập nhật tài khoản.') },
    )
  }
  function resetPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    setSaved('')
    reset.mutate(
      { id: user.id, temporaryPassword: String(data.get('temporaryPassword')) },
      {
        onSuccess: () => {
          form.reset()
          setSaved('Đã đặt mật khẩu tạm. Người dùng cần đổi khi đăng nhập.')
        },
      },
    )
  }
  return (
    <details className="rounded-lg border p-4">
      <summary className="cursor-pointer">
        <span className="font-medium">{user.displayName}</span>
        <span className="ml-3 text-sm text-muted-foreground">
          {user.email} · {user.systemRole} · {user.isActive ? 'Đang hoạt động' : 'Đã vô hiệu hóa'}
        </span>
      </summary>
      <p className="my-3 text-sm text-muted-foreground">
        Đăng nhập gần nhất {formatDateTime(user.lastLoginAt)} · {user.projectCount} project
      </p>
      <div className="grid gap-6 md:grid-cols-2">
        <form className="grid gap-3" onSubmit={submit}>
          <label className="grid gap-2">
            Tên hiển thị
            <Input name="displayName" defaultValue={user.displayName} required />
          </label>
          <label className="grid gap-2">
            Quyền hệ thống
            <select className="h-9 rounded-md border bg-background px-3" name="systemRole" defaultValue={user.systemRole}>
              <option value="USER">User</option>
              <option value="SUPER_ADMIN">Super Admin</option>
            </select>
          </label>
          <label className="flex items-center gap-2">
            <input name="active" type="checkbox" defaultChecked={user.isActive} />
            Đang hoạt động
          </label>
          <FormError error={update.error} />
          <Button type="submit" className="justify-self-start" disabled={update.isPending}>
            Cập nhật
          </Button>
        </form>
        <form className="grid content-start gap-3" onSubmit={resetPassword}>
          <label className="grid gap-2">
            Mật khẩu tạm mới
            <Input name="temporaryPassword" type="password" autoComplete="new-password" required />
          </label>
          <FormError error={reset.error} />
          <Button type="submit" variant="outline" className="justify-self-start" disabled={reset.isPending}>
            Đặt lại mật khẩu
          </Button>
        </form>
      </div>
      {saved && <output className="mt-3 block text-sm">{saved}</output>}
    </details>
  )
}

export default function UsersPage() {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const query = useUsers(search, page)
  const create = useCreateUser()
  const [created, setCreated] = useState(false)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    setCreated(false)
    create.mutate(
      {
        email: String(data.get('email')),
        displayName: String(data.get('displayName')),
        systemRole: data.get('systemRole') as AdminUser['systemRole'],
        temporaryPassword: String(data.get('temporaryPassword')),
      },
      {
        onSuccess: () => {
          form.reset()
          setCreated(true)
        },
      },
    )
  }
  return (
    <section className="space-y-6">
      <PageHeader title="Người dùng" />
      <label className="grid max-w-sm gap-2">
        Tìm theo tên hoặc email
        <Input
          value={search}
          onChange={(event) => {
            setSearch(event.target.value)
            setPage(1)
          }}
        />
      </label>
      {query.isPending && <LoadingBlock />}
      {query.isError && <ErrorBlock error={query.error} onRetry={() => void query.refetch()} />}
      <div className="space-y-3">
        {query.data?.items.map((user) => (
          <UserEditor key={user.id} user={user} />
        ))}
      </div>
      {query.data?.items.length === 0 && <p className="text-sm text-muted-foreground">Không tìm thấy người dùng.</p>}
      {query.data && <Pagination page={page} totalPages={query.data.totalPages} pending={query.isFetching} onPage={setPage} />}
      <Card>
        <CardHeader>
          <CardTitle>Tạo người dùng</CardTitle>
        </CardHeader>
        <CardContent>
          <form className="grid max-w-lg gap-4" onSubmit={submit}>
            <label className="grid gap-2">
              Email
              <Input name="email" type="email" required />
            </label>
            <label className="grid gap-2">
              Tên hiển thị
              <Input name="displayName" required />
            </label>
            <label className="grid gap-2">
              Quyền hệ thống
              <select name="systemRole" className="h-9 rounded-md border bg-background px-3" defaultValue="USER">
                <option value="USER">User</option>
                <option value="SUPER_ADMIN">Super Admin</option>
              </select>
            </label>
            <label className="grid gap-2">
              Mật khẩu tạm
              <Input name="temporaryPassword" type="password" autoComplete="new-password" required />
            </label>
            <FormError error={create.error} />
            {created && <output className="block text-sm">Đã tạo tài khoản. Người dùng cần đổi mật khẩu ở lần đăng nhập đầu.</output>}
            <Button className="justify-self-start" type="submit" disabled={create.isPending}>
              Tạo tài khoản
            </Button>
          </form>
        </CardContent>
      </Card>
    </section>
  )
}
