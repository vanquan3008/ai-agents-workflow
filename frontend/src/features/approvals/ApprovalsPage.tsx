import { Link, useSearchParams } from 'react-router'
import type { ApprovalStatus } from '../../shared/api/types'
import { formatDateTime, statusLabel } from '../../shared/lib/format'
import { Empty, ErrorState, Loading } from '../../shared/ui/States'
import { StatusBadge } from '../../shared/ui/StatusBadge'
import { useApprovals } from './api'

const STATUSES: ApprovalStatus[] = ['PENDING', 'APPROVED', 'REJECTED', 'EXPIRED', 'SUPERSEDED']

export function ApprovalsPage() {
  const [params, setParams] = useSearchParams()
  const status = (params.get('status') ?? 'PENDING') as ApprovalStatus | ''
  const page = Number(params.get('page') ?? '1')
  const approvals = useApprovals(status, page)

  return (
    <section className="stack" aria-labelledby="approvals-heading">
      <header className="page-header">
        <h1 id="approvals-heading">Phê duyệt</h1>
        <label className="visually-hidden" htmlFor="approval-status">
          Trạng thái
        </label>
        <select id="approval-status" value={status} onChange={(e) => setParams({ status: e.target.value })}>
          <option value="">Tất cả</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {statusLabel(s)}
            </option>
          ))}
        </select>
      </header>

      {approvals.isPending ? (
        <Loading />
      ) : approvals.isError ? (
        <ErrorState error={approvals.error} onRetry={() => void approvals.refetch()} />
      ) : approvals.data.items.length === 0 ? (
        <Empty title="Không có yêu cầu duyệt nào." />
      ) : (
        <table className="table">
          <thead>
            <tr>
              <th scope="col">Task</th>
              <th scope="col">Loại</th>
              <th scope="col">Vùng nhạy cảm</th>
              <th scope="col">Trạng thái</th>
              <th scope="col">Hết hạn</th>
            </tr>
          </thead>
          <tbody>
            {approvals.data.items.map((a) => (
              <tr key={a.id}>
                <td>
                  <Link to={`/approvals/${a.id}`}>{a.taskTitle}</Link>
                </td>
                <td>{a.kind === 'PLAN' ? `Kế hoạch v${a.planVersion}` : `Diff ${a.commitSha?.slice(0, 7)}`}</td>
                <td>{a.sensitiveAreas.join(', ') || 'Ngoài phạm vi tự động'}</td>
                <td>
                  <StatusBadge status={a.status} />
                </td>
                <td>{formatDateTime(a.expiresAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
