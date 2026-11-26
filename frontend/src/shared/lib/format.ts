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
