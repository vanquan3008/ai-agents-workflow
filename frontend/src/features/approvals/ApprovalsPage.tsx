import { useState } from 'react'
import { Link } from 'react-router'
import { useApprovals } from './api'
import type { ApprovalStatus } from '@/shared/api/types'
import { formatDateTime, statusLabel } from '@/shared/lib/format'
import { ErrorBlock, EmptyBlock, LoadingBlock, PageHeader } from '@/shared/ui/page'
import { Pagination } from '@/shared/ui/pagination'
import { StatusBadge } from '@/shared/ui/status-badge'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

export default function ApprovalsPage() {
  const [status, setStatus] = useState<ApprovalStatus | ''>('PENDING')
  const [page, setPage] = useState(1)
  const query = useApprovals(status, page)
  return (
    <section className="space-y-4">
      <PageHeader title="Phê duyệt" />
      <label className="flex items-center gap-3 text-sm">
        Trạng thái
        <select
          className="h-9 rounded-md border bg-background px-3"
          value={status}
          onChange={(event) => {
            setStatus(event.target.value as ApprovalStatus | '')
            setPage(1)
          }}
        >
          <option value="">Tất cả</option>
          {(['PENDING', 'APPROVED', 'REJECTED', 'EXPIRED', 'SUPERSEDED'] as const).map((value) => (
            <option value={value} key={value}>
              {statusLabel(value)}
            </option>
          ))}
        </select>
      </label>
      {query.isPending && <LoadingBlock />}
      {query.isError && <ErrorBlock error={query.error} onRetry={() => void query.refetch()} />}
      {query.data?.items.length === 0 && <EmptyBlock title="Không có yêu cầu duyệt" />}
      {query.data && query.data.items.length > 0 && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Task</TableHead>
              <TableHead>Nội dung</TableHead>
              <TableHead>Trạng thái</TableHead>
              <TableHead>Hết hạn</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {query.data.items.map((approval) => (
              <TableRow key={approval.id}>
                <TableCell>
                  <Link className="font-medium hover:underline" to={`/approvals/${approval.id}`}>
                    {approval.taskTitle}
                  </Link>
                </TableCell>
                <TableCell>{approval.kind === 'PLAN' ? `Kế hoạch v${approval.planVersion}` : 'Diff'}</TableCell>
                <TableCell>
                  <StatusBadge status={approval.status} />
                </TableCell>
                <TableCell>{formatDateTime(approval.expiresAt)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
      {query.data && <Pagination page={page} totalPages={query.data.totalPages} pending={query.isFetching} onPage={setPage} />}
    </section>
  )
}
