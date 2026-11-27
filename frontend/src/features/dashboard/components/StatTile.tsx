import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { formatNumber } from '@/shared/lib/format'
import type { StatusTone } from '@/shared/ui/status-tone'

const ACCENT_CLASSES: Record<StatusTone, string> = {
  neutral: 'text-muted-foreground bg-muted',
  info: 'text-info bg-info/10',
  warning: 'text-warning bg-warning/12',
  success: 'text-success bg-success/10',
  danger: 'text-danger bg-danger/10',
}

interface StatTileProps {
  label: string
  value: number | undefined
  icon: ReactNode
  to: string
  tone?: StatusTone
  hint?: string
}

/** Compact, clickable counter backed by a real API total; never shows placeholder numbers. */
export function StatTile({ label, value, icon, to, tone = 'neutral', hint }: StatTileProps) {
  return (
    <Link
      to={to}
      className="bg-card hover:border-foreground/15 focus-visible:ring-ring/50 group flex items-center gap-3 rounded-lg border p-3 shadow-xs transition-colors outline-none focus-visible:ring-3"
    >
      <span className={cn('flex size-8 shrink-0 items-center justify-center rounded-md [&_svg]:size-4', ACCENT_CLASSES[tone])}>{icon}</span>
      <span className="min-w-0 flex-1">
        <span className="text-muted-foreground block truncate text-xs">{label}</span>
        {value === undefined ? (
          <Skeleton className="mt-1 h-5 w-10" />
        ) : (
          <span className="block text-lg leading-6 font-semibold tabular-nums">{formatNumber(value)}</span>
        )}
      </span>
      {hint && <span className="text-muted-foreground hidden text-[11px] xl:block">{hint}</span>}
    </Link>
  )
}
