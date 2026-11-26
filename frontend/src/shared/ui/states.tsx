import type { ReactNode } from 'react'
import { AlertCircle, Inbox, RotateCw } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { ApiError } from '@/shared/api/http'

/** Skeleton placeholder that keeps the layout stable while data loads. */
export function LoadingBlock({ rows = 4, label = 'Đang tải…', className }: { rows?: number; label?: string; className?: string }) {
  return (
    <output className={cn('block space-y-2', className)} aria-live="polite" aria-label={label}>
      {Array.from({ length: rows }, (_, index) => (
        <Skeleton key={index} className="h-9 w-full" />
      ))}
    </output>
  )
}

function describeError(error: unknown): string {
  if (!(error instanceof ApiError)) return 'Không kết nối được tới máy chủ.'
  if (error.isForbidden) return 'Bạn không có quyền xem nội dung này.'
  if (error.isNotFound) return 'Không tìm thấy dữ liệu.'
  return error.message
}

export function ErrorBlock({ error, onRetry, className }: { error: unknown; onRetry?: () => void; className?: string }) {
  return (
    <Alert variant="destructive" className={cn('border-destructive/30 bg-destructive/5', className)}>
      <AlertCircle />
      <AlertTitle>{describeError(error)}</AlertTitle>
      {(onRetry || (error instanceof ApiError && error.traceId)) && (
        <AlertDescription className="mt-1 flex flex-wrap items-center gap-3">
          {error instanceof ApiError && error.traceId && <span className="font-mono text-xs">Mã truy vết: {error.traceId}</span>}
          {onRetry && (
            <Button size="xs" variant="outline" onClick={onRetry}>
              <RotateCw /> Thử lại
            </Button>
          )}
        </AlertDescription>
      )}
    </Alert>
  )
}

interface EmptyBlockProps {
  title: string
  description?: ReactNode
  icon?: ReactNode
  action?: ReactNode
  className?: string
}

export function EmptyBlock({ title, description, icon, action, className }: EmptyBlockProps) {
  return (
    <Empty className={cn('border border-dashed py-10', className)}>
      <EmptyHeader>
        <EmptyMedia variant="icon">{icon ?? <Inbox />}</EmptyMedia>
        <EmptyTitle>{title}</EmptyTitle>
        {description && <EmptyDescription>{description}</EmptyDescription>}
      </EmptyHeader>
      {action && <EmptyContent>{action}</EmptyContent>}
    </Empty>
  )
}

/** Form-level error that is not attached to a specific field. */
export function FormError({ error }: { error: unknown }) {
  if (!error) return null
  const message = error instanceof ApiError ? error.message : 'Không gửi được yêu cầu. Thử lại.'
  return (
    <Alert variant="destructive" className="border-destructive/30 bg-destructive/5">
      <AlertCircle />
      <AlertTitle>{message}</AlertTitle>
    </Alert>
  )
}
