import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useCancelTask, useClarifyTask, useRetryTask, useTask, useTaskExecutions, useTaskTimeline } from './api'
import { useTaskRealtime } from './realtime'
import { formatDateTime, formatUsd, shortSha, statusLabel } from '@/shared/lib/format'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { StatusBadge } from '@/shared/ui/status-badge'
import { ErrorBlock, FormError, LoadingBlock, PageHeader } from '@/shared/ui/page'

export default function TaskDetailPage() {
  const { taskId = '' } = useParams()
  useTaskRealtime(taskId)
  const task = useTask(taskId)
  const timeline = useTaskTimeline(taskId)
  const executions = useTaskExecutions(taskId)
  const cancel = useCancelTask(taskId)
  const retry = useRetryTask(taskId)
  const clarify = useClarifyTask(taskId)
  const [additionalInfo, setAdditionalInfo] = useState('')
  if (task.isPending) return <LoadingBlock />
  if (task.isError) return <ErrorBlock error={task.error} onRetry={() => void task.refetch()} />
  const data = task.data

  return (
    <article className="space-y-6">
      <PageHeader
        title={data.title}
        description={data.repositoryFullName}
        actions={
          <>
            {data.allowedNextStatuses.includes('PENDING') && (
              <Button variant="outline" disabled={retry.isPending} onClick={() => retry.mutate()}>
                Thử lại
              </Button>
            )}
            {data.allowedNextStatuses.includes('CANCELLED') && (
              <Button
                variant="destructive"
                disabled={cancel.isPending}
                onClick={() => {
                  if (window.confirm('Hủy task này?')) cancel.mutate(undefined)
                }}
              >
                Hủy task
              </Button>
            )}
          </>
        }
      />
      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={data.status} />
        <p className="text-sm">{data.statusReason}</p>
      </div>
      <FormError error={cancel.error ?? retry.error} />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Mô tả</CardTitle>
          </CardHeader>
          <CardContent className="whitespace-pre-wrap">{data.description}</CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Thực thi</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-3 text-sm">
              <dt>Base commit</dt>
              <dd className="font-mono">{shortSha(data.baseCommitSha)}</dd>
              <dt>Nhánh</dt>
              <dd className="break-all font-mono">{data.workBranch ?? '—'}</dd>
              <dt>Chi phí</dt>
              <dd>{formatUsd(data.usage.estimatedCostUsd)}</dd>
              <dt>Token vào / ra</dt>
              <dd>
                {data.usage.inputTokens} / {data.usage.outputTokens}
              </dd>
              <dt>Pull request</dt>
              <dd>
                {data.pullRequest ? (
                  <a className="underline" href={data.pullRequest.url} target="_blank" rel="noreferrer">
                    #{data.pullRequest.number}
                  </a>
                ) : (
                  '—'
                )}
              </dd>
            </dl>
          </CardContent>
        </Card>
      </div>
      {data.status === 'NEEDS_CLARIFICATION' && (
        <form
          className="grid gap-3 rounded-xl border p-6"
          onSubmit={(event) => {
            event.preventDefault()
            clarify.mutate(additionalInfo.trim(), { onSuccess: () => setAdditionalInfo('') })
          }}
        >
          <label htmlFor="additional-info">Bổ sung thông tin</label>
          <Textarea
            id="additional-info"
            value={additionalInfo}
            onChange={(event) => setAdditionalInfo(event.target.value)}
            required
            maxLength={20000}
          />
          <FormError error={clarify.error} />
          <Button className="justify-self-start" disabled={clarify.isPending || !additionalInfo.trim()} type="submit">
            Gửi bổ sung
          </Button>
        </form>
      )}
      {data.currentPlan && (
        <Card>
          <CardHeader>
            <CardTitle>Kế hoạch v{data.currentPlan.version}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p>{data.currentPlan.summary}</p>
            <ol className="list-decimal space-y-1 pl-5">
              {data.currentPlan.steps.map((step) => (
                <li key={step}>{step}</li>
              ))}
            </ol>
            <h3 className="font-medium">Tiêu chí nghiệm thu</h3>
            <ul className="list-disc pl-5">
              {data.currentPlan.acceptanceCriteria.map((criterion) => (
                <li key={criterion}>{criterion}</li>
              ))}
            </ul>
            <h3 className="font-medium">File dự kiến sửa</h3>
            <ul className="font-mono text-sm">
              {data.currentPlan.affectedPaths.map((path) => (
                <li key={path}>{path}</li>
              ))}
            </ul>
            {data.currentPlan.risks.length > 0 && (
              <>
                <h3 className="font-medium">Rủi ro</h3>
                <ul>
                  {data.currentPlan.risks.map((risk) => (
                    <li key={risk}>{risk}</li>
                  ))}
                </ul>
              </>
            )}
            <details>
              <summary>Chính sách v{data.currentPlan.policyVersion}</summary>
              <ul>
                {data.currentPlan.policyReasons.map((reason) => (
                  <li key={reason}>{reason}</li>
                ))}
              </ul>
            </details>
            {data.status === 'WAITING_FOR_APPROVAL' && (
              <Link className="inline-block underline" to="/approvals">
                Mở danh sách phê duyệt
              </Link>
            )}
          </CardContent>
        </Card>
      )}
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Timeline</CardTitle>
          </CardHeader>
          <CardContent>
            {timeline.isPending && <LoadingBlock />}
            {timeline.isError && <ErrorBlock error={timeline.error} />}
            <ol className="space-y-5">
              {timeline.data?.map((entry) => (
                <li key={entry.sequence} className="border-l-2 pl-4">
                  <p className="text-xs text-muted-foreground">
                    {formatDateTime(entry.occurredAt)} · {entry.actorKind}
                  </p>
                  <p className="font-medium">{statusLabel(entry.to)}</p>
                  <p className="text-sm text-muted-foreground">{entry.reason}</p>
                </li>
              ))}
            </ol>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Lần chạy</CardTitle>
          </CardHeader>
          <CardContent>
            {executions.isPending && <LoadingBlock />}
            {executions.isError && <ErrorBlock error={executions.error} />}
            {executions.data?.length === 0 && <p className="text-sm text-muted-foreground">Chưa có lần chạy.</p>}
            <ul className="space-y-4">
              {executions.data?.map((execution) => (
                <li key={execution.id} className="space-y-1">
                  <Link className="mr-3 underline" to={`/executions/${execution.id}`}>
                    {execution.step} #{execution.attempt}
                  </Link>
                  <StatusBadge status={execution.status} />
                  <p className="text-sm text-muted-foreground">
                    {execution.model} · {formatUsd(execution.usage.estimatedCostUsd)}
                  </p>
                  {execution.error && <p className="text-sm text-destructive">{execution.error}</p>}
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      </div>
    </article>
  )
}
