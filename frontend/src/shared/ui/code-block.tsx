import { cn } from '@/lib/utils'
import { CopyButton } from './copy-button'

interface CodeBlockProps {
  code: string
  label?: string
  className?: string
  maxHeightClassName?: string
}

/** Monospace block for JSON, diffs and logs; scrolls inside itself so long lines never stretch the layout. */
export function CodeBlock({ code, label, className, maxHeightClassName = 'max-h-80' }: CodeBlockProps) {
  return (
    <figure className={cn('bg-muted/40 min-w-0 overflow-hidden rounded-md border', className)}>
      {label && (
        <figcaption className="text-muted-foreground flex h-8 items-center justify-between border-b px-3 text-xs font-medium">
          <span>{label}</span>
          <CopyButton value={code} label={`Sao chép ${label.toLowerCase()}`} />
        </figcaption>
      )}
      <pre className={cn('scrollbar-thin overflow-auto p-3 font-mono text-xs leading-relaxed', maxHeightClassName)}>
        <code>{code}</code>
      </pre>
    </figure>
  )
}
