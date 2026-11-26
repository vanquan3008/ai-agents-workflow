import { Link } from 'react-router'
import type { TaskSummary } from '@/shared/api/types'
import { formatRelative } from '@/shared/lib/format'
import { StatusBadge } from '@/shared/ui/status-badge'
import { EmptyBlock } from '@/shared/ui/page'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

export function TaskTable({ tasks }: { tasks: TaskSummary[] }) {
  if (tasks.length === 0) return <EmptyBlock title="Chưa có task" />
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Task</TableHead>
          <TableHead>Repository</TableHead>
          <TableHead>Trạng thái</TableHead>
          <TableHead>Cập nhật</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {tasks.map((task) => (
          <TableRow key={task.id}>
            <TableCell>
              <Link className="font-medium hover:underline" to={`/tasks/${task.id}`}>
                {task.title}
              </Link>
            </TableCell>
            <TableCell className="text-muted-foreground">{task.repositoryFullName}</TableCell>
            <TableCell>
              <StatusBadge status={task.status} />
            </TableCell>
            <TableCell className="text-muted-foreground">{formatRelative(task.updatedAt)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
