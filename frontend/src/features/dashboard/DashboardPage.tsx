import { Link } from 'react-router'
import { useTasks } from '@/features/tasks/api'
import { TaskTable } from '@/features/tasks/TaskTable'
import { useApprovals } from '@/features/approvals/api'
import { useProjects } from '@/features/projects/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ErrorBlock, LoadingBlock, PageHeader } from '@/shared/ui/page'

export default function DashboardPage() {
  const tasks = useTasks({ pageSize: 8 })
  const approvals = useApprovals('PENDING')
  const projects = useProjects()
  return (
    <section className="space-y-6">
      <PageHeader title="Tổng quan" />
      <div className="grid gap-4 sm:grid-cols-3">
        <Card><CardHeader><CardTitle>Task</CardTitle></CardHeader><CardContent>{tasks.data?.totalCount ?? '—'}</CardContent></Card>
        <Card><CardHeader><CardTitle>Chờ duyệt</CardTitle></CardHeader><CardContent><Link to="/approvals">{approvals.data?.totalCount ?? '—'}</Link></CardContent></Card>
        <Card><CardHeader><CardTitle>Project</CardTitle></CardHeader><CardContent><Link to="/projects">{projects.data?.length ?? '—'}</Link></CardContent></Card>
      </div>
      {approvals.isError && <ErrorBlock error={approvals.error} onRetry={() => void approvals.refetch()} />}
      {projects.isError && <ErrorBlock error={projects.error} onRetry={() => void projects.refetch()} />}
      <Card>
        <CardHeader><CardTitle>Task gần đây</CardTitle></CardHeader>
        <CardContent>
          {tasks.isPending && <LoadingBlock />}
          {tasks.isError && <ErrorBlock error={tasks.error} onRetry={() => void tasks.refetch()} />}
          {tasks.data && <TaskTable tasks={tasks.data.items} />}
        </CardContent>
      </Card>
    </section>
  )
}
