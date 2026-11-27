import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

export interface DescriptionItem {
  label: string
  value: ReactNode
  mono?: boolean
}

/** Label/value pairs in a compact two-column grid that wraps long values instead of overflowing. */
export function DescriptionList({ items, className }: { items: DescriptionItem[]; className?: string }) {
  return (
    <dl className={cn('grid grid-cols-[minmax(7rem,auto)_1fr] gap-x-4 gap-y-2.5 text-sm', className)}>
      {items.map((item) => (
        <div key={item.label} className="contents">
          <dt className="text-muted-foreground">{item.label}</dt>
          <dd className={cn('min-w-0 break-words', item.mono && 'font-mono text-[13px]')}>{item.value}</dd>
        </div>
      ))}
    </dl>
  )
}
