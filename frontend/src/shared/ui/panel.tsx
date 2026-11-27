import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface PanelProps {
  title?: ReactNode
  description?: ReactNode
  actions?: ReactNode
  icon?: ReactNode
  children: ReactNode
  className?: string
  contentClassName?: string
  /** Removes body padding so tables and lists can run edge to edge. */
  flush?: boolean
}

/** Bordered surface with an optional compact header; the building block of every detail page. */
export function Panel({ title, description, actions, icon, children, className, contentClassName, flush = false }: PanelProps) {
  const hasHeader = title !== undefined || actions !== undefined
  return (
    <section className={cn('bg-card text-card-foreground min-w-0 rounded-lg border shadow-xs', className)}>
      {hasHeader && (
        <header className="flex min-h-11 items-center justify-between gap-3 border-b px-4 py-2.5">
          <div className="flex min-w-0 items-center gap-2">
            {icon && <span className="text-muted-foreground [&_svg]:size-4">{icon}</span>}
            <div className="min-w-0">
              {title && <h2 className="truncate text-sm font-medium">{title}</h2>}
              {description && <p className="text-muted-foreground truncate text-xs">{description}</p>}
            </div>
          </div>
          {actions && <div className="flex shrink-0 items-center gap-1.5">{actions}</div>}
        </header>
      )}
      <div className={cn(!flush && 'p-4', contentClassName)}>{children}</div>
    </section>
  )
}
