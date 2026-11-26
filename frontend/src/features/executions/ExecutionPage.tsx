import { Link, useParams } from 'react-router'
import { formatDateTime, formatNumber, formatUsd } from '../../shared/lib/format'
import { ErrorState, Loading } from '../../shared/ui/States'
import { StatusBadge } from '../../shared/ui/StatusBadge'
import { useExecution } from './api'

export function ExecutionPage() {
  const { executionId = '' } = useParams()
  const execution = useExecution(executionId)

  if (execution.isPending) return <Loading />
  if (execution.isError) return <ErrorState error={execution.error} onRetry={() => void execution.refetch()} />

  const { execution: ex, toolExecutions, checkpoint } = execution.data

  return (
    <article className="stack">
      <header className="page-header">
        <div>
          <p className="muted">
            <Link to={`/tasks/${ex.taskId}`}>Quay lại task</Link>
          </p>
          <h1>
            {ex.step} · lần {ex.attempt}
          </h1>
          <StatusBadge status={ex.status} />
        </div>
      </header>

      <dl className="card facts">
        <dt>Model</dt>
        <dd>{ex.model}</dd>
        <dt>Token</dt>
        <dd>
          {formatNumber(ex.usage.inputTokens)} vào · {formatNumber(ex.usage.outputTokens)} ra · {formatNumber(ex.usage.cacheReadTokens)} cache
        </dd>
        <dt>Chi phí ước tính</dt>
        <dd>{formatUsd(ex.usage.estimatedCostUsd)}</dd>
        <dt>Thời lượng</dt>
        <dd>{ex.durationSeconds !== null ? `${ex.durationSeconds.toFixed(1)} giây` : 'Đang chạy'}</dd>
        <dt>Bắt đầu</dt>
        <dd>{formatDateTime(ex.startedAt)}</dd>
        {ex.error && (
          <>
            <dt>Lỗi</dt>
            <dd className="field-error">{ex.error}</dd>
          </>
        )}
      </dl>

      <section className="card" aria-labelledby="tools-heading">
        <h2 id="tools-heading">Tool call ({toolExecutions.length})</h2>
        {toolExecutions.length === 0 ? (
          <p className="muted">Bước này không gọi công cụ nào.</p>
        ) : (
          <ol className="list">
            {toolExecutions.map((tool) => (
              <li key={tool.id}>
                <strong>{tool.toolName}</strong> <StatusBadge status={tool.status} />
                {tool.exitCode !== null && <span className="muted"> · exit {tool.exitCode}</span>}
                {tool.denialReason && <div className="field-error">{tool.denialReason}</div>}
                <details>
                  <summary>Đầu vào / đầu ra</summary>
                  <pre>{tool.inputJson}</pre>
                  {tool.outputJson && <pre>{tool.outputJson}</pre>}
                </details>
              </li>
            ))}
          </ol>
        )}
      </section>

      {checkpoint && (
        <details className="card">
          <summary>Checkpoint</summary>
          <pre>{checkpoint}</pre>
        </details>
      )}
    </article>
  )
}
