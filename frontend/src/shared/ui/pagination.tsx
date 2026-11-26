import { Button } from '@/components/ui/button'

export function Pagination({
  page,
  totalPages,
  pending,
  onPage,
}: {
  page: number
  totalPages: number
  pending: boolean
  onPage: (page: number) => void
}) {
  return (
    <nav aria-label="Phân trang" className="flex items-center justify-end gap-3 py-4">
      <Button variant="outline" disabled={page <= 1 || pending} onClick={() => onPage(page - 1)}>
        Trước
      </Button>
      <span className="text-sm text-muted-foreground">
        {page} / {Math.max(1, totalPages)}
      </span>
      <Button variant="outline" disabled={page >= totalPages || pending} onClick={() => onPage(page + 1)}>
        Sau
      </Button>
    </nav>
  )
}
