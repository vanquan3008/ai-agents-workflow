import { useState } from 'react'
import { Building2, DoorOpen, GitBranch, LayoutGrid, Settings2 } from 'lucide-react'
import { Link, useParams } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { useProject } from '@/features/projects/api'
import { formatNumber } from '@/shared/lib/format'
import { PageHeader } from '@/shared/ui/page-header'
import { RelativeTime } from '@/shared/ui/relative-time'
import { EmptyBlock, ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { useDepartmentDirectory } from './api'
import { CreateDepartmentDialog } from './components/directory/CreateDepartmentDialog'
import { THEME_LABELS, themeStyle } from './model/presentation'
import type { DepartmentSummary } from './types'

export default function OfficeDirectoryPage() {
  const { projectId = '' } = useParams()
  const project = useProject(projectId)
  const [showArchived, setShowArchived] = useState(false)
  const directory = useDepartmentDirectory(projectId, showArchived)
  const canManage = directory.data?.canManage ?? false

  return (
    <article>
      <PageHeader
        backTo={{ to: `/projects/${projectId}`, label: project.data?.name ?? 'Project' }}
        title="Phòng kỹ thuật"
        description="Mỗi phòng ban theo dõi một nhóm repository và hiển thị task qua các bước của quy trình agent."
        actions={canManage ? <CreateDepartmentDialog projectId={projectId} /> : undefined}
      />

      {canManage && (
        <div className="mb-4 flex items-center gap-2">
          <Switch id="show-archived" checked={showArchived} onCheckedChange={setShowArchived} />
          <Label htmlFor="show-archived" className="text-sm font-normal">
            Hiện phòng ban đã lưu trữ
          </Label>
        </div>
      )}

      {directory.isPending && <LoadingBlock rows={4} />}
      {directory.isError && <ErrorBlock error={directory.error} onRetry={() => void directory.refetch()} />}
      {directory.data?.items.length === 0 && (
        <EmptyBlock
          icon={<Building2 />}
          title="Chưa có phòng ban"
          description={
            canManage
              ? 'Tạo phòng ban từ mẫu Phát triển phần mềm, Kiểm thử hoặc Nền tảng & vận hành.'
              : 'Owner của project cần tạo và xuất bản phòng ban.'
          }
          action={canManage ? <CreateDepartmentDialog projectId={projectId} /> : undefined}
        />
      )}
      {directory.data && directory.data.items.length > 0 && (
        <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {directory.data.items.map((department) => (
            <li key={department.id}>
              <DepartmentCard projectId={projectId} department={department} canManage={canManage} />
            </li>
          ))}
        </ul>
      )}
    </article>
  )
}

function DepartmentCard({ projectId, department, canManage }: { projectId: string; department: DepartmentSummary; canManage: boolean }) {
  const isPublished = department.publishedVersion !== null
  const base = `/projects/${projectId}/office/${department.id}`
  return (
    <section
      style={department.theme ? themeStyle(department.theme) : undefined}
      className="bg-card flex h-full flex-col gap-3 rounded-lg border p-4 shadow-xs"
      aria-labelledby={`department-${department.id}`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 id={`department-${department.id}`} className="flex items-center gap-2 truncate text-sm font-medium">
            <span className="size-2.5 shrink-0 rounded-full bg-[color:var(--office-accent,var(--muted-foreground))]" aria-hidden="true" />
            {department.name}
          </h2>
          <p className="text-muted-foreground truncate font-mono text-xs">{department.slug}</p>
        </div>
        <div className="flex shrink-0 flex-wrap justify-end gap-1">
          {department.isArchived && (
            <Badge variant="outline" className="rounded-md">
              Đã lưu trữ
            </Badge>
          )}
          {isPublished ? (
            <Badge variant="secondary" className="rounded-md">
              v{department.publishedVersion}
            </Badge>
          ) : (
            <Badge variant="outline" className="rounded-md">
              Chưa xuất bản
            </Badge>
          )}
          {department.hasDraft && isPublished && (
            <Badge variant="outline" className="rounded-md">
              Có bản nháp
            </Badge>
          )}
        </div>
      </div>

      <dl className="text-muted-foreground grid grid-cols-3 gap-2 text-xs">
        <div>
          <dt className="sr-only">Repository</dt>
          <dd className="flex items-center gap-1">
            <GitBranch className="size-3.5" aria-hidden="true" /> {formatNumber(department.repositoryCount)} repo
          </dd>
        </div>
        <div>
          <dt className="sr-only">Số phòng</dt>
          <dd className="flex items-center gap-1">
            <LayoutGrid className="size-3.5" aria-hidden="true" /> {formatNumber(department.roomCount)} phòng
          </dd>
        </div>
        <div>
          <dt className="sr-only">Giao diện</dt>
          <dd className="truncate">{department.theme ? THEME_LABELS[department.theme] : '—'}</dd>
        </div>
      </dl>
      <p className="text-muted-foreground text-xs">
        cập nhật <RelativeTime value={department.updatedAt} />
      </p>

      <div className="mt-auto flex flex-wrap gap-2">
        {isPublished && !department.isArchived && (
          <Button asChild size="sm">
            <Link to={base}>
              <DoorOpen /> Mở văn phòng
            </Link>
          </Button>
        )}
        {canManage && !department.isArchived && (
          <Button asChild size="sm" variant="outline">
            <Link to={`${base}/settings`}>
              <Settings2 /> Cấu hình
            </Link>
          </Button>
        )}
      </div>
    </section>
  )
}
