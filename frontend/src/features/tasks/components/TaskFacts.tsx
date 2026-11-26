import { ExternalLink } from 'lucide-react'
import type { TaskDetail } from '@/shared/api/types'
import { formatDateTime, formatNumber, formatUsd, shortSha } from '@/shared/lib/format'
import { channelLabel } from '@/shared/lib/labels'
import { CopyButton } from '@/shared/ui/copy-button'
import { DescriptionList } from '@/shared/ui/description-list'

export function TaskFacts({ task }: { task: TaskDetail }) {
  return (
    <DescriptionList
      items={[
        { label: 'Repository', value: task.repositoryFullName, mono: true },
        {
          label: 'Base commit',
          mono: true,
          value: task.baseCommitSha ? (
            <span className="inline-flex items-center gap-1">
              {shortSha(task.baseCommitSha)}
              <CopyButton value={task.baseCommitSha} label="Sao chép commit" />
            </span>
          ) : (
            '—'
          ),
        },
        { label: 'Nhánh', value: task.workBranch ?? '—', mono: true },
        {
          label: 'Pull request',
          value: task.pullRequest ? (
            <a
              className="text-primary inline-flex items-center gap-1 font-medium hover:underline"
              href={task.pullRequest.url}
              target="_blank"
              rel="noreferrer"
            >
              #{task.pullRequest.number} <ExternalLink className="size-3" />
            </a>
          ) : (
            '—'
          ),
        },
        { label: 'Chi phí ước tính', value: formatUsd(task.usage.estimatedCostUsd) },
        { label: 'Token vào / ra', value: `${formatNumber(task.usage.inputTokens)} / ${formatNumber(task.usage.outputTokens)}` },
        { label: 'Kênh', value: channelLabel(task.channel) },
        { label: 'Số lần thử lại', value: formatNumber(task.retryCount) },
        { label: 'Tạo lúc', value: formatDateTime(task.createdAt) },
        { label: 'Hoàn tất', value: formatDateTime(task.completedAt) },
      ]}
    />
  )
}
