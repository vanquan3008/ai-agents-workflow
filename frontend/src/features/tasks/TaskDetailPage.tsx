import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { formatDateTime, formatNumber, formatUsd, shortSha, statusLabel } from '../../shared/lib/format'
import { ErrorState, Loading } from '../../shared/ui/States'
import { StatusBadge } from '../../shared/ui/StatusBadge'
import { useCancelTask, useClarifyTask, useRetryTask, useTask, useTaskExecutions, useTaskTimeline } from './api'
import { useTaskRealtime } from './realtime'

export function TaskDetailPage() {
  const { taskId = '' } = useParams()
  useTaskRealtime(taskId)
  const task = useTask(taskId)
  const timeline = useTaskTimeline(taskId)
  const executions = useTaskExecutions(taskId)
  const cancel = useCancelTask(taskId)
  const retry = useRetryTask(taskId)
  const clarify = useClarifyTask(taskId)
  const [clarification, setClarification] = useState('')

  if (task.isPending) return <Loading />
  if (task.isError) return <ErrorState error={task.error} onRetry={() => void task.refetch()} />

  const t = task.data
  const canCancel = t.allowedNextStatuses.includes('CANCELLED')
  const canRetry = t.allowedNextStatuses.includes('PENDING') && t.status !== 'NEEDS_CLARIFICATION'

  return (
    <article className="stack">
      <header className="page-header">
        <div>
          <p className="muted">
            <Link to="/">Task</Link> / {t.repositoryFullName}
          </p>
          <h1>{t.title}</h1>
          <p>
            <StatusBadge status={t.status} /> {t.statusReason && <span className="muted">{t.statusReason}</span>}
          </p>
        </div>
        <div className="actions">
          {canRetry && (
            <button type="button" className="button" disabled={retry.isPending} onClick={() => retry.mutate()}>
              Thử lại
            </button>
          )}
          {canCancel && (
            <button
              type="button"
              className="button button--danger"
              disabled={cancel.isPending}
              onClick={() => {
                if (window.confirm('Hủy task này? Worker sẽ dừng trước bước tiếp theo.')) cancel.mutate(undefined)
              }}
            >
              Hủy task
            </button>
          )}
        </div>
      </header>
      {(cancel.isError || retry.isError) && <ErrorState error={cancel.error ?? retry.error} />}

      <section className="grid">
        <dl className="card facts">
          <dt>Nguồn</dt>
          <dd>
            {t.channel}
            {t.sourceReference && ` · ${t.sourceReference}`}
          </dd>
          <dt>Base commit</dt>
          <dd>
            <code>{shortSha(t.baseCommitSha)}</code>
          </dd>
          <dt>Nhánh</dt>
          <dd>
            <code>{t.workBranch ?? '—'}</code>
          </dd>
          <dt>Chi phí</dt>
          <dd>
            {formatUsd(t.usage.estimatedCostUsd)} · {formatNumber(t.usage.inputTokens + t.usage.outputTokens)} token
          </dd>
          <dt>Pull request</dt>
          <dd>
            {t.pullRequest ? (
              <a href={t.pullRequest.url} target="_blank" rel="noreferrer">
                #{t.pullRequest.number}
              </a>
            ) : (
              '—'
            )}
          </dd>
          <dt>Tạo lúc</dt>
          <dd>{formatDateTime(t.createdAt)}</dd>
        </dl>

        <div className="card">
          <h2>Mô tả</h2>
          <p className="prewrap">{t.description}</p>
        </div>
      </section>

      {t.status === 'NEEDS_CLARIFICATION' && (
        <form
          className="card form"
          onSubmit={(e) => {
            e.preventDefault()
            if (clarification.trim()) clarify.mutate(clarification.trim(), { onSuccess: () => setClarification('') })
          }}
        >
          <h2>Bổ sung thông tin</h2>
          <p>{t.statusReason}</p>
          <label htmlFor="clarification">Câu trả lời</label>
          <textarea id="clarification" rows={4} value={clarification} onChange={(e) => setClarification(e.target.value)} />
          {clarify.isError && <ErrorState error={clarify.error} />}
          <button type="submit" className="button button--primary" disabled={clarify.isPending || !clarification.trim()}>
            Gửi bổ sung
          </button>
        </form>
      )}

      {t.currentPlan && (
        <section className="card" aria-labelledby="plan-heading">
          <h2 id="plan-heading">
            Kế hoạch v{t.currentPlan.version} <StatusBadge status={t.currentPlan.policyOutcome === 'AUTO_APPROVED' ? 'APPROVED' : 'WAITING_FOR_APPROVAL'} />
          </h2>
          <p>{t.currentPlan.summary}</p>
          <div className="grid">
            <div>
              <h3>Các bước</h3>
              <ol>{t.currentPlan.steps.map((s) => <li key={s}>{s}</li>)}</ol>
            </div>
            <div>
              <h3>Tiêu chí kiểm tra</h3>
              <ul>{t.currentPlan.acceptanceCriteria.map((s) => <li key={s}>{s}</li>)}</ul>
              <h3>File dự kiến sửa</h3>
              <ul>{t.currentPlan.affectedPaths.map((p) => <li key={p}><code>{p}</code></li>)}</ul>
            </div>
          </div>
          <details>
            <summary>Lý do áp dụng chính sách (v{t.currentPlan.policyVersion})</summary>
            <ul>{t.currentPlan.policyReasons.map((r) => <li key={r}>{r}</li>)}</ul>
          </details>
        </section>
      )}

      <section className="grid">
        <div className="card" aria-labelledby="timeline-heading">
          <h2 id="timeline-heading">Timeline</h2>
          {timeline.isPending ? (
            <Loading />
          ) : timeline.isError ? (
            <ErrorState error={timeline.error} />
          ) : (
            <ol className="timeline">
              {timeline.data.map((entry) => (
                <li key={entry.sequence}>
                  <span className="muted">#{entry.sequence} · {formatDateTime(entry.occurredAt)}</span>
                  <div>
                    {entry.from ? `${statusLabel(entry.from)} → ` : ''}
                    <strong>{statusLabel(entry.to)}</strong> <span className="muted">bởi {entry.actorKind.toLowerCase()}</span>
                  </div>
                  {entry.reason && <div className="muted">{entry.reason}</div>}
                </li>
              ))}
            </ol>
          )}
        </div>

        <div className="card" aria-labelledby="executions-heading">
          <h2 id="executions-heading">Lần chạy</h2>
          {executions.isPending ? (
            <Loading />
          ) : executions.isError ? (
            <ErrorState error={executions.error} />
          ) : executions.data.length === 0 ? (
            <p className="muted">Chưa có lần chạy nào.</p>
          ) : (
            <ul className="list">
              {executions.data.map((ex) => (
                <li key={ex.id}>
                  <Link to={`/executions/${ex.id}`}>
                    {ex.step} #{ex.attempt}
                  </Link>{' '}
                  <StatusBadge status={ex.status} />
                  <div className="muted">
                    {ex.model} · {formatUsd(ex.usage.estimatedCostUsd)} · {formatDateTime(ex.startedAt)}
                  </div>
                  {ex.error && <div className="field-error">{ex.error}</div>}
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>
    </article>
  )
}
