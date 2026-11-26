import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { TASK_STATUSES, type TaskStatus } from '../../shared/api/types'
import { formatDateTime, statusLabel } from '../../shared/lib/format'
import { Empty, ErrorState, Loading } from '../../shared/ui/States'
import { StatusBadge } from '../../shared/ui/StatusBadge'
import { useProjects, useRepositories } from '../projects/api'
import { useCreateTask, useTasks } from './api'
import { CreateTaskForm } from './components/CreateTaskForm'

export function TaskListPage() {
  const [params, setParams] = useSearchParams()
  const navigate = useNavigate()
  const status = (params.get('status') ?? '') as TaskStatus | ''
  const page = Number(params.get('page') ?? '1')
  const [search, setSearch] = useState(params.get('q') ?? '')

  const projects = useProjects()
  const projectId = params.get('project') ?? projects.data?.[0]?.id
  const repositories = useRepositories(projectId)
  const tasks = useTasks({ projectId, status, search: params.get('q') ?? undefined, page, pageSize: 20 })
  const createTask = useCreateTask()

  const update = (patch: Record<string, string>) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(patch)) {
      if (value) next.set(key, value)
      else next.delete(key)
    }
    setParams(next)
  }

  return (
    <div className="layout-split">
      <section aria-labelledby="tasks-heading">
        <header className="page-header">
          <h1 id="tasks-heading">Task</h1>
          {projects.data && projects.data.length > 1 && (
            <select aria-label="Project" value={projectId ?? ''} onChange={(e) => update({ project: e.target.value, page: '' })}>
              {projects.data.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          )}
        </header>

        <form
          className="toolbar"
          role="search"
          onSubmit={(e) => {
            e.preventDefault()
            update({ q: search.trim(), page: '' })
          }}
        >
          <label className="visually-hidden" htmlFor="task-status">
            Trạng thái
          </label>
          <select id="task-status" value={status} onChange={(e) => update({ status: e.target.value, page: '' })}>
            <option value="">Tất cả trạng thái</option>
            {TASK_STATUSES.map((s) => (
              <option key={s} value={s}>
                {statusLabel(s)}
              </option>
            ))}
          </select>
          <label className="visually-hidden" htmlFor="task-search">
            Tìm theo tiêu đề
          </label>
          <input id="task-search" type="search" placeholder="Tìm theo tiêu đề" value={search} onChange={(e) => setSearch(e.target.value)} />
          <button type="submit" className="button">
            Tìm
          </button>
        </form>

        {tasks.isPending ? (
          <Loading />
        ) : tasks.isError ? (
          <ErrorState error={tasks.error} onRetry={() => void tasks.refetch()} />
        ) : tasks.data.items.length === 0 ? (
          <Empty title="Chưa có task nào khớp bộ lọc.">
            <p className="muted">Tạo task ở khung bên phải, hoặc giao việc qua Discord/GitHub Issue.</p>
          </Empty>
        ) : (
          <>
            <table className="table">
              <thead>
                <tr>
                  <th scope="col">Tiêu đề</th>
                  <th scope="col">Repo</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Nguồn</th>
                  <th scope="col">Cập nhật</th>
                </tr>
              </thead>
              <tbody>
                {tasks.data.items.map((task) => (
                  <tr key={task.id}>
                    <td>
                      <Link to={`/tasks/${task.id}`}>{task.title}</Link>
                      {task.statusReason && <div className="muted truncate">{task.statusReason}</div>}
                    </td>
                    <td>{task.repositoryFullName}</td>
                    <td>
                      <StatusBadge status={task.status} />
                    </td>
                    <td>{task.channel}</td>
                    <td>{formatDateTime(task.updatedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <nav className="pagination" aria-label="Phân trang">
              <button type="button" className="button" disabled={page <= 1} onClick={() => update({ page: String(page - 1) })}>
                Trước
              </button>
              <span>
                Trang {tasks.data.page}/{Math.max(1, tasks.data.totalPages)} · {tasks.data.totalCount} task
              </span>
              <button type="button" className="button" disabled={!tasks.data.hasNextPage} onClick={() => update({ page: String(page + 1) })}>
                Sau
              </button>
            </nav>
          </>
        )}
      </section>

      <aside className="panel">
        {repositories.isPending && projectId ? (
          <Loading label="Đang tải repo…" />
        ) : repositories.isError ? (
          <ErrorState error={repositories.error} />
        ) : (
          <CreateTaskForm
            key={projectId}
            repositories={repositories.data ?? []}
            onSubmit={(input) => createTask.mutateAsync(input)}
            onCreated={(result) => void navigate(`/tasks/${result.taskId}`)}
          />
        )}
      </aside>
    </div>
  )
}
