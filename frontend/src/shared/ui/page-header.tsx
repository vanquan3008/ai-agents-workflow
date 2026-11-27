import type { ReactNode } from 'react'
import { ArrowLeft } from 'lucide-react'
import { Link } from 'react-router'
import { cn } from '@/lib/utils'

interface PageHeaderProps {
  title: ReactNode
  description?: ReactNode
  actions?: ReactNode
  /** Small line above the title, e.g. a status badge or identifier. */
  eyebrow?: ReactNode
  backTo?: { to: string; label: string }
  className?: string
}

export function PageHeader({ title, description, actions, eyebrow, backTo, className }: PageHeaderProps) {
  return (
    <header className={cn('flex flex-col gap-4 pb-6 sm:flex-row sm:items-start sm:justify-between', className)}>
      <div className="min-w-0 space-y-1.5">
        {backTo && (
          <Link
            to={backTo.to}
            className="text-muted-foreground hover:text-foreground inline-flex items-center gap-1 text-xs font-medium transition-colors"
          >
            <ArrowLeft className="size-3.5" aria-hidden="true" />
            {backTo.label}
          </Link>
        )}
        {eyebrow && <div className="flex flex-wrap items-center gap-2">{eyebrow}</div>}
        <h1 className="font-heading text-xl leading-tight font-semibold tracking-tight text-balance break-words">{title}</h1>
        {description && <div className="text-muted-foreground max-w-3xl text-sm text-pretty">{description}</div>}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
    </header>
  )
}
