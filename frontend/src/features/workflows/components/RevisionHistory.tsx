import { ChevronRight } from 'lucide-react'
import type { WorkflowRevision } from '@/shared/api/types'
import { CodeBlock } from '@/shared/ui/code-block'
import { RelativeTime } from '@/shared/ui/relative-time'

export function RevisionHistory({ revisions }: { revisions: WorkflowRevision[] }) {
  return (
    <ol className="-mx-1 divide-y">
      {revisions.map((revision) => (
        <li key={revision.version}>
          <details className="group">
            <summary className="hover:bg-muted/50 flex cursor-pointer list-none items-start gap-2 rounded-md px-1 py-2.5 transition-colors [&::-webkit-details-marker]:hidden">
              <ChevronRight className="text-muted-foreground mt-0.5 size-4 shrink-0 transition-transform group-open:rotate-90" />
              <div className="min-w-0 flex-1">
                <div className="flex items-baseline justify-between gap-2">
                  <span className="font-mono text-sm font-medium">v{revision.version}</span>
                  <RelativeTime value={revision.changedAt} className="text-muted-foreground text-xs" />
                </div>
                {revision.comment && <p className="text-muted-foreground line-clamp-2 text-xs break-words">{revision.comment}</p>}
              </div>
            </summary>
            <div className="px-1 pb-3">
              <CodeBlock code={JSON.stringify(revision.definition, null, 2)} label="Nội dung" maxHeightClassName="max-h-64" />
            </div>
          </details>
        </li>
      ))}
    </ol>
  )
}
