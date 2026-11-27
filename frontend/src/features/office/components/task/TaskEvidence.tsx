import type { ReactNode } from 'react'
import { ExternalLink, GitPullRequest } from 'lucide-react'
import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useTaskExecutions } from '@/features/tasks/api'
import type { TaskDetail } from '@/shared/api/types'
import { formatDuration, formatUsd, shortSha } from '@/shared/lib/format'
import { RelativeTime } from '@/shared/ui/relative-time'
import { ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { StatusBadge } from '@/shared/ui/status-badge'
import { useTaskApprovals } from '../../api'
import { primaryAction, type PrimaryAction } from '../../model/actions'
import {
  IMPLEMENTED_STATIONS,
  STATION_DESCRIPTIONS,
  STATION_EXECUTION_STEP,
  STATION_LABELS,
  waitingFor,
  type StationState,
} from '../../model/stations'
import type { WorkflowStation } from '../../types'
import { StationStateBadge } from '../floor/RoomVisuals'

export function PrimaryActionButton({ action, className }: { action: PrimaryAction; className?: string }) {
  if (action.external) {
    return (
      <Button asChild size="sm" className={className}>
        <a href={action.to} target="_blank" rel="noreferrer noopener">
          <GitPullRequest /> {action.label} <ExternalLink />
        </a>
      </Button>
    )
  }
  return (
    <Button asChild size="sm" className={className}>
      <Link to={action.to}>{action.label}</Link>
    </Button>
  )
}

/** Compact summary of the selected task with its next action; shown first on phones. */
export function SelectedTaskSummary({ task, station }: { task: TaskDetail; station: WorkflowStation }) {
  const approvals = useTaskApprovals(task.id, task.status === 'WAITING_FOR_APPROVAL')
  const waiting = waitingFor(task.status)
  return (
    <div className="space-y-3">
      <div className="space-y-1.5">
        <div className="flex flex-wrap items-center gap-2">
          <StatusBadge status={task.status} />
          <span className="text-muted-foreground text-xs">{STATION_LABELS[station]}</span>
        </div>
        <h2 className="text-sm leading-snug font-medium break-words">{task.title}</h2>
        <p className="text-muted-foreground text-xs">
          {task.repositoryFullName} · <span className="font-mono">{shortSha(task.baseCommitSha)}</span> · lần thử {task.retryCount + 1}
        </p>
        {waiting && <p className="text-warning text-xs font-medium">{waiting}</p>}
        {task.statusReason && <p className="text-muted-foreground line-clamp-3 text-xs break-words">{task.statusReason}</p>}
      </div>
      <PrimaryActionButton action={primaryAction(task, approvals.data?.items)} className="w-full sm:w-auto" />
    </div>
  )
}

interface StationEvidenceProps {
  task: TaskDetail
  station: WorkflowStation
  state: StationState
  /** Whether the task currently stands at this station (derived from the persisted timeline by the caller). */
  isCurrent: boolean
}

/** Evidence recorded for one station of the selected task, read from executions, the plan and approvals. */
export function StationEvidence({ task, station, state, isCurrent }: StationEvidenceProps) {
  const executions = useTaskExecutions(task.id)
  const approvals = useTaskApprovals(task.id, station === 'APPROVAL')
  const step = STATION_EXECUTION_STEP[station]
  const stepExecutions = (executions.data ?? []).filter((execution) => execution.step === step).reverse()
  const waiting = isCurrent ? waitingFor(task.status) : null

  return (
    <div className="space-y-4">
      <div className="space-y-1.5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="text-sm font-medium">{STATION_LABELS[station]}</h3>
          <StationStateBadge state={state} />
        </div>
        <p className="text-muted-foreground text-xs leading-relaxed">{STATION_DESCRIPTIONS[station]}</p>
        {waiting && <p className="text-warning text-xs font-medium">{waiting}</p>}
      </div>

      {station === 'PLANNING' && task.currentPlan && (
        <EvidenceSection title={`Kế hoạch v${task.currentPlan.version}`}>
          <p className="text-sm leading-relaxed break-words">{task.currentPlan.summary}</p>
          {task.currentPlan.affectedPaths.length > 0 && (
            <ul className="mt-2 space-y-0.5">
              {task.currentPlan.affectedPaths.slice(0, 5).map((path) => (
                <li key={path} className="text-muted-foreground truncate font-mono text-xs">
                  {path}
                </li>
              ))}
            </ul>
          )}
        </EvidenceSection>
      )}

      {station === 'APPROVAL' && (
        <EvidenceSection title="Yêu cầu phê duyệt">
          {approvals.isPending && <LoadingBlock rows={2} />}
          {approvals.isError && <ErrorBlock error={approvals.error} onRetry={() => void approvals.refetch()} />}
          {approvals.data?.items.length === 0 && <p className="text-muted-foreground text-xs">Chưa có yêu cầu phê duyệt cho task này.</p>}
          <ul className="space-y-2">
            {approvals.data?.items.map((approval) => (
              <li key={approval.id} className="flex items-center justify-between gap-2 text-xs">
                <span className="min-w-0">
                  <span className="font-medium">{approval.kind === 'PLAN' ? `Kế hoạch v${approval.planVersion}` : `Diff ${shortSha(approval.commitSha)}`}</span>
                  <span className="text-muted-foreground block">
                    yêu cầu <RelativeTime value={approval.requestedAt} />
                  </span>
                </span>
                <Link to={`/approvals/${approval.id}`} className="shrink-0">
                  <StatusBadge status={approval.status} tone={approval.status === 'PENDING' ? 'warning' : undefined} label={approval.status === 'PENDING' ? 'Chờ quyết định' : undefined} />
                </Link>
              </li>
            ))}
          </ul>
        </EvidenceSection>
      )}

      {station === 'PUBLISHING' && task.pullRequest && (
        <EvidenceSection title="Pull request">
          <a href={task.pullRequest.url} target="_blank" rel="noreferrer noopener" className="text-sm font-medium underline-offset-2 hover:underline">
            #{task.pullRequest.number} · {task.pullRequest.headBranch}
          </a>
          <p className="text-muted-foreground font-mono text-xs">{shortSha(task.pullRequest.headCommitSha)}</p>
        </EvidenceSection>
      )}

      {step && (
        <EvidenceSection title="Lần chạy">
          {executions.isPending && <LoadingBlock rows={2} />}
          {executions.isError && <ErrorBlock error={executions.error} onRetry={() => void executions.refetch()} />}
          {executions.data && stepExecutions.length === 0 && (
            <div className="space-y-1.5">
              {!IMPLEMENTED_STATIONS.has(station) && (
                <Badge variant="outline" className="rounded-md">
                  Chưa triển khai
                </Badge>
              )}
              <p className="text-muted-foreground text-xs">
                {IMPLEMENTED_STATIONS.has(station) ? 'Chưa có lần chạy nào cho bước này.' : 'Worker chưa thực thi bước này trong bản pilot hiện tại.'}
              </p>
            </div>
          )}
          <ul className="space-y-2">
            {stepExecutions.slice(0, 3).map((execution) => (
              <li key={execution.id}>
                <Link
                  to={`/executions/${execution.id}`}
                  className="hover:bg-accent/50 focus-visible:ring-ring -mx-2 flex items-center justify-between gap-2 rounded-md px-2 py-1.5 focus-visible:ring-[3px] focus-visible:outline-none"
                >
                  <span className="min-w-0 text-xs">
                    <span className="block font-medium">Lần {execution.attempt}</span>
                    <span className="text-muted-foreground block truncate">
                      {execution.model} · {formatDuration(execution.durationSeconds)} · {formatUsd(execution.usage.estimatedCostUsd)}
                    </span>
                  </span>
                  <StatusBadge status={execution.status} />
                </Link>
              </li>
            ))}
          </ul>
        </EvidenceSection>
      )}
    </div>
  )
}

function EvidenceSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-2 border-t pt-3">
      <h4 className="text-muted-foreground text-xs font-medium tracking-wide uppercase">{title}</h4>
      {children}
    </section>
  )
}
