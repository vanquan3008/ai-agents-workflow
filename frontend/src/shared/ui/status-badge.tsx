import { cn } from '@/lib/utils'
import { statusLabel } from '@/shared/lib/format'

type Tone = 'neutral' | 'info' | 'warning' | 'success' | 'danger'

const TONE: Record<string, Tone> = {
  PENDING: 'neutral',
  PLANNING: 'info',
  IN_PROGRESS: 'info',
  TESTING: 'info',
  REVIEWING: 'info',
  PUBLISHING: 'info',
  RUNNING: 'info',
  NEEDS_CLARIFICATION: 'warning',
  WAITING_FOR_APPROVAL: 'warning',
  BLOCKED: 'warning',
  COMPLETED: 'success',
  APPROVED: 'success',
  SUCCEEDED: 'success',
  FAILED: 'danger',
  REJECTED: 'danger',
  TIMED_OUT: 'danger',
  DENIED: 'danger',
  BUDGET_EXCEEDED: 'danger',
  CANCELLED: 'neutral',
  EXPIRED: 'neutral',
  SUPERSEDED: 'neutral',
}

const TONE_CLASSES: Record<Tone, string> = {
  neutral: 'bg-muted text-muted-foreground ring-border',
  info: 'bg-sky-500/10 text-sky-700 ring-sky-500/20 dark:text-sky-300',
  warning: 'bg-amber-500/10 text-amber-700 ring-amber-500/25 dark:text-amber-300',
  success: 'bg-emerald-500/10 text-emerald-700 ring-emerald-500/20 dark:text-emerald-300',
  danger: 'bg-rose-500/10 text-rose-700 ring-rose-500/20 dark:text-rose-300',
}

const DOT_CLASSES: Record<Tone, string> = {
  neutral: 'bg-muted-foreground/60',
  info: 'bg-sky-500 animate-pulse',
  warning: 'bg-amber-500',
  success: 'bg-emerald-500',
  danger: 'bg-rose-500',
}

export function StatusBadge({ status, className }: { status: string; className?: string }) {
  const tone = TONE[status] ?? 'neutral'
  return (
    <span
      data-status={status}
      className={cn('inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap ring-1 ring-inset', TONE_CLASSES[tone], className)}
    >
      <span className={cn('size-1.5 rounded-full', DOT_CLASSES[tone])} aria-hidden="true" />
      {statusLabel(status)}
    </span>
  )
}
