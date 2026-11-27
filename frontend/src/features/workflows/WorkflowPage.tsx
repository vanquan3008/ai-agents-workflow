import { History, Lock } from 'lucide-react'
import { useParams } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { PageHeader } from '@/shared/ui/page-header'
import { Panel } from '@/shared/ui/panel'
import { ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { useWorkflow, useWorkflowRevisions } from './api'
import { RevisionHistory } from './components/RevisionHistory'
import { WorkflowEditor } from './components/WorkflowEditor'

export default function WorkflowPage() {
  const { repositoryId = '' } = useParams()
  const query = useWorkflow(repositoryId)
  const revisions = useWorkflowRevisions(repositoryId)

  if (query.isPending) return <LoadingBlock rows={8} />
  if (query.isError) return <ErrorBlock error={query.error} onRetry={() => void query.refetch()} />
  const workflow = query.data

  return (
    <article>
      <PageHeader
        eyebrow={
          <>
            <Badge variant="outline" className="rounded-md font-mono">
              v{workflow.version}
            </Badge>
            {workflow.isDefault && (
              <Badge variant="secondary" className="rounded-md">
                Mặc định hệ thống
              </Badge>
            )}
          </>
        }
        title="Quy trình agent"
        description={
          <>
            <span className="text-foreground font-mono">{workflow.repositoryFullName}</span> · Task giữ ảnh chụp quy trình lúc bắt đầu, nên
            thay đổi chỉ áp dụng cho task mới. Tùy chỉnh cho Coding, Reviewer, duyệt diff và PR được lưu và kiểm tra ngay, có hiệu lực khi
            các bước đó được triển khai.
          </>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_22rem] xl:grid-cols-[minmax(0,1fr)_24rem]">
        <div className="min-w-0">
          <WorkflowEditor key={workflow.version} workflow={workflow} />
        </div>

        <aside className="min-w-0 space-y-4 lg:sticky lg:top-16 lg:self-start">
          <Panel title="Luôn được hệ thống áp dụng" icon={<Lock />}>
            <ul className="space-y-2 text-sm">
              {workflow.limits.invariants.map((invariant) => (
                <li key={invariant} className="text-muted-foreground flex gap-2">
                  <span className="bg-primary/60 mt-2 size-1 shrink-0 rounded-full" aria-hidden="true" />
                  <span className="text-pretty">{invariant}</span>
                </li>
              ))}
            </ul>
          </Panel>
          <Panel title="Lịch sử thay đổi" icon={<History />}>
            {revisions.isPending && <LoadingBlock rows={3} />}
            {revisions.isError && <ErrorBlock error={revisions.error} onRetry={() => void revisions.refetch()} />}
            {revisions.data?.length === 0 && <p className="text-muted-foreground text-sm">Đang dùng quy trình mặc định; chưa có phiên bản nào.</p>}
            {revisions.data && revisions.data.length > 0 && <RevisionHistory revisions={revisions.data} />}
          </Panel>
        </aside>
      </div>
    </article>
  )
}
