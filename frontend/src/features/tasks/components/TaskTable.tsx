import { GitPullRequest } from 'lucide-react'
import { Link, useNavigate } from 'react-router'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { TaskSummary } from '@/shared/api/types'
import { channelLabel } from '@/shared/lib/labels'
import { RelativeTime } from '@/shared/ui/relative-time'
import { StatusBadge } from '@/shared/ui/status-badge'

interface TaskTableProps {
  tasks: TaskSummary[]
  /** Hides secondary columns for compact placements such as the dashboard. */
  compact?: boolean
}

export function TaskTable({ tasks, compact = false }: TaskTableProps) {
  const navigate = useNavigate()

  return (
    <Table>
      <TableHeader>
        <TableRow className="hover:bg-transparent">
          <TableHead className="w-full">Task</TableHead>
          <TableHead>Trạng thái</TableHead>
          {!compact && <TableHead className="hidden lg:table-cell">Kênh</TableHead>}
          <TableHead className="hidden text-right sm:table-cell">Cập nhật</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {tasks.map((task) => (
          <TableRow key={task.id} className="group cursor-pointer" onClick={() => void navigate(`/tasks/${task.id}`)}>
            <TableCell className="max-w-0 py-2.5">
              <Link
                to={`/tasks/${task.id}`}
                onClick={(event) => event.stopPropagation()}
                className="block truncate font-medium group-hover:underline group-hover:underline-offset-4"
              >
                {task.title}
              </Link>
              <div className="text-muted-foreground mt-0.5 flex items-center gap-2 text-xs">
                <span className="truncate font-mono">{task.repositoryFullName}</span>
                {task.pullRequestUrl && <GitPullRequest className="text-success size-3.5 shrink-0" aria-label="Đã có pull request" />}
                <RelativeTime value={task.updatedAt} className="shrink-0 sm:hidden" />
              </div>
            </TableCell>
            <TableCell>
              <StatusBadge status={task.status} />
            </TableCell>
            {!compact && <TableCell className="text-muted-foreground hidden lg:table-cell">{channelLabel(task.channel)}</TableCell>}
            <TableCell className="text-muted-foreground hidden text-right text-xs whitespace-nowrap sm:table-cell">
              <RelativeTime value={task.updatedAt} />
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
