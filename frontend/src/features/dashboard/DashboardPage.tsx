import { AlertOctagon, ArrowRight, Ban, FolderGit2, ListTodo, MessageSquareWarning, Plus, ShieldCheck } from 'lucide-react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { useApprovals } from '@/features/approvals/api'
import { useAuth } from '@/features/auth/AuthProvider'
import { useProjects } from '@/features/projects/api'
import { useTaskCount, useTasks } from '@/features/tasks/api'
import { TaskTable } from '@/features/tasks/components/TaskTable'
import { formatNumber } from '@/shared/lib/format'
import { PageHeader } from '@/shared/ui/page-header'
import { Panel } from '@/shared/ui/panel'
import { EmptyBlock, ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { PendingApprovalsList } from './components/PendingApprovalsList'
import { StatTile } from './components/StatTile'

const RECENT_TASK_COUNT = 8
const PENDING_APPROVAL_PREVIEW = 5

export default function DashboardPage() {
  const { user } = useAuth()
  const recentTasks = useTasks({ pageSize: RECENT_TASK_COUNT })
  const totalTasks = useTaskCount({})
  const needsClarification = useTaskCount({ status: 'NEEDS_CLARIFICATION' })
  const failed = useTaskCount({ status: 'FAILED' })
  const blocked = useTaskCount({ status: 'BLOCKED' })
  const pendingApprovals = useApprovals('PENDING')
  const projects = useProjects()
  const firstName = user?.displayName.split(/\s+/).at(-1)

  return (
    <div className="space-y-6">
      <PageHeader
        title="Tổng quan"
        description={firstName ? `Chào ${firstName}, đây là những việc cần bạn chú ý.` : undefined}
        actions={
          <Button asChild>
            <Link to="/tasks?new=1">
              <Plus /> Giao việc
            </Link>
          </Button>
        }
        className="pb-0"
      />

      <section aria-label="Chỉ số" className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
        <StatTile label="Tổng task" value={totalTasks.count} icon={<ListTodo />} to="/tasks" tone="info" />
        <StatTile label="Chờ duyệt" value={pendingApprovals.data?.totalCount} icon={<ShieldCheck />} to="/approvals" tone="warning" />
        <StatTile
          label="Cần bổ sung"
          value={needsClarification.count}
          icon={<MessageSquareWarning />}
          to="/tasks?status=NEEDS_CLARIFICATION"
          tone="warning"
        />
        <StatTile label="Bị chặn" value={blocked.count} icon={<Ban />} to="/tasks?status=BLOCKED" tone="neutral" />
        <StatTile label="Thất bại" value={failed.count} icon={<AlertOctagon />} to="/tasks?status=FAILED" tone="danger" />
      </section>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_22rem] xl:grid-cols-[minmax(0,1fr)_24rem]">
        <Panel
          title="Task gần đây"
          icon={<ListTodo />}
          flush
          actions={
            <Button asChild variant="ghost" size="sm">
              <Link to="/tasks">
                Xem tất cả <ArrowRight />
              </Link>
            </Button>
          }
        >
          {recentTasks.isPending && <LoadingBlock rows={5} className="p-4" />}
          {recentTasks.isError && <ErrorBlock error={recentTasks.error} onRetry={() => void recentTasks.refetch()} className="m-4 w-auto" />}
          {recentTasks.data?.items.length === 0 && (
            <EmptyBlock className="m-4 w-auto" icon={<ListTodo />} title="Chưa có task" description="Giao việc đầu tiên cho agent để bắt đầu." />
          )}
          {recentTasks.data && recentTasks.data.items.length > 0 && (
            <div className="overflow-x-auto">
              <TaskTable tasks={recentTasks.data.items} compact />
            </div>
          )}
        </Panel>

        <div className="min-w-0 space-y-4">
          <Panel
            title="Chờ bạn duyệt"
            icon={<ShieldCheck />}
            actions={
              pendingApprovals.data && pendingApprovals.data.totalCount > PENDING_APPROVAL_PREVIEW ? (
                <Button asChild variant="ghost" size="sm">
                  <Link to="/approvals">Tất cả</Link>
                </Button>
              ) : undefined
            }
          >
            {pendingApprovals.isPending && <LoadingBlock rows={2} />}
            {pendingApprovals.isError && <ErrorBlock error={pendingApprovals.error} onRetry={() => void pendingApprovals.refetch()} />}
            {pendingApprovals.data?.items.length === 0 && <p className="text-muted-foreground text-sm">Không có yêu cầu duyệt nào đang chờ.</p>}
            {pendingApprovals.data && pendingApprovals.data.items.length > 0 && (
              <PendingApprovalsList approvals={pendingApprovals.data.items.slice(0, PENDING_APPROVAL_PREVIEW)} />
            )}
          </Panel>

          <Panel
            title="Project"
            icon={<FolderGit2 />}
            actions={
              <Button asChild variant="ghost" size="sm">
                <Link to="/projects">Quản lý</Link>
              </Button>
            }
          >
            {projects.isPending && <LoadingBlock rows={2} />}
            {projects.isError && <ErrorBlock error={projects.error} onRetry={() => void projects.refetch()} />}
            {projects.data?.length === 0 && <p className="text-muted-foreground text-sm">Bạn chưa tham gia project nào.</p>}
            <ul className="-mx-1 divide-y">
              {projects.data?.map((project) => (
                <li key={project.id}>
                  <Link to={`/projects/${project.id}`} className="hover:bg-muted/60 flex items-center justify-between gap-3 rounded-md px-1 py-2 transition-colors">
                    <span className="truncate text-sm font-medium">{project.name}</span>
                    <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
                      {formatNumber(project.repositoryCount)} repo · {formatNumber(project.memberCount)} người
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </Panel>
        </div>
      </div>
    </div>
  )
}
