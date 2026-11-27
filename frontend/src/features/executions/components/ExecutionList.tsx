import { ChevronRight } from 'lucide-react'
import { Link } from 'react-router'
import type { ExecutionSummary } from '@/shared/api/types'
import { formatDuration, formatUsd } from '@/shared/lib/format'
import { stepLabel } from '@/shared/lib/labels'
import { StatusBadge } from '@/shared/ui/status-badge'

export function ExecutionList({ executions }: { executions: ExecutionSummary[] }) {
  return (
    <ul className="-mx-1 divide-y">
      {executions.map((execution) => (
        <li key={execution.id}>
          <Link
            to={`/executions/${execution.id}`}
            className="hover:bg-muted/60 focus-visible:bg-muted/60 group flex items-center gap-3 rounded-md px-1 py-2.5 transition-colors"
          >
            <div className="min-w-0 flex-1 space-y-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-sm font-medium">
                  {stepLabel(execution.step)} <span className="text-muted-foreground font-normal">#{execution.attempt}</span>
                </span>
                <StatusBadge status={execution.status} />
              </div>
              <p className="text-muted-foreground truncate font-mono text-xs">
                {execution.model} · {formatUsd(execution.usage.estimatedCostUsd)} · {formatDuration(execution.durationSeconds)}
              </p>
              {execution.error && <p className="text-destructive line-clamp-2 text-xs break-words">{execution.error}</p>}
            </div>
            <ChevronRight className="text-muted-foreground size-4 shrink-0 transition-transform group-hover:translate-x-0.5" />
          </Link>
        </li>
      ))}
    </ul>
  )
}
