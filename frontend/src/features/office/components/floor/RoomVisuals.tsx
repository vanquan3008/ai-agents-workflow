import {
  BookOpen,
  Box,
  CircleCheck,
  CircleDashed,
  CircleX,
  ClipboardList,
  Code,
  Coffee,
  FlaskConical,
  GitCompare,
  Hourglass,
  Inbox,
  Loader,
  Rocket,
  Server,
  ShieldCheck,
  type LucideIcon,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { STATE_LABELS, type StationState } from '../../model/stations'

const PRESET_ICONS: Record<string, LucideIcon> = {
  'planning-table': ClipboardList,
  workbench: Code,
  'test-bench': FlaskConical,
  'diff-display': GitCompare,
  'meeting-table': ShieldCheck,
  'release-desk': Rocket,
  'reception-desk': Inbox,
  bookshelf: BookOpen,
  lounge: Coffee,
  'server-rack': Server,
}

export function PresetIcon({ preset, className }: { preset: string; className?: string }) {
  const Icon = PRESET_ICONS[preset] ?? Box
  return <Icon className={className} aria-hidden="true" />
}

const STATE_ICONS: Record<StationState, LucideIcon> = {
  active: Loader,
  waiting: Hourglass,
  failed: CircleX,
  stopped: CircleX,
  done: CircleCheck,
  passed: CircleCheck,
  idle: CircleDashed,
}

const STATE_CLASSES: Record<StationState, string> = {
  active: 'bg-info/10 text-info ring-info/25',
  waiting: 'bg-warning/12 text-warning ring-warning/30',
  failed: 'bg-danger/10 text-danger ring-danger/25',
  stopped: 'bg-neutral/10 text-muted-foreground ring-neutral/20',
  done: 'bg-success/10 text-success ring-success/25',
  passed: 'bg-success/8 text-success ring-success/20',
  idle: 'bg-muted text-muted-foreground ring-border',
}

/** Text plus icon plus color for a station state, so state never depends on color alone. */
export function StationStateBadge({ state, className }: { state: StationState; className?: string }) {
  const Icon = STATE_ICONS[state]
  return (
    <span
      className={cn(
        'inline-flex h-5 items-center gap-1 rounded-md px-1.5 text-[11px] font-medium whitespace-nowrap ring-1 ring-inset',
        STATE_CLASSES[state],
        className,
      )}
    >
      <Icon className={cn('size-3', state === 'active' && 'motion-safe:animate-spin')} aria-hidden="true" />
      {STATE_LABELS[state]}
    </span>
  )
}
