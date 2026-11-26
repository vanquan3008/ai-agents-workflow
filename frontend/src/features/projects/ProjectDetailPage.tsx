import { type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { useProject, useMembers, useRepositories, useRegisterRepository, useUpsertMember, useRemoveMember } from './api'
import type { ProjectRole } from '@/shared/api/types'
import { useAuth } from '@/features/auth/AuthProvider'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { EmptyBlock, ErrorBlock, FormError, LoadingBlock, PageHeader } from '@/shared/ui/page'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

export default function ProjectDetailPage() {
  const { projectId = '' } = useParams()
  const project = useProject(projectId)
  const members = useMembers(projectId)
  const repositories = useRepositories(projectId)
  const register = useRegisterRepository(projectId)
  const upsert = useUpsertMember(projectId)
  const remove = useRemoveMember(projectId)
  const { isSuperAdmin } = useAuth()
  if (project.isPending) return <LoadingBlock />
  if (project.isError) return <ErrorBlock error={project.error} />
  const canManage = isSuperAdmin || project.data.myRole === 'OWNER'

  function addMember(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    upsert.mutate({ email: String(data.get('email')), role: data.get('role') as ProjectRole }, { onSuccess: () => form.reset() })
  }
  function addRepository(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    register.mutate(
      {
        installationId: Number(data.get('installationId')),
        owner: String(data.get('owner')),
        name: String(data.get('name')),
        defaultBranch: String(data.get('defaultBranch')),
        buildCommand: String(data.get('buildCommand')),
        testCommand: String(data.get('testCommand')),
      },
      { onSuccess: () => form.reset() },
    )
  }
  return (
    <article className="space-y-6">
      <PageHeader title={project.data.name} description={`${project.data.slug} · ${project.data.myRole}`} />
      <Card>
        <CardHeader>
          <CardTitle>Repository</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {repositories.isPending && <LoadingBlock />}
          {repositories.isError && <ErrorBlock error={repositories.error} />}
          {repositories.data?.length === 0 && <EmptyBlock title="Chưa có repository" />}
          <ul className="divide-y">
            {repositories.data?.map((repository) => (
              <li key={repository.id} className="flex flex-wrap justify-between gap-3 py-3">
                <span>
                  {repository.fullName} <span className="text-sm text-muted-foreground">· {repository.defaultBranch}</span>
                </span>
                <Link className="text-sm underline" to={`/repositories/${repository.id}/workflow`}>
                  Quy trình v{repository.workflowVersion}
                </Link>
              </li>
            ))}
          </ul>
          {canManage && (
            <details>
              <summary className="cursor-pointer font-medium">Đăng ký repository</summary>
              <form onSubmit={addRepository} className="mt-4 grid gap-4 sm:grid-cols-2">
                <label className="grid gap-2">
                  GitHub Installation ID
                  <Input name="installationId" type="number" min={1} required />
                </label>
                <label className="grid gap-2">
                  Owner trên GitHub
                  <Input name="owner" required />
                </label>
                <label className="grid gap-2">
                  Tên repo
                  <Input name="name" required />
                </label>
                <label className="grid gap-2">
                  Nhánh mặc định
                  <Input name="defaultBranch" defaultValue="main" required />
                </label>
                <label className="grid gap-2">
                  Lệnh build
                  <Input name="buildCommand" defaultValue="dotnet build" required />
                </label>
                <label className="grid gap-2">
                  Lệnh test
                  <Input name="testCommand" defaultValue="dotnet test" required />
                </label>
                <div className="sm:col-span-2">
                  <FormError error={register.error} />
                </div>
                <Button className="justify-self-start" type="submit" disabled={register.isPending}>
                  Đăng ký repo
                </Button>
              </form>
            </details>
          )}
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Thành viên</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {members.isPending && <LoadingBlock />}
          {members.isError && <ErrorBlock error={members.error} />}
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Người dùng</TableHead>
                <TableHead>Vai trò</TableHead>
                <TableHead>Thao tác</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {members.data?.map((member) => (
                <TableRow key={member.userId}>
                  <TableCell>
                    {member.displayName}
                    <p className="text-sm text-muted-foreground">{member.email}</p>
                  </TableCell>
                  <TableCell>{member.role}</TableCell>
                  <TableCell>
                    {canManage && (
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={remove.isPending}
                        onClick={() => {
                          if (window.confirm(`Gỡ ${member.email} khỏi project?`)) remove.mutate(member.userId)
                        }}
                      >
                        Gỡ
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <FormError error={remove.error} />
          {canManage && (
            <form onSubmit={addMember} className="grid max-w-lg gap-4">
              <label className="grid gap-2">
                Email tài khoản đã có
                <Input name="email" type="email" required />
              </label>
              <label className="grid gap-2">
                Vai trò
                <select name="role" defaultValue="REQUESTER" className="h-9 rounded-md border bg-background px-3">
                  {['VIEWER', 'REQUESTER', 'APPROVER', 'OWNER'].map((role) => (
                    <option key={role}>{role}</option>
                  ))}
                </select>
              </label>
              <FormError error={upsert.error} />
              <Button type="submit" className="justify-self-start" disabled={upsert.isPending}>
                Thêm hoặc đổi vai trò
              </Button>
            </form>
          )}
        </CardContent>
      </Card>
    </article>
  )
}
