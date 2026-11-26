import { cn } from '@/lib/utils'
import { statusLabel } from '@/shared/lib/labels'
import { isLiveStatus, toneOf, type StatusTone } from './status-tone'

const TONE_CLASSES: Record<StatusTone, string> = {
  neutral: 'bg-neutral/10 text-muted-foreground ring-neutral/20',
  info: 'bg-info/10 text-info ring-info/25',
  warning: 'bg-warning/12 text-warning ring-warning/30',
  success: 'bg-success/10 text-success ring-success/25',
  danger: 'bg-danger/10 text-danger ring-danger/25',
}

const DOT_CLASSES: Record<StatusTone, string> = {
  neutral: 'bg-neutral',
  info: 'bg-info',
  warning: 'bg-warning',
  success: 'bg-success',
  danger: 'bg-danger',
}

export function StatusDot({ status, tone: toneOverride, className }: { status: string; tone?: StatusTone; className?: string }) {
  const tone = toneOverride ?? toneOf(status)
  return (
    <span className={cn('relative inline-flex size-2 shrink-0', className)} aria-hidden="true">
      {isLiveStatus(status) && <span className={cn('absolute inset-0 animate-ping rounded-full opacity-60', DOT_CLASSES[tone])} />}
      <span className={cn('relative inline-flex size-2 rounded-full', DOT_CLASSES[tone])} />
    </span>
  )
}

interface StatusBadgeProps {
  status: string
  /** Overrides for statuses whose meaning differs by entity (e.g. a pending approval needs attention). */
  label?: string
  tone?: StatusTone
  className?: string
}

export function StatusBadge({ status, label, tone: toneOverride, className }: StatusBadgeProps) {
  const tone = toneOverride ?? toneOf(status)
  return (
    <span
      data-status={status}
      className={cn(
        'inline-flex h-5 items-center gap-1.5 rounded-md px-1.5 text-xs font-medium whitespace-nowrap ring-1 ring-inset',
        TONE_CLASSES[tone],
        className,
      )}
    >
      <StatusDot status={status} tone={tone} className="size-1.5 [&>span]:size-1.5" />
      {label ?? statusLabel(status)}
    </span>
  )
}
