import { AlertTriangle, FileText, Info, Lock } from 'lucide-react'
import { Link, useParams } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/features/auth/AuthProvider'
import { useProject } from '@/features/projects/api'
import { useTask } from '@/features/tasks/api'
import { PlanDetails } from '@/features/tasks/components/PlanDetails'
import { formatDateTime } from '@/shared/lib/format'
import { sensitiveAreaLabel } from '@/shared/lib/labels'
import { DescriptionList } from '@/shared/ui/description-list'
import { PageHeader } from '@/shared/ui/page-header'
import { Panel } from '@/shared/ui/panel'
import { RelativeTime } from '@/shared/ui/relative-time'
import { ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { ApprovalStatusBadge } from './components/ApprovalStatusBadge'
import { useApproval } from './api'
import { ApprovalDecisionPanel } from './components/ApprovalDecisionPanel'

export default function ApprovalDetailPage() {
  const { approvalId = '' } = useParams()
  const query = useApproval(approvalId)
  const task = useTask(query.data?.taskId ?? '')
  const project = useProject(query.data?.projectId ?? '')
  const { isSuperAdmin } = useAuth()

  if (query.isPending) return <LoadingBlock rows={6} />
  if (query.isError) return <ErrorBlock error={query.error} onRetry={() => void query.refetch()} />

  const approval = query.data
  const myRole = project.data?.myRole
  const canDecide = isSuperAdmin || myRole === 'OWNER' || myRole === 'APPROVER'
  const isCurrentPlan = approval.kind === 'PLAN' && approval.planVersion === task.data?.currentPlan?.version
  const isOpen = approval.status === 'PENDING' && Date.parse(approval.expiresAt) > query.dataUpdatedAt
  const reasons = approval.scope.split('\n').filter(Boolean)

  return (
    <article>
      <PageHeader
        backTo={{ to: '/approvals', label: 'Tất cả yêu cầu duyệt' }}
        eyebrow={<ApprovalStatusBadge status={approval.status} />}
        title={approval.taskTitle}
        description={approval.kind === 'PLAN' ? `Duyệt kế hoạch v${approval.planVersion} trước khi agent sửa code.` : 'Duyệt diff trước khi xuất bản pull request.'}
        actions={
          <Button asChild variant="outline">
            <Link to={`/tasks/${approval.taskId}`}>Mở task</Link>
          </Button>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_22rem] xl:grid-cols-[minmax(0,1fr)_24rem]">
        <div className="min-w-0 space-y-4">
          <Panel title="Vì sao cần duyệt" icon={<AlertTriangle />}>
            {approval.sensitiveAreas.length > 0 && (
              <div className="mb-3 flex flex-wrap gap-1.5">
                {approval.sensitiveAreas.map((area) => (
                  <Badge key={area} variant="outline" className="border-warning/40 text-warning rounded-md">
                    {sensitiveAreaLabel(area)}
                  </Badge>
                ))}
              </div>
            )}
            <ul className="space-y-1.5 text-sm">
              {reasons.map((reason) => (
                <li key={reason} className="text-muted-foreground break-words">
                  • {reason}
                </li>
              ))}
            </ul>
          </Panel>

          {task.isPending && <LoadingBlock rows={4} />}
          {task.isError && <ErrorBlock error={task.error} onRetry={() => void task.refetch()} />}
          {approval.kind === 'PLAN' && task.data && !isCurrentPlan && (
            <Panel className="border-warning/40 bg-warning/[0.04]">
              <p className="text-sm" role="alert">
                Kế hoạch của task đã thay đổi sau yêu cầu này. Mở task để xem yêu cầu duyệt mới.
              </p>
            </Panel>
          )}
          {isCurrentPlan && task.data?.currentPlan && (
            <Panel title={`Kế hoạch v${approval.planVersion}`} icon={<FileText />}>
              <PlanDetails plan={task.data.currentPlan} showPolicy={false} />
            </Panel>
          )}
        </div>

        <aside className="min-w-0 space-y-4 lg:sticky lg:top-16 lg:self-start">
          {isOpen && canDecide && isCurrentPlan && <ApprovalDecisionPanel approvalId={approval.id} planVersion={approval.planVersion} />}
          {isOpen && !canDecide && project.data && (
            <Panel>
              <p className="text-muted-foreground flex items-start gap-2 text-sm">
                <Lock className="mt-0.5 size-4 shrink-0" /> Cần quyền Approver hoặc Owner của project để quyết định.
              </p>
            </Panel>
          )}
          <Panel title="Chi tiết" icon={<Info />}>
            <DescriptionList
              items={[
                { label: 'Loại', value: approval.kind === 'PLAN' ? 'Kế hoạch' : 'Diff' },
                ...(approval.commitSha ? [{ label: 'Commit', value: approval.commitSha, mono: true }] : []),
                { label: 'Chính sách', value: `v${approval.policyVersion}` },
                { label: 'Yêu cầu lúc', value: formatDateTime(approval.requestedAt) },
                { label: 'Hết hạn', value: <RelativeTime value={approval.expiresAt} /> },
                ...(approval.decidedAt ? [{ label: 'Quyết định lúc', value: formatDateTime(approval.decidedAt) }] : []),
                ...(approval.comment ? [{ label: 'Nhận xét', value: <span className="whitespace-pre-wrap">{approval.comment}</span> }] : []),
              ]}
            />
          </Panel>
        </aside>
      </div>
    </article>
  )
}
