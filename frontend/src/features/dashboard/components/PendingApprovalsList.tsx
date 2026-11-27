import { ChevronRight } from 'lucide-react'
import { Link } from 'react-router'
import type { Approval } from '@/shared/api/types'
import { RelativeTime } from '@/shared/ui/relative-time'

export function PendingApprovalsList({ approvals }: { approvals: Approval[] }) {
  return (
    <ul className="-mx-1 divide-y">
      {approvals.map((approval) => (
        <li key={approval.id}>
          <Link
            to={`/approvals/${approval.id}`}
            className="hover:bg-muted/60 group flex items-center gap-3 rounded-md px-1 py-2.5 transition-colors"
          >
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium">{approval.taskTitle}</p>
              <p className="text-muted-foreground text-xs">
                {approval.kind === 'PLAN' ? `Kế hoạch v${approval.planVersion}` : 'Diff'} · hết hạn <RelativeTime value={approval.expiresAt} />
              </p>
            </div>
            <ChevronRight className="text-muted-foreground size-4 shrink-0 transition-transform group-hover:translate-x-0.5" />
          </Link>
        </li>
      ))}
    </ul>
  )
}
