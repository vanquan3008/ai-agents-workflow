import { Link, useNavigate } from 'react-router'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { Approval } from '@/shared/api/types'
import { sensitiveAreaLabel } from '@/shared/lib/labels'
import { RelativeTime } from '@/shared/ui/relative-time'
import { ApprovalStatusBadge } from './ApprovalStatusBadge'

export function ApprovalTable({ approvals }: { approvals: Approval[] }) {
  const navigate = useNavigate()
  return (
    <Table>
      <TableHeader>
        <TableRow className="hover:bg-transparent">
          <TableHead className="w-full min-w-40">Task</TableHead>
          <TableHead>Đối tượng</TableHead>
          <TableHead>Trạng thái</TableHead>
          <TableHead className="text-right">Hết hạn</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {approvals.map((approval) => (
          <TableRow key={approval.id} className="group cursor-pointer" onClick={() => void navigate(`/approvals/${approval.id}`)}>
            <TableCell className="max-w-0 py-2.5">
              <Link
                to={`/approvals/${approval.id}`}
                onClick={(event) => event.stopPropagation()}
                className="block truncate font-medium group-hover:underline group-hover:underline-offset-4"
              >
                {approval.taskTitle}
              </Link>
              {approval.sensitiveAreas.length > 0 && (
                <p className="text-muted-foreground mt-0.5 truncate text-xs">{approval.sensitiveAreas.map(sensitiveAreaLabel).join(' · ')}</p>
              )}
            </TableCell>
            <TableCell className="text-sm whitespace-nowrap">
              {approval.kind === 'PLAN' ? `Kế hoạch v${approval.planVersion}` : <span className="font-mono">{approval.commitSha?.slice(0, 7)}</span>}
            </TableCell>
            <TableCell>
              <ApprovalStatusBadge status={approval.status} />
            </TableCell>
            <TableCell className="text-muted-foreground text-right text-xs whitespace-nowrap">
              <RelativeTime value={approval.expiresAt} />
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
