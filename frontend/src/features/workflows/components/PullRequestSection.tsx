import { GitPullRequest } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import type { WorkflowDefinition } from '@/shared/api/types'
import { Panel } from '@/shared/ui/panel'
import { SettingRow } from './SettingRow'

const splitList = (value: string) =>
  value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)

interface PullRequestSectionProps {
  definition: WorkflowDefinition
  onChange: (patch: Partial<WorkflowDefinition>) => void
}

export function PullRequestSection({ definition, onChange }: PullRequestSectionProps) {
  const pullRequest = definition.pullRequest
  const update = (patch: Partial<WorkflowDefinition['pullRequest']>) => onChange({ pullRequest: { ...pullRequest, ...patch } })

  return (
    <Panel title="Pull request" icon={<GitPullRequest />}>
      <div className="divide-y">
        <SettingRow label="Tiền tố tiêu đề" htmlFor="pr-prefix">
          <Input id="pr-prefix" maxLength={50} value={pullRequest.titlePrefix} onChange={(event) => update({ titlePrefix: event.target.value })} />
        </SettingRow>
        <SettingRow label="Nhãn" description="Cách nhau bằng dấu phẩy, tối đa 10." htmlFor="pr-labels">
          <Input id="pr-labels" defaultValue={pullRequest.labels.join(', ')} onChange={(event) => update({ labels: splitList(event.target.value) })} />
        </SettingRow>
        <SettingRow label="Người review" description="GitHub login, cách nhau bằng dấu phẩy." htmlFor="pr-reviewers">
          <Input
            id="pr-reviewers"
            className="font-mono"
            defaultValue={pullRequest.reviewers.join(', ')}
            onChange={(event) => update({ reviewers: splitList(event.target.value) })}
          />
        </SettingRow>
        <SettingRow label="Tạo PR dạng draft" htmlFor="pr-draft">
          <div className="flex sm:justify-end">
            <Switch id="pr-draft" checked={pullRequest.draft} onCheckedChange={(checked) => update({ draft: checked })} />
          </div>
        </SettingRow>
      </div>
    </Panel>
  )
}
