import { useEffect, useState } from 'react'
import { Search, Users } from 'lucide-react'
import { Input } from '@/components/ui/input'
import type { AdminUser } from '@/shared/api/types'
import { PageHeader } from '@/shared/ui/page-header'
import { Pagination } from '@/shared/ui/pagination'
import { Panel } from '@/shared/ui/panel'
import { EmptyBlock, ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { useUsers } from './api'
import { CreateUserDialog } from './components/CreateUserDialog'
import { EditUserSheet } from './components/EditUserSheet'
import { UserTable } from './components/UserTable'

const SEARCH_DEBOUNCE_MS = 300

export default function UsersPage() {
  const [searchDraft, setSearchDraft] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [editingUserId, setEditingUserId] = useState<string | null>(null)
  const query = useUsers(search, page)
  const editingUser: AdminUser | null = query.data?.items.find((user) => user.id === editingUserId) ?? null

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch(searchDraft.trim())
      setPage(1)
    }, SEARCH_DEBOUNCE_MS)
    return () => window.clearTimeout(timer)
  }, [searchDraft])

  return (
    <div className="space-y-4">
      <PageHeader title="Người dùng" description="Tài khoản DevAgent và quyền cấp hệ thống." actions={<CreateUserDialog />} className="pb-2" />

      <div className="relative sm:w-80">
        <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2" aria-hidden="true" />
        <Input
          type="search"
          value={searchDraft}
          onChange={(event) => setSearchDraft(event.target.value)}
          placeholder="Tìm theo tên hoặc email…"
          aria-label="Tìm người dùng"
          className="pl-8"
        />
      </div>

      <Panel flush>
        {query.isPending && <LoadingBlock rows={5} className="p-4" />}
        {query.isError && <ErrorBlock error={query.error} onRetry={() => void query.refetch()} className="m-4 w-auto" />}
        {query.data?.items.length === 0 && <EmptyBlock className="m-4 w-auto" icon={<Users />} title="Không tìm thấy người dùng" />}
        {query.data && query.data.items.length > 0 && (
          <div className="overflow-x-auto">
            <UserTable users={query.data.items} onEdit={(user) => setEditingUserId(user.id)} />
          </div>
        )}
      </Panel>

      {query.data && query.data.totalPages > 1 && (
        <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} pending={query.isFetching} onPage={setPage} />
      )}

      <EditUserSheet user={editingUser} onOpenChange={(open) => !open && setEditingUserId(null)} />
    </div>
  )
}
