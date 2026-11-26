import { AlertCircle, Bookmark, Gauge, Wrench } from 'lucide-react'
import { Link, useParams } from 'react-router'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { formatDateTime, formatDuration, formatNumber, formatUsd, prettyJson } from '@/shared/lib/format'
import { stepLabel } from '@/shared/lib/labels'
import { CodeBlock } from '@/shared/ui/code-block'
import { DescriptionList } from '@/shared/ui/description-list'
import { PageHeader } from '@/shared/ui/page-header'
import { Panel } from '@/shared/ui/panel'
import { EmptyBlock, ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { StatusBadge } from '@/shared/ui/status-badge'
import { useExecution } from './api'
import { ToolExecutionList } from './components/ToolExecutionList'

export default function ExecutionPage() {
  const { executionId = '' } = useParams()
  const query = useExecution(executionId)

  if (query.isPending) return <LoadingBlock rows={6} />
  if (query.isError) return <ErrorBlock error={query.error} onRetry={() => void query.refetch()} />
  const { execution, checkpoint, toolExecutions } = query.data

  return (
    <article>
      <PageHeader
        backTo={{ to: `/tasks/${execution.taskId}`, label: 'Về task' }}
        eyebrow={<StatusBadge status={execution.status} />}
        title={
          <>
            {stepLabel(execution.step)} <span className="text-muted-foreground font-normal">· lần {execution.attempt}</span>
          </>
        }
        description={<span className="font-mono">{execution.model}</span>}
        actions={
          <Button asChild variant="outline">
            <Link to={`/tasks/${execution.taskId}`}>Mở task</Link>
          </Button>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_22rem] xl:grid-cols-[minmax(0,1fr)_24rem]">
        <div className="min-w-0 space-y-4">
          {execution.error && (
            <Alert variant="destructive" className="border-destructive/30 bg-destructive/5">
              <AlertCircle />
              <AlertTitle>Lần chạy kết thúc với lỗi</AlertTitle>
              <AlertDescription className="break-words whitespace-pre-wrap">{execution.error}</AlertDescription>
            </Alert>
          )}

          <Panel title="Lời gọi công cụ" description={`${toolExecutions.length} lời gọi`} icon={<Wrench />} flush>
            {toolExecutions.length === 0 ? (
              <EmptyBlock
                className="m-4 w-auto py-6"
                icon={<Wrench />}
                title="Bước này chưa gọi công cụ"
                description="Planner chỉ đọc cấu trúc repository; công cụ ghi file và chạy lệnh thuộc các bước sau."
              />
            ) : (
              <ToolExecutionList tools={toolExecutions} />
            )}
          </Panel>

          {checkpoint && (
            <Panel title="Checkpoint" description="Điểm lưu tiến độ để Worker khác tiếp tục khi lease hết hạn." icon={<Bookmark />}>
              <CodeBlock code={prettyJson(checkpoint)} label="JSON" />
            </Panel>
          )}
        </div>

        <aside className="min-w-0 lg:sticky lg:top-16 lg:self-start">
          <Panel title="Số liệu" icon={<Gauge />}>
            <DescriptionList
              items={[
                { label: 'Bắt đầu', value: formatDateTime(execution.startedAt) },
                { label: 'Kết thúc', value: formatDateTime(execution.finishedAt) },
                { label: 'Thời lượng', value: formatDuration(execution.durationSeconds) },
                { label: 'Chi phí ước tính', value: formatUsd(execution.usage.estimatedCostUsd) },
                { label: 'Token vào', value: formatNumber(execution.usage.inputTokens) },
                { label: 'Token ra', value: formatNumber(execution.usage.outputTokens) },
                { label: 'Token cache', value: formatNumber(execution.usage.cacheReadTokens) },
              ]}
            />
          </Panel>
        </aside>
      </div>
    </article>
  )
}
