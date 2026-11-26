import type { ReactNode } from 'react'
import { ApiError } from '../api/http'

export function Loading({ label = 'Đang tải…' }: { label?: string }) {
  return (
    <div className="state" role="status" aria-live="polite">
      <span className="spinner" aria-hidden="true" />
      {label}
    </div>
  )
}

export function Empty({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="state state--empty">
      <p className="state__title">{title}</p>
      {children}
    </div>
  )
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const message =
    error instanceof ApiError
      ? error.isForbidden
        ? 'Bạn không có quyền xem nội dung này.'
        : error.isNotFound
          ? 'Không tìm thấy dữ liệu.'
          : error.message
      : 'Không kết nối được tới máy chủ.'

  return (
    <div className="state state--error" role="alert">
      <p className="state__title">{message}</p>
      {error instanceof ApiError && error.traceId && <p className="muted">Mã truy vết: {error.traceId}</p>}
      {onRetry && (
        <button type="button" className="button" onClick={onRetry}>
          Thử lại
        </button>
      )}
    </div>
  )
}
