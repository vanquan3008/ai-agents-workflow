import type { TimelineEntry } from '@/shared/api/types'
import { actorLabel, statusLabel } from '@/shared/lib/labels'
import { RelativeTime } from '@/shared/ui/relative-time'
import { StatusDot } from '@/shared/ui/status-badge'

/** Status history newest first; the persisted timeline is what the dashboard replays after reconnecting. */
export function TaskTimeline({ entries }: { entries: TimelineEntry[] }) {
  const newestFirst = [...entries].sort((a, b) => b.sequence - a.sequence)

  return (
    <ol className="relative space-y-4">
      <span className="bg-border absolute top-2 bottom-2 left-[3px] w-px" aria-hidden="true" />
      {newestFirst.map((entry) => (
        <li key={entry.sequence} className="relative flex gap-3 pl-0">
          <StatusDot status={entry.to} className="mt-1.5 ring-4 ring-card" />
          <div className="min-w-0 flex-1 space-y-0.5">
            <div className="flex flex-wrap items-baseline justify-between gap-x-3">
              <p className="text-sm font-medium">{statusLabel(entry.to)}</p>
              <RelativeTime value={entry.occurredAt} className="text-muted-foreground text-xs" />
            </div>
            {entry.reason && <p className="text-muted-foreground text-sm break-words">{entry.reason}</p>}
            <p className="text-muted-foreground/80 text-xs">{actorLabel(entry.actorKind)}</p>
          </div>
        </li>
      ))}
    </ol>
  )
}
