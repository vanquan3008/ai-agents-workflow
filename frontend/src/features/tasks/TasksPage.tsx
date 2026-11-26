import { ListTodo, Plus } from 'lucide-react'
import { useSearchParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { useProjects } from '@/features/projects/api'
import { PageHeader } from '@/shared/ui/page-header'
import { Pagination } from '@/shared/ui/pagination'
import { Panel } from '@/shared/ui/panel'
import { EmptyBlock, ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { useTasks } from './api'
import { CreateTaskDialog } from './components/CreateTaskDialog'
import { TaskFilters } from './components/TaskFilters'
import { TaskTable } from './components/TaskTable'
import { useTaskFilters } from './use-task-filters'

const NEW_TASK_PARAM = 'new'

export default function TasksPage() {
  const filters = useTaskFilters()
  const [params, setParams] = useSearchParams()
  const projects = useProjects()
  const tasks = useTasks({ projectId: filters.projectId, status: filters.status, search: filters.search, page: filters.page })
  const createOpen = params.get(NEW_TASK_PARAM) === '1'

  function setCreateOpen(open: boolean) {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (open) next.set(NEW_TASK_PARAM, '1')
        else next.delete(NEW_TASK_PARAM)
        return next
      },
      { replace: true },
    )
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Task"
        description="Yêu cầu sửa lỗi từ dashboard, Discord và GitHub. Trạng thái cập nhật trực tiếp."
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus /> Giao việc
          </Button>
        }
        className="pb-2"
      />

      <TaskFilters
        value={filters}
        projects={projects.data ?? []}
        hasFilters={filters.hasFilters}
        onChange={filters.update}
        onReset={filters.reset}
      />

      {projects.isError && <ErrorBlock error={projects.error} onRetry={() => void projects.refetch()} />}

      <Panel flush>
        {tasks.isPending && <LoadingBlock rows={6} className="p-4" />}
        {tasks.isError && <ErrorBlock error={tasks.error} onRetry={() => void tasks.refetch()} className="m-4 w-auto" />}
        {tasks.data && tasks.data.items.length === 0 && (
          <EmptyBlock
            className="m-4 w-auto"
            icon={<ListTodo />}
            title={filters.hasFilters ? 'Không có task khớp bộ lọc' : 'Chưa có task nào'}
            description={filters.hasFilters ? 'Thử nới bộ lọc hoặc xóa từ khóa tìm kiếm.' : 'Giao việc đầu tiên để agent bắt đầu lập kế hoạch.'}
            action={
              filters.hasFilters ? (
                <Button variant="outline" size="sm" onClick={filters.reset}>
                  Xóa bộ lọc
                </Button>
              ) : (
                <Button size="sm" onClick={() => setCreateOpen(true)}>
                  <Plus /> Giao việc
                </Button>
              )
            }
          />
        )}
        {tasks.data && tasks.data.items.length > 0 && (
          <div className="overflow-x-auto">
            <TaskTable tasks={tasks.data.items} />
          </div>
        )}
      </Panel>

      {tasks.data && tasks.data.totalPages > 1 && (
        <Pagination
          page={filters.page}
          totalPages={tasks.data.totalPages}
          totalCount={tasks.data.totalCount}
          pending={tasks.isFetching}
          onPage={(page) => filters.update({ page })}
        />
      )}

      <CreateTaskDialog open={createOpen} onOpenChange={setCreateOpen} defaultProjectId={filters.projectId} />
    </div>
  )
}
