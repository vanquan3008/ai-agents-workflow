import { useDeferredValue, useState } from 'react'
import { ListTodo, Search } from 'lucide-react'
import { Link } from 'react-router'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'
import { RelativeTime } from '@/shared/ui/relative-time'
import { EmptyBlock, ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { StatusBadge } from '@/shared/ui/status-badge'
import { useDepartmentTasks } from '../../api'

const PAGE_SIZE = 20

interface OfficeTaskListProps {
  projectId: string
  departmentId: string
  selectedTaskId: string | undefined
  onSelectTask: (taskId: string) => void
  className?: string
}

/** Recent tasks of the department's repositories, newest first. Selecting one follows it through the floor. */
export function OfficeTaskList({ projectId, departmentId, selectedTaskId, onSelectTask, className }: OfficeTaskListProps) {
  const [search, setSearch] = useState('')
  // Deferred so typing stays responsive while the list query catches up.
  const deferredSearch = useDeferredValue(search.trim())
  const tasks = useDepartmentTasks(projectId, departmentId, { search: deferredSearch, pageSize: PAGE_SIZE })

  return (
    <section aria-label="Task gần đây" className={cn('bg-card flex min-h-0 flex-col rounded-lg border shadow-xs', className)}>
      <header className="space-y-2 border-b p-3">
        <h2 className="text-sm font-medium">Task gần đây</h2>
        <div className="relative">
          <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2" aria-hidden="true" />
          <Input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Tìm task"
            aria-label="Tìm task theo tiêu đề"
            className="h-8 pl-8 text-sm"
          />
        </div>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {tasks.isPending && <LoadingBlock rows={4} className="p-3" />}
        {tasks.isError && <ErrorBlock error={tasks.error} onRetry={() => void tasks.refetch()} className="m-3 w-auto" />}
        {tasks.data?.items.length === 0 && (
          <EmptyBlock
            className="m-3 border-0 py-8"
            icon={<ListTodo />}
            title={deferredSearch ? 'Không có task phù hợp' : 'Chưa có task'}
            description={deferredSearch ? 'Thử từ khóa khác.' : 'Task của các repository mà phòng ban theo dõi sẽ hiện ở đây.'}
          />
        )}
        {tasks.data && tasks.data.items.length > 0 && (
          <ul className="divide-y" aria-busy={tasks.isFetching}>
            {tasks.data.items.map((task) => (
              <li key={task.id}>
                <button
                  type="button"
                  onClick={() => onSelectTask(task.id)}
                  aria-current={task.id === selectedTaskId || undefined}
                  className={cn(
                    'hover:bg-accent/50 focus-visible:ring-ring w-full space-y-1 px-3 py-2.5 text-left focus-visible:ring-[3px] focus-visible:outline-none',
                    task.id === selectedTaskId && 'bg-accent/70',
                  )}
                >
                  <span className="line-clamp-2 text-sm font-medium">{task.title}</span>
                  <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
                    <StatusBadge status={task.status} />
                    <span className="text-muted-foreground truncate text-xs">{task.repositoryFullName}</span>
                  </span>
                  <span className="text-muted-foreground block text-xs">
                    cập nhật <RelativeTime value={task.updatedAt} />
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {tasks.data && tasks.data.totalCount > tasks.data.items.length && (
        <footer className="text-muted-foreground border-t px-3 py-2 text-xs">
          {tasks.data.items.length}/{tasks.data.totalCount} task gần nhất ·{' '}
          <Link to={`/tasks?project=${projectId}`} className="hover:text-foreground underline underline-offset-2">
            Mở trang Task
          </Link>
        </footer>
      )}
    </section>
  )
}
