import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { formatDateTime, shortSha } from '../../shared/lib/format'
import { ErrorState, Loading } from '../../shared/ui/States'
import { StatusBadge } from '../../shared/ui/StatusBadge'
import { useTask } from '../tasks/api'
import { useApproval, useDecideApproval } from './api'

/**
 * Màn hình duyệt: hiển thị đúng đối tượng được duyệt (phiên bản kế hoạch hoặc commit), vùng nhạy cảm và lý do.
 * Toàn bộ thao tác dùng phần tử form gốc nên dùng được bằng bàn phím (Tab, Enter).
 */
export function ApprovalDetailPage() {
  const { approvalId = '' } = useParams()
  const approval = useApproval(approvalId)
  const task = useTask(approval.data?.taskId ?? '')
  const decide = useDecideApproval(approvalId)
  const [comment, setComment] = useState('')
  const [decision, setDecision] = useState<'approve' | 'reject'>('approve')
  const [validation, setValidation] = useState<string | null>(null)

  if (approval.isPending) return <Loading />
  if (approval.isError) return <ErrorState error={approval.error} onRetry={() => void approval.refetch()} />

  const a = approval.data
  const plan = task.data?.currentPlan
  const isPending = a.status === 'PENDING'

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (decision === 'reject' && !comment.trim()) {
      setValidation('Từ chối cần nêu lý do.')
      return
    }
    setValidation(null)
    decide.mutate({ decision, comment: comment.trim() })
  }

  return (
    <article className="stack">
      <header className="page-header">
        <div>
          <p className="muted">
            <Link to="/approvals">Phê duyệt</Link> / <Link to={`/tasks/${a.taskId}`}>{a.taskTitle}</Link>
          </p>
          <h1>{a.kind === 'PLAN' ? `Duyệt kế hoạch v${a.planVersion}` : `Duyệt diff tại ${shortSha(a.commitSha)}`}</h1>
          <StatusBadge status={a.status} />
        </div>
      </header>

      <section className="grid">
        <dl className="card facts">
          <dt>Vùng nhạy cảm</dt>
          <dd>{a.sensitiveAreas.join(', ') || 'Ngoài phạm vi tự động'}</dd>
          <dt>Chính sách</dt>
          <dd>v{a.policyVersion}</dd>
          <dt>Yêu cầu lúc</dt>
          <dd>{formatDateTime(a.requestedAt)}</dd>
          <dt>Hết hạn</dt>
          <dd>{formatDateTime(a.expiresAt)}</dd>
          {a.decidedAt && (
            <>
              <dt>Quyết định lúc</dt>
              <dd>{formatDateTime(a.decidedAt)}</dd>
              <dt>Ghi chú</dt>
              <dd>{a.comment ?? '—'}</dd>
            </>
          )}
        </dl>
        <div className="card">
          <h2>Vì sao cần duyệt</h2>
          <ul>
            {a.scope.split('\n').map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
        </div>
      </section>

      {plan && a.kind === 'PLAN' && (
        <section className="card">
          <h2>Kế hoạch v{plan.version}</h2>
          <p>{plan.summary}</p>
          <ol>{plan.steps.map((s) => <li key={s}>{s}</li>)}</ol>
          <h3>File dự kiến sửa</h3>
          <ul>{plan.affectedPaths.map((p) => <li key={p}><code>{p}</code></li>)}</ul>
          {plan.version !== a.planVersion && <p className="field-error">Kế hoạch đã đổi sau khi xin duyệt; yêu cầu này sẽ không còn hiệu lực.</p>}
        </section>
      )}

      {isPending && (
        <form className="card form" onSubmit={submit} aria-labelledby="decision-heading">
          <h2 id="decision-heading">Quyết định</h2>
          <fieldset className="radio-group">
            <legend className="visually-hidden">Lựa chọn</legend>
            <label>
              <input type="radio" name="decision" value="approve" checked={decision === 'approve'} onChange={() => setDecision('approve')} />
              Duyệt
            </label>
            <label>
              <input type="radio" name="decision" value="reject" checked={decision === 'reject'} onChange={() => setDecision('reject')} />
              Từ chối
            </label>
          </fieldset>
          <label htmlFor="approval-comment">{decision === 'reject' ? 'Lý do từ chối (bắt buộc)' : 'Ghi chú (không bắt buộc)'}</label>
          <textarea
            id="approval-comment"
            rows={3}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            aria-invalid={Boolean(validation)}
            aria-describedby={validation ? 'approval-comment-error' : undefined}
          />
          {validation && <p id="approval-comment-error" className="field-error">{validation}</p>}
          {decide.isError && <ErrorState error={decide.error} />}
          <button type="submit" className={`button ${decision === 'reject' ? 'button--danger' : 'button--primary'}`} disabled={decide.isPending}>
            {decide.isPending ? 'Đang gửi…' : decision === 'reject' ? 'Từ chối' : 'Duyệt'}
          </button>
        </form>
      )}
    </article>
  )
}
