import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useApproval, useDecideApproval } from './api'
import { useTask } from '@/features/tasks/api'
import { useAuth } from '@/features/auth/AuthProvider'
import { useProject } from '@/features/projects/api'
import { formatDateTime } from '@/shared/lib/format'
import { ErrorBlock, FormError, LoadingBlock, PageHeader } from '@/shared/ui/page'
import { StatusBadge } from '@/shared/ui/status-badge'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

export default function ApprovalDetailPage() {
  const { approvalId = '' } = useParams()
  const query = useApproval(approvalId)
  const task = useTask(query.data?.taskId ?? '')
  const project = useProject(query.data?.projectId ?? '')
  const { isSuperAdmin } = useAuth()
  const decide = useDecideApproval(approvalId)
  const [comment, setComment] = useState('')
  if (query.isPending) return <LoadingBlock />
  if (query.isError) return <ErrorBlock error={query.error} onRetry={() => void query.refetch()} />
  const approval = query.data
  const canDecide = isSuperAdmin || project.data?.myRole === 'OWNER' || project.data?.myRole === 'APPROVER'
  const isCurrentPlan = approval.kind === 'PLAN' && approval.planVersion === task.data?.currentPlan?.version
  const pending = approval.status === 'PENDING' && Date.parse(approval.expiresAt) > query.dataUpdatedAt
  return (
    <article className="max-w-3xl space-y-6">
      <PageHeader
        title={approval.taskTitle}
        description={approval.scope}
        actions={
          <Link className="underline" to={`/tasks/${approval.taskId}`}>
            Xem task
          </Link>
        }
      />
      <StatusBadge status={approval.status} />
      <p className="text-sm text-muted-foreground">
        Hết hạn {formatDateTime(approval.expiresAt)} · Chính sách v{approval.policyVersion}
      </p>
      {task.isPending && <LoadingBlock />}
      {task.isError && <ErrorBlock error={task.error} />}
      {project.isError && <ErrorBlock error={project.error} />}
      {isCurrentPlan && task.data?.currentPlan && (
        <Card>
          <CardHeader>
            <CardTitle>Kế hoạch v{approval.planVersion}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p>{task.data.currentPlan.summary}</p>
            <ol className="list-decimal pl-5">
              {task.data.currentPlan.steps.map((step) => (
                <li key={step}>{step}</li>
              ))}
            </ol>
            <h3 className="font-medium">Tiêu chí nghiệm thu</h3>
            <ul className="list-disc pl-5">
              {task.data.currentPlan.acceptanceCriteria.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
            <h3 className="font-medium">File dự kiến sửa</h3>
            <ul className="font-mono text-sm">
              {task.data.currentPlan.affectedPaths.map((path) => (
                <li key={path}>{path}</li>
              ))}
            </ul>
            <ul>
              {task.data.currentPlan.risks.map((risk) => (
                <li key={risk}>{risk}</li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
      {approval.kind === 'PLAN' && task.data && !isCurrentPlan && (
        <p role="alert">Kế hoạch hiện tại đã thay đổi. Mở task để kiểm tra yêu cầu duyệt mới.</p>
      )}
      {approval.commitSha && <p className="break-all font-mono">{approval.commitSha}</p>}
      {approval.comment && <p className="whitespace-pre-wrap">{approval.comment}</p>}
      {pending && canDecide && isCurrentPlan && (
        <div className="space-y-3">
          <label htmlFor="approval-comment">Nhận xét, bắt buộc khi từ chối</label>
          <Textarea id="approval-comment" value={comment} onChange={(event) => setComment(event.target.value)} maxLength={2000} />
          <FormError error={decide.error} />
          <div className="flex gap-3">
            <Button disabled={decide.isPending} onClick={() => decide.mutate({ decision: 'approve', comment })}>
              Duyệt kế hoạch này
            </Button>
            <Button
              variant="destructive"
              disabled={decide.isPending || !comment.trim()}
              onClick={() => decide.mutate({ decision: 'reject', comment: comment.trim() })}
            >
              Từ chối
            </Button>
          </div>
        </div>
      )}
      {pending && !canDecide && <p className="text-sm text-muted-foreground">Cần quyền Approver hoặc Owner để quyết định.</p>}
    </article>
  )
}
