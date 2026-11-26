import type { ReactNode } from 'react'
import { AlertCircle, Inbox, RotateCw } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError } from '@/shared/api/http'

export function PageHeader({ title, description, actions, eyebrow }: { title: ReactNode; description?: ReactNode; actions?: ReactNode; eyebrow?: ReactNode }) {
  return (
    <div className="flex flex-col gap-3 pb-6 sm:flex-row sm:items-end sm:justify-between">
      <div className="min-w-0 space-y-1">
        {eyebrow && <div className="text-muted-foreground text-xs font-medium tracking-wide uppercase">{eyebrow}</div>}
        <h1 className="font-heading text-2xl font-semibold tracking-tight">{title}</h1>
        {description && <p className="text-muted-foreground max-w-3xl text-sm">{description}</p>}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
    </div>
  )
}

/** Khung tải dạng skeleton (giữ bố cục ổn định thay vì nhảy khi dữ liệu về). */
export function LoadingBlock({ rows = 4, label = 'Đang tải…' }: { rows?: number; label?: string }) {
  return (
    <output className="block space-y-3" aria-live="polite" aria-label={label}>
      {Array.from({ length: rows }, (_, i) => (
        <Skeleton key={i} className="h-10 w-full" />
      ))}
    </output>
  )
}

export function ErrorBlock({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const message =
    error instanceof ApiError
      ? error.isForbidden
        ? 'Bạn không có quyền xem nội dung này.'
        : error.isNotFound
          ? 'Không tìm thấy dữ liệu.'
          : error.message
      : 'Không kết nối được tới máy chủ.'

  return (
    <Alert variant="destructive">
      <AlertCircle />
      <AlertTitle>{message}</AlertTitle>
      <AlertDescription className="flex flex-wrap items-center gap-3">
        {error instanceof ApiError && error.traceId && <span className="font-mono text-xs">Mã truy vết: {error.traceId}</span>}
        {onRetry && (
          <Button size="sm" variant="outline" onClick={onRetry}>
            <RotateCw /> Thử lại
          </Button>
        )}
      </AlertDescription>
    </Alert>
  )
}

export function EmptyBlock({ title, description, icon, action }: { title: string; description?: string; icon?: ReactNode; action?: ReactNode }) {
  return (
    <Empty className="border border-dashed">
      <EmptyHeader>
        <EmptyMedia variant="icon">{icon ?? <Inbox />}</EmptyMedia>
        <EmptyTitle>{title}</EmptyTitle>
        {description && <EmptyDescription>{description}</EmptyDescription>}
      </EmptyHeader>
      {action && <EmptyContent>{action}</EmptyContent>}
    </Empty>
  )
}

/** Thông báo lỗi chung của form (lỗi không gắn với trường cụ thể). */
export function FormError({ error }: { error: unknown }) {
  if (!error) return null
  const message = error instanceof ApiError ? error.message : 'Không gửi được yêu cầu. Thử lại.'
  return (
    <Alert variant="destructive">
      <AlertCircle />
      <AlertTitle>{message}</AlertTitle>
    </Alert>
  )
}
