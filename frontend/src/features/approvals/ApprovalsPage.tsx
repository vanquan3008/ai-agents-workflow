import { useState } from 'react'
import { ShieldCheck } from 'lucide-react'
import type { ApprovalStatus } from '@/shared/api/types'
import { statusLabel } from '@/shared/lib/labels'
import { OptionSelect } from '@/shared/ui/option-select'
import { PageHeader } from '@/shared/ui/page-header'
import { Pagination } from '@/shared/ui/pagination'
import { Panel } from '@/shared/ui/panel'
import { EmptyBlock, ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { useApprovals } from './api'
import { ApprovalTable } from './components/ApprovalTable'

const ALL = 'all'
const APPROVAL_STATUSES: readonly ApprovalStatus[] = ['PENDING', 'APPROVED', 'REJECTED', 'EXPIRED', 'SUPERSEDED']

export default function ApprovalsPage() {
  const [status, setStatus] = useState<ApprovalStatus | ''>('PENDING')
  const [page, setPage] = useState(1)
  const query = useApprovals(status, page)

  return (
    <div className="space-y-4">
      <PageHeader
        title="Phê duyệt"
        description="Thay đổi chạm vùng nhạy cảm hoặc nằm ngoài phạm vi tự động cần người có quyền quyết định."
        className="pb-2"
      />

      <OptionSelect
        aria-label="Lọc theo trạng thái"
        className="sm:w-56"
        value={status || ALL}
        onValueChange={(value) => {
          setStatus(value === ALL ? '' : (value as ApprovalStatus))
          setPage(1)
        }}
        options={[{ value: ALL, label: 'Mọi trạng thái' }, ...APPROVAL_STATUSES.map((value) => ({ value, label: statusLabel(value) }))]}
      />

      <Panel flush>
        {query.isPending && <LoadingBlock rows={5} className="p-4" />}
        {query.isError && <ErrorBlock error={query.error} onRetry={() => void query.refetch()} className="m-4 w-auto" />}
        {query.data?.items.length === 0 && (
          <EmptyBlock
            className="m-4 w-auto"
            icon={<ShieldCheck />}
            title={status === 'PENDING' ? 'Không có gì chờ duyệt' : 'Không có yêu cầu duyệt'}
            description={status === 'PENDING' ? 'Khi agent cần quyết định của bạn, yêu cầu sẽ xuất hiện ở đây.' : undefined}
          />
        )}
        {query.data && query.data.items.length > 0 && (
          <div className="overflow-x-auto">
            <ApprovalTable approvals={query.data.items} />
          </div>
        )}
      </Panel>

      {query.data && query.data.totalPages > 1 && (
        <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} pending={query.isFetching} onPage={setPage} />
      )}
    </div>
  )
}
