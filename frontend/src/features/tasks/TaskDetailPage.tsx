import { ClipboardList, History, Info, PlayCircle, ShieldCheck } from 'lucide-react'
import { Link, useParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { ExecutionList } from '@/features/executions/components/ExecutionList'
import { PageHeader } from '@/shared/ui/page-header'
import { Panel } from '@/shared/ui/panel'
import { RelativeTime } from '@/shared/ui/relative-time'
import { EmptyBlock, ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { StatusBadge } from '@/shared/ui/status-badge'
import { useTask, useTaskExecutions, useTaskTimeline } from './api'
import { ClarificationForm } from './components/ClarificationForm'
import { PlanDetails } from './components/PlanDetails'
import { TaskActions } from './components/TaskActions'
import { TaskFacts } from './components/TaskFacts'
import { TaskTimeline } from './components/TaskTimeline'
import { useTaskRealtime } from './realtime'

export default function TaskDetailPage() {
  const { taskId = '' } = useParams()
  useTaskRealtime(taskId)
  const task = useTask(taskId)
  const timeline = useTaskTimeline(taskId)
  const executions = useTaskExecutions(taskId)

  if (task.isPending) return <LoadingBlock rows={8} />
  if (task.isError) return <ErrorBlock error={task.error} onRetry={() => void task.refetch()} />
  const data = task.data

  return (
    <article>
      <PageHeader
        backTo={{ to: '/tasks', label: 'Tất cả task' }}
        eyebrow={
          <>
            <StatusBadge status={data.status} />
            <span className="text-muted-foreground text-xs">
              cập nhật <RelativeTime value={data.updatedAt} />
            </span>
          </>
        }
        title={data.title}
        description={data.statusReason}
        actions={<TaskActions task={data} />}
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_22rem] xl:grid-cols-[minmax(0,1fr)_24rem]">
        <div className="min-w-0 space-y-4">
          {data.status === 'NEEDS_CLARIFICATION' && <ClarificationForm taskId={data.id} question={data.statusReason} />}

          {data.status === 'WAITING_FOR_APPROVAL' && (
            <Panel className="border-warning/40 bg-warning/[0.04]">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-start gap-2.5">
                  <ShieldCheck className="text-warning mt-0.5 size-4 shrink-0" />
                  <p className="text-sm">Kế hoạch chạm vùng cần duyệt. Người có quyền Approver hoặc Owner cần quyết định trước khi agent sửa code.</p>
                </div>
                <Button asChild variant="outline" size="sm" className="shrink-0">
                  <Link to="/approvals">Mở phê duyệt</Link>
                </Button>
              </div>
            </Panel>
          )}

          <Panel title="Mô tả" icon={<ClipboardList />}>
            <p className="text-sm leading-relaxed break-words whitespace-pre-wrap">{data.description}</p>
          </Panel>

          <Panel
            title={data.currentPlan ? `Kế hoạch v${data.currentPlan.version}` : 'Kế hoạch'}
            description={data.currentPlan ? 'Do Planner tạo; quyết định duyệt gắn với đúng phiên bản này.' : undefined}
            icon={<PlayCircle />}
          >
            {data.currentPlan ? (
              <PlanDetails plan={data.currentPlan} />
            ) : (
              <p className="text-muted-foreground text-sm">Agent chưa lập kế hoạch cho task này.</p>
            )}
          </Panel>

          <Panel title="Timeline" icon={<History />}>
            {timeline.isPending && <LoadingBlock rows={3} />}
            {timeline.isError && <ErrorBlock error={timeline.error} onRetry={() => void timeline.refetch()} />}
            {timeline.data && <TaskTimeline entries={timeline.data} />}
          </Panel>
        </div>

        <aside className="min-w-0 space-y-4 lg:sticky lg:top-16 lg:self-start">
          <Panel title="Thông tin thực thi" icon={<Info />}>
            <TaskFacts task={data} />
          </Panel>
          <Panel title="Lần chạy" description={executions.data ? `${executions.data.length} lần` : undefined} icon={<PlayCircle />}>
            {executions.isPending && <LoadingBlock rows={2} />}
            {executions.isError && <ErrorBlock error={executions.error} onRetry={() => void executions.refetch()} />}
            {executions.data?.length === 0 && <EmptyBlock className="py-6" title="Chưa có lần chạy" description="Worker sẽ ghi lại từng bước khi xử lý." />}
            {executions.data && executions.data.length > 0 && <ExecutionList executions={executions.data} />}
          </Panel>
        </aside>
      </div>
    </article>
  )
}
