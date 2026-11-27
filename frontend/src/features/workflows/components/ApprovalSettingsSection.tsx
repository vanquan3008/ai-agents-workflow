import { ShieldCheck } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import type { ApprovalRequirement, FindingSeverity, WorkflowDefinition, WorkflowLimits } from '@/shared/api/types'
import { OptionSelect } from '@/shared/ui/option-select'
import { Panel } from '@/shared/ui/panel'
import { SettingRow } from './SettingRow'

const APPROVAL_LABELS: Record<ApprovalRequirement, string> = {
  ON_SENSITIVE: 'Khi chạm vùng nhạy cảm',
  ALWAYS: 'Luôn duyệt',
}

const SEVERITY_LABELS: Record<FindingSeverity, string> = {
  LOW: 'Thấp trở lên',
  MEDIUM: 'Trung bình trở lên',
  HIGH: 'Cao',
}

interface SectionProps {
  definition: WorkflowDefinition
  limits: WorkflowLimits
  onChange: (patch: Partial<WorkflowDefinition>) => void
}

export function ApprovalSettingsSection({ definition, limits, onChange }: SectionProps) {
  const approvalOptions = limits.approvalOptions.map((option) => ({ value: option, label: APPROVAL_LABELS[option] }))

  return (
    <Panel title="Phê duyệt và kiểm tra" icon={<ShieldCheck />}>
      <div className="divide-y">
        <SettingRow label="Duyệt kế hoạch" description="Vùng nhạy cảm luôn cần duyệt; có thể siết thành luôn duyệt." htmlFor="plan-approval">
          <OptionSelect
            id="plan-approval"
            value={definition.planApproval}
            onValueChange={(value) => onChange({ planApproval: value as ApprovalRequirement })}
            options={approvalOptions}
          />
        </SettingRow>
        <SettingRow label="Duyệt diff" description="Áp dụng trước khi mở pull request." htmlFor="diff-approval">
          <OptionSelect
            id="diff-approval"
            value={definition.diffApproval}
            onValueChange={(value) => onChange({ diffApproval: value as ApprovalRequirement })}
            options={approvalOptions}
          />
        </SettingRow>
        <SettingRow label="Số lần thử Planner" description={`Trong một lượt lập kế hoạch, tối đa ${limits.plannerMaxAttemptsCap}.`} htmlFor="planner-attempts">
          <Input
            id="planner-attempts"
            type="number"
            min={1}
            max={limits.plannerMaxAttemptsCap}
            required
            value={definition.plannerMaxAttempts}
            onChange={(event) => onChange({ plannerMaxAttempts: Number(event.target.value) })}
          />
        </SettingRow>
        <SettingRow label="Số vòng sửa và test" description={`Trần hệ thống: ${limits.maxFixIterationsCap} vòng.`} htmlFor="fix-iterations">
          <Input
            id="fix-iterations"
            type="number"
            min={0}
            max={limits.maxFixIterationsCap}
            required
            value={definition.maxFixIterations}
            onChange={(event) => onChange({ maxFixIterations: Number(event.target.value) })}
          />
        </SettingRow>
        <SettingRow label="Phát hiện chặn xuất bản" description="Phát hiện của Reviewer từ mức này trở lên chặn pull request." htmlFor="blocking-severity">
          <OptionSelect
            id="blocking-severity"
            value={definition.reviewerBlockingSeverity}
            onValueChange={(value) => onChange({ reviewerBlockingSeverity: value as FindingSeverity })}
            options={limits.severityOptions.map((option) => ({ value: option, label: SEVERITY_LABELS[option] }))}
          />
        </SettingRow>
        <SettingRow label="Yêu cầu test hồi quy" description="Diff phải có thay đổi file test mới được xuất bản." htmlFor="regression-test">
          <div className="flex sm:justify-end">
            <Switch
              id="regression-test"
              checked={definition.requireRegressionTest}
              onCheckedChange={(checked) => onChange({ requireRegressionTest: checked })}
            />
          </div>
        </SettingRow>
      </div>
    </Panel>
  )
}
