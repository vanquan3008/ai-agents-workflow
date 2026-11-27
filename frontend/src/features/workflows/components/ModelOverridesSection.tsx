import { Cpu } from 'lucide-react'
import { Input } from '@/components/ui/input'
import type { WorkflowDefinition } from '@/shared/api/types'
import { Panel } from '@/shared/ui/panel'
import { SettingRow } from './SettingRow'

const STEPS = [
  { key: 'planner', label: 'Planner' },
  { key: 'coding', label: 'Coding' },
  { key: 'reviewer', label: 'Reviewer' },
] as const

interface ModelOverridesSectionProps {
  definition: WorkflowDefinition
  onChange: (patch: Partial<WorkflowDefinition>) => void
}

export function ModelOverridesSection({ definition, onChange }: ModelOverridesSectionProps) {
  return (
    <Panel title="Model theo bước" description="Để trống để dùng model mặc định; model phải có đơn giá cấu hình." icon={<Cpu />}>
      <div className="divide-y">
        {STEPS.map((step) => (
          <SettingRow key={step.key} label={step.label} htmlFor={`model-${step.key}`}>
            <Input
              id={`model-${step.key}`}
              className="font-mono"
              placeholder="mặc định"
              value={definition.models[step.key] ?? ''}
              onChange={(event) => onChange({ models: { ...definition.models, [step.key]: event.target.value.trim() || null } })}
            />
          </SettingRow>
        ))}
      </div>
    </Panel>
  )
}
