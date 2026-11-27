import { ChevronRight, Wrench } from 'lucide-react'
import type { ToolExecution } from '@/shared/api/types'
import { formatDateTime, prettyJson } from '@/shared/lib/format'
import { CodeBlock } from '@/shared/ui/code-block'
import { StatusBadge } from '@/shared/ui/status-badge'

/** Each tool call collapses to one row; inputs and outputs expand on demand to keep long JSON out of the way. */
export function ToolExecutionList({ tools }: { tools: ToolExecution[] }) {
  return (
    <ul className="divide-y">
      {tools.map((tool) => (
        <li key={tool.id}>
          <details className="group">
            <summary className="hover:bg-muted/50 flex cursor-pointer list-none items-center gap-3 px-4 py-2.5 transition-colors [&::-webkit-details-marker]:hidden">
              <ChevronRight className="text-muted-foreground size-4 shrink-0 transition-transform group-open:rotate-90" />
              <span className="text-muted-foreground w-6 shrink-0 font-mono text-xs tabular-nums">{tool.sequence}</span>
              <span className="flex min-w-0 flex-1 items-center gap-2">
                <Wrench className="text-muted-foreground size-3.5 shrink-0" />
                <span className="truncate font-mono text-sm">{tool.toolName}</span>
              </span>
              {tool.exitCode !== null && <span className="text-muted-foreground hidden font-mono text-xs sm:inline">exit {tool.exitCode}</span>}
              <StatusBadge status={tool.status} />
            </summary>
            <div className="space-y-3 px-4 pt-1 pb-4 sm:pl-[4.25rem]">
              <p className="text-muted-foreground text-xs">
                {formatDateTime(tool.startedAt)} → {formatDateTime(tool.finishedAt)}
              </p>
              {tool.denialReason && <p className="text-destructive text-sm break-words">{tool.denialReason}</p>}
              <CodeBlock label="Đầu vào" code={prettyJson(tool.inputJson)} />
              {tool.outputJson && <CodeBlock label="Đầu ra" code={prettyJson(tool.outputJson)} />}
            </div>
          </details>
        </li>
      ))}
    </ul>
  )
}
