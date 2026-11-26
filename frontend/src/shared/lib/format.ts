const dateTime = new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'medium' })
const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 4 })
const integer = new Intl.NumberFormat('vi-VN')

/** Thời gian lưu trữ ở UTC; hiển thị theo múi giờ của người xem. */
export const formatDateTime = (iso: string | null | undefined) => (iso ? dateTime.format(new Date(iso)) : '—')

export const formatUsd = (value: number) => usd.format(value)

export const formatNumber = (value: number) => integer.format(value)

export const shortSha = (sha: string | null | undefined) => (sha ? sha.slice(0, 7) : '—')

const STATUS_LABELS: Record<string, string> = {
  PENDING: 'Chờ xử lý',
  PLANNING: 'Đang lập kế hoạch',
  NEEDS_CLARIFICATION: 'Cần bổ sung thông tin',
  WAITING_FOR_APPROVAL: 'Chờ duyệt',
  IN_PROGRESS: 'Đang sửa',
  TESTING: 'Đang chạy test',
  REVIEWING: 'Đang review',
  PUBLISHING: 'Đang mở PR',
  COMPLETED: 'Hoàn tất',
  REJECTED: 'Bị từ chối',
  FAILED: 'Thất bại',
  BLOCKED: 'Bị chặn',
  CANCELLED: 'Đã hủy',
  TIMED_OUT: 'Hết giờ',
  APPROVED: 'Đã duyệt',
  EXPIRED: 'Hết hạn',
  SUPERSEDED: 'Không còn hiệu lực',
  RUNNING: 'Đang chạy',
  SUCCEEDED: 'Thành công',
  DENIED: 'Bị chặn',
  BUDGET_EXCEEDED: 'Chạm trần ngân sách',
}

export const statusLabel = (status: string) => STATUS_LABELS[status] ?? status

const relative = new Intl.RelativeTimeFormat('vi-VN', { numeric: 'auto' })

/** "3 phút trước", "hôm qua"… cho cột cập nhật. */
export function formatRelative(iso: string | null | undefined): string {
  if (!iso) return '—'
  const seconds = Math.round((new Date(iso).getTime() - Date.now()) / 1000)
  const abs = Math.abs(seconds)
  if (abs < 60) return relative.format(seconds, 'second')
  if (abs < 3600) return relative.format(Math.round(seconds / 60), 'minute')
  if (abs < 86_400) return relative.format(Math.round(seconds / 3600), 'hour')
  return relative.format(Math.round(seconds / 86_400), 'day')
}

/** Chữ cái đầu cho avatar. */
export const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(-2)
    .map((w) => w[0]?.toUpperCase() ?? '')
    .join('')
