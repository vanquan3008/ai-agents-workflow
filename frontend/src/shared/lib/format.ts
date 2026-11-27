const dateTime = new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' })
const dateTimeLong = new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'medium' })
const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 4 })
const integer = new Intl.NumberFormat('vi-VN')
const compact = new Intl.NumberFormat('vi-VN', { notation: 'compact', maximumFractionDigits: 1 })
const relative = new Intl.RelativeTimeFormat('vi-VN', { numeric: 'auto' })

/** Timestamps are stored in UTC and rendered in the viewer's time zone. */
export const formatDateTime = (iso: string | null | undefined) => (iso ? dateTime.format(new Date(iso)) : '—')

export const formatDateTimeLong = (iso: string | null | undefined) => (iso ? dateTimeLong.format(new Date(iso)) : '—')

export const formatUsd = (value: number) => usd.format(value)

export const formatNumber = (value: number) => integer.format(value)

export const formatCompactNumber = (value: number) => compact.format(value)

export const shortSha = (sha: string | null | undefined) => (sha ? sha.slice(0, 7) : '—')

export function formatRelative(iso: string | null | undefined): string {
  if (!iso) return '—'
  const seconds = Math.round((new Date(iso).getTime() - Date.now()) / 1000)
  const abs = Math.abs(seconds)
  if (abs < 60) return relative.format(seconds, 'second')
  if (abs < 3600) return relative.format(Math.round(seconds / 60), 'minute')
  if (abs < 86_400) return relative.format(Math.round(seconds / 3600), 'hour')
  return relative.format(Math.round(seconds / 86_400), 'day')
}

export function formatDuration(totalSeconds: number | null | undefined): string {
  if (totalSeconds == null) return '—'
  if (totalSeconds < 1) return '<1 giây'
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = Math.round(totalSeconds % 60)
  if (minutes === 0) return `${seconds} giây`
  const hours = Math.floor(minutes / 60)
  return hours === 0 ? `${minutes} phút ${seconds} giây` : `${hours} giờ ${minutes % 60} phút`
}

export const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(-2)
    .map((word) => word[0]?.toUpperCase() ?? '')
    .join('')

/** Pretty-prints JSON when possible; returns the raw text otherwise. */
export function prettyJson(raw: string | null | undefined): string {
  if (!raw) return ''
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
}
