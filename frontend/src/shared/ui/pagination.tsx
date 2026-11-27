import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { formatNumber } from '@/shared/lib/format'

interface PaginationProps {
  page: number
  totalPages: number
  totalCount?: number
  pending: boolean
  onPage: (page: number) => void
}

export function Pagination({ page, totalPages, totalCount, pending, onPage }: PaginationProps) {
  const lastPage = Math.max(1, totalPages)
  return (
    <nav aria-label="Phân trang" className="text-muted-foreground flex items-center justify-between gap-3 text-xs">
      <span>{totalCount !== undefined ? `${formatNumber(totalCount)} mục` : null}</span>
      <div className="flex items-center gap-1.5">
        <span className="tabular-nums">
          Trang {page} / {lastPage}
        </span>
        <Button variant="outline" size="icon-sm" disabled={page <= 1 || pending} onClick={() => onPage(page - 1)} aria-label="Trang trước">
          <ChevronLeft />
        </Button>
        <Button variant="outline" size="icon-sm" disabled={page >= lastPage || pending} onClick={() => onPage(page + 1)} aria-label="Trang sau">
          <ChevronRight />
        </Button>
      </div>
    </nav>
  )
}
