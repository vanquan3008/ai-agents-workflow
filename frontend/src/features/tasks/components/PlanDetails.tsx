import { AlertTriangle, CheckCircle2, FileCode2, ShieldAlert } from 'lucide-react'
import type { ReactNode } from 'react'
import { Badge } from '@/components/ui/badge'
import type { TaskPlan } from '@/shared/api/types'
import { cn } from '@/lib/utils'
import { policyOutcomeLabel, sensitiveAreaLabel } from '@/shared/lib/labels'

function PlanSection({ title, icon, children }: { title: string; icon: ReactNode; children: ReactNode }) {
  return (
    <section className="space-y-2">
      <h3 className="text-muted-foreground flex items-center gap-1.5 text-xs font-medium tracking-wide uppercase [&_svg]:size-3.5">
        {icon}
        {title}
      </h3>
      {children}
    </section>
  )
}

const OUTCOME_CLASSES: Record<TaskPlan['policyOutcome'], string> = {
  AUTO_APPROVED: 'bg-success/10 text-success',
  REQUIRES_APPROVAL: 'bg-warning/12 text-warning',
  NEEDS_REVIEW: 'bg-warning/12 text-warning',
}

/** Read-only rendering of a plan version: summary, steps, acceptance criteria, files, risks and the policy verdict. */
export function PlanDetails({ plan, showPolicy = true }: { plan: TaskPlan; showPolicy?: boolean }) {
  return (
    <div className="space-y-5">
      <p className="text-sm leading-relaxed text-pretty">{plan.summary}</p>

      <PlanSection title="Các bước" icon={<CheckCircle2 />}>
        <ol className="space-y-1.5">
          {plan.steps.map((step, index) => (
            <li key={step} className="flex gap-3 text-sm">
              <span className="bg-muted text-muted-foreground flex size-5 shrink-0 items-center justify-center rounded font-mono text-[11px]">
                {index + 1}
              </span>
              <span className="min-w-0 pt-px">{step}</span>
            </li>
          ))}
        </ol>
      </PlanSection>

      {plan.acceptanceCriteria.length > 0 && (
        <PlanSection title="Tiêu chí nghiệm thu" icon={<CheckCircle2 />}>
          <ul className="list-disc space-y-1 pl-5 text-sm marker:text-muted-foreground">
            {plan.acceptanceCriteria.map((criterion) => (
              <li key={criterion}>{criterion}</li>
            ))}
          </ul>
        </PlanSection>
      )}

      <PlanSection title="File dự kiến sửa" icon={<FileCode2 />}>
        <ul className="bg-muted/40 divide-y rounded-md border font-mono text-xs">
          {plan.affectedPaths.map((path) => (
            <li key={path} className="truncate px-3 py-1.5" title={path}>
              {path}
            </li>
          ))}
        </ul>
      </PlanSection>

      {plan.risks.length > 0 && (
        <PlanSection title="Rủi ro" icon={<AlertTriangle />}>
          <ul className="list-disc space-y-1 pl-5 text-sm marker:text-warning">
            {plan.risks.map((risk) => (
              <li key={risk}>{risk}</li>
            ))}
          </ul>
        </PlanSection>
      )}

      {showPolicy && (
        <PlanSection title={`Chính sách v${plan.policyVersion}`} icon={<ShieldAlert />}>
          <div className="flex flex-wrap items-center gap-1.5">
            <span className={cn('inline-flex h-5 items-center rounded-md px-1.5 text-xs font-medium', OUTCOME_CLASSES[plan.policyOutcome])}>
              {policyOutcomeLabel(plan.policyOutcome)}
            </span>
            {plan.sensitiveAreas.map((area) => (
              <Badge key={area} variant="outline" className="rounded-md">
                {sensitiveAreaLabel(area)}
              </Badge>
            ))}
          </div>
          {plan.policyReasons.length > 0 && (
            <ul className="text-muted-foreground space-y-1 text-xs">
              {plan.policyReasons.map((reason) => (
                <li key={reason} className="break-words">
                  • {reason}
                </li>
              ))}
            </ul>
          )}
        </PlanSection>
      )}
    </div>
  )
}
