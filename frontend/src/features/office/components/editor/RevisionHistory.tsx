import { RotateCcw } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { formatDateTime } from '@/shared/lib/format'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { useDepartmentRevisions } from '../../api'

interface RevisionHistoryProps {
  projectId: string
  departmentId: string
  /** True when restoring would discard edits that are not saved yet. */
  hasUnsavedChanges: boolean
  restoring: boolean
  onRestore: (version: number) => void
}

/** Published versions, newest first. Restoring copies a version into a new draft; history itself never changes. */
export function RevisionHistory({ projectId, departmentId, hasUnsavedChanges, restoring, onRestore }: RevisionHistoryProps) {
  const revisions = useDepartmentRevisions(projectId, departmentId)

  if (revisions.isPending) return <LoadingBlock rows={2} />
  if (revisions.isError) return <ErrorBlock error={revisions.error} onRetry={() => void revisions.refetch()} />
  if (revisions.data.length === 0) return <p className="text-muted-foreground text-sm">Chưa xuất bản phiên bản nào.</p>

  return (
    <ol className="divide-y">
      {revisions.data.map((revision) => (
        <li key={revision.version} className="flex items-center justify-between gap-3 py-2.5 first:pt-0 last:pb-0">
          <div className="min-w-0 text-sm">
            <p className="flex items-center gap-2 font-medium">
              v{revision.version}
              {revision.isCurrent && (
                <Badge variant="secondary" className="rounded-md">
                  Đang hiển thị
                </Badge>
              )}
            </p>
            <p className="text-muted-foreground truncate text-xs">
              {revision.name} · {revision.roomCount} phòng · {revision.repositoryCount} repo
            </p>
            <p className="text-muted-foreground truncate text-xs">
              {revision.publishedByName || 'Không rõ'} · {formatDateTime(revision.publishedAt)}
            </p>
          </div>
          <ConfirmDialog
            trigger={
              <Button type="button" size="xs" variant="outline" disabled={restoring}>
                <RotateCcw /> Khôi phục
              </Button>
            }
            title={`Khôi phục phiên bản ${revision.version}?`}
            description={
              hasUnsavedChanges
                ? 'Thay đổi chưa lưu và bản nháp hiện tại sẽ được thay bằng cấu hình của phiên bản này. Bạn cần xuất bản để áp dụng.'
                : 'Bản nháp hiện tại sẽ được thay bằng cấu hình của phiên bản này. Bạn cần xuất bản để áp dụng.'
            }
            confirmLabel="Khôi phục vào bản nháp"
            onConfirm={() => onRestore(revision.version)}
          />
        </li>
      ))}
    </ol>
  )
}
