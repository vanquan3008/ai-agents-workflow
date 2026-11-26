import { Link, useParams } from 'react-router'
import { useExecution } from './api'
import { formatDateTime, formatUsd } from '@/shared/lib/format'
import { ErrorBlock, LoadingBlock, PageHeader } from '@/shared/ui/page'
import { StatusBadge } from '@/shared/ui/status-badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

export default function ExecutionPage() {
  const { executionId = '' } = useParams()
  const query = useExecution(executionId)
  if (query.isPending) return <LoadingBlock />
  if (query.isError) return <ErrorBlock error={query.error} onRetry={() => void query.refetch()} />
  const { execution, checkpoint, toolExecutions } = query.data
  return (
    <article className="space-y-6">
      <PageHeader
        title={`${execution.step} · lần ${execution.attempt}`}
        description={execution.model}
        actions={
          <Link className="underline" to={`/tasks/${execution.taskId}`}>
            Xem task
          </Link>
        }
      />
      <StatusBadge status={execution.status} />
      <dl className="grid max-w-xl grid-cols-2 gap-3 text-sm">
        <dt>Bắt đầu</dt>
        <dd>{formatDateTime(execution.startedAt)}</dd>
        <dt>Kết thúc</dt>
        <dd>{formatDateTime(execution.finishedAt)}</dd>
        <dt>Chi phí</dt>
        <dd>{formatUsd(execution.usage.estimatedCostUsd)}</dd>
        <dt>Token vào / ra</dt>
        <dd>
          {execution.usage.inputTokens} / {execution.usage.outputTokens}
        </dd>
      </dl>
      {execution.error && (
        <p role="alert" className="text-destructive">
          {execution.error}
        </p>
      )}
      {checkpoint && (
        <details>
          <summary>Checkpoint</summary>
          <pre className="overflow-auto rounded-md bg-muted p-4 text-xs">{checkpoint}</pre>
        </details>
      )}
      <Card>
        <CardHeader>
          <CardTitle>Công cụ</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {toolExecutions.length === 0 && <p className="text-sm text-muted-foreground">Bước này chưa có lời gọi công cụ.</p>}
          {toolExecutions.map((tool) => (
            <details key={tool.id} className="rounded-md border p-4">
              <summary>
                {tool.sequence}. {tool.toolName} · {tool.status}
              </summary>
              <p className="my-2 text-sm">{tool.denialReason}</p>
              <pre className="overflow-auto whitespace-pre-wrap text-xs">{tool.inputJson}</pre>
              <pre className="mt-3 overflow-auto whitespace-pre-wrap text-xs">{tool.outputJson}</pre>
            </details>
          ))}
        </CardContent>
      </Card>
    </article>
  )
}
