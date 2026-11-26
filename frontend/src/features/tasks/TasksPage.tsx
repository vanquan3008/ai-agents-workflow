import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { useProjects, useRepositories } from '@/features/projects/api'
import { useCreateTask, useTasks } from './api'
import { TaskTable } from './TaskTable'
import { TASK_STATUSES, type TaskStatus } from '@/shared/api/types'
import { newIdempotencyKey } from '@/shared/api/http'
import { statusLabel } from '@/shared/lib/format'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ErrorBlock, FormError, LoadingBlock, PageHeader } from '@/shared/ui/page'
import { Pagination } from '@/shared/ui/pagination'

export default function TasksPage() {
  const [projectId, setProjectId] = useState('')
  const [status, setStatus] = useState<TaskStatus | ''>('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [key, setKey] = useState(() => newIdempotencyKey())
  const projects = useProjects()
  const repositories = useRepositories(projectId)
  const tasks = useTasks({ projectId, status, search, page })
  const create = useCreateTask()
  const navigate = useNavigate()

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    create.mutate(
      {
        repositoryId: String(data.get('repositoryId')),
        title: String(data.get('title')).trim(),
        description: String(data.get('description')).trim(),
        idempotencyKey: key,
      },
      { onSuccess: (result) => navigate(`/tasks/${result.taskId}`) },
    )
  }

  return (
    <section className="space-y-6">
      <PageHeader title="Task" />
      {projects.isError && <ErrorBlock error={projects.error} />}
      <div className="flex flex-wrap gap-3">
        <label className="grid gap-1 text-sm">
          Project
          <select
            className="h-9 rounded-md border bg-background px-3"
            value={projectId}
            onChange={(event) => {
              setProjectId(event.target.value)
              setPage(1)
            }}
          >
            <option value="">Tất cả project</option>
            {projects.data?.map((project) => (
              <option value={project.id} key={project.id}>
                {project.name}
              </option>
            ))}
          </select>
        </label>
        <label className="grid gap-1 text-sm">
          Trạng thái
          <select
            className="h-9 rounded-md border bg-background px-3"
            value={status}
            onChange={(event) => {
              setStatus(event.target.value as TaskStatus | '')
              setPage(1)
            }}
          >
            <option value="">Tất cả trạng thái</option>
            {TASK_STATUSES.map((value) => (
              <option key={value} value={value}>
                {statusLabel(value)}
              </option>
            ))}
          </select>
        </label>
        <label className="grid gap-1 text-sm">
          Tìm task
          <Input
            value={search}
            onChange={(event) => {
              setSearch(event.target.value)
              setPage(1)
            }}
          />
        </label>
      </div>
      {tasks.isPending && <LoadingBlock />}
      {tasks.isError && <ErrorBlock error={tasks.error} onRetry={() => void tasks.refetch()} />}
      {tasks.data && (
        <>
          <TaskTable tasks={tasks.data.items} />
          <Pagination page={page} totalPages={tasks.data.totalPages} pending={tasks.isFetching} onPage={setPage} />
        </>
      )}
      <Card>
        <CardHeader>
          <CardTitle>Giao việc</CardTitle>
        </CardHeader>
        <CardContent>
          {!projectId ? (
            <p className="text-sm text-muted-foreground">Chọn project để giao việc cho một repository.</p>
          ) : (
            <form onSubmit={submit} onChange={() => setKey(newIdempotencyKey())} className="grid max-w-2xl gap-4">
              {repositories.isError && <ErrorBlock error={repositories.error} />}
              <label className="grid gap-2">
                Repository
                <select key={projectId} name="repositoryId" className="h-9 rounded-md border bg-background px-3" required defaultValue="">
                  <option value="" disabled>
                    Chọn repository
                  </option>
                  {repositories.data
                    ?.filter((repo) => repo.isActive)
                    .map((repo) => (
                      <option key={repo.id} value={repo.id}>
                        {repo.fullName}
                      </option>
                    ))}
                </select>
              </label>
              <label className="grid gap-2">
                Tiêu đề
                <Input name="title" required maxLength={200} />
              </label>
              <label className="grid gap-2">
                Mô tả lỗi và kết quả mong đợi
                <Textarea name="description" rows={5} required maxLength={20000} />
              </label>
              <FormError error={create.error} />
              <Button
                className="justify-self-start"
                type="submit"
                disabled={create.isPending || !repositories.data?.some((repo) => repo.isActive)}
              >
                {create.isPending ? 'Đang tạo…' : 'Tạo task'}
              </Button>
            </form>
          )}
        </CardContent>
      </Card>
    </section>
  )
}
