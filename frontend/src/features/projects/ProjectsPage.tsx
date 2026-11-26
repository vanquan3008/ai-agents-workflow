import { type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { useProjects, useCreateProject } from './api'
import { useAuth } from '@/features/auth/AuthProvider'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { EmptyBlock, ErrorBlock, FormError, LoadingBlock, PageHeader } from '@/shared/ui/page'

export default function ProjectsPage() {
  const query = useProjects()
  const create = useCreateProject()
  const { isSuperAdmin } = useAuth()
  const navigate = useNavigate()
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    create.mutate(
      {
        name: String(data.get('name')).trim(),
        slug: String(data.get('slug')).trim(),
        ownerEmail: String(data.get('ownerEmail')).trim() || undefined,
      },
      { onSuccess: (project) => navigate(`/projects/${project.id}`) },
    )
  }
  return (
    <section className="space-y-6">
      <PageHeader title="Project" />
      {query.isPending && <LoadingBlock />}
      {query.isError && <ErrorBlock error={query.error} onRetry={() => void query.refetch()} />}
      {query.data?.length === 0 && <EmptyBlock title="Chưa có project" />}
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {query.data?.map((project) => (
          <Card key={project.id}>
            <CardHeader>
              <CardTitle>
                <Link className="hover:underline" to={`/projects/${project.id}`}>
                  {project.name}
                </Link>
              </CardTitle>
            </CardHeader>
            <CardContent className="text-sm text-muted-foreground">
              {project.repositoryCount} repo · {project.memberCount} thành viên · {project.myRole}
            </CardContent>
          </Card>
        ))}
      </div>
      {isSuperAdmin && (
        <Card>
          <CardHeader>
            <CardTitle>Tạo project</CardTitle>
          </CardHeader>
          <CardContent>
            <form className="grid max-w-lg gap-4" onSubmit={submit}>
              <label className="grid gap-2">
                Tên project
                <Input name="name" required maxLength={200} />
              </label>
              <label className="grid gap-2">
                Slug
                <Input name="slug" required pattern="[a-z0-9]+(-[a-z0-9]+)*" maxLength={100} />
              </label>
              <label className="grid gap-2">
                Email Owner, để trống để tự quản lý
                <Input name="ownerEmail" type="email" />
              </label>
              <FormError error={create.error} />
              <Button className="justify-self-start" type="submit" disabled={create.isPending}>
                Tạo project
              </Button>
            </form>
          </CardContent>
        </Card>
      )}
    </section>
  )
}
