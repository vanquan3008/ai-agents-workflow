import { formatDateTimeLong, formatRelative } from '@/shared/lib/format'

/** Relative time ("3 phút trước") with the exact timestamp available on hover and to assistive tech. */
export function RelativeTime({ value, className }: { value: string | null | undefined; className?: string }) {
  if (!value) return <span className={className}>—</span>
  return (
    <time dateTime={value} title={formatDateTimeLong(value)} className={className}>
      {formatRelative(value)}
    </time>
  )
}
