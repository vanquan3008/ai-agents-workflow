import type { ReactNode } from 'react'

/** Label and help text on the left, control on the right; stacks on narrow screens. */
export function SettingRow({ label, description, htmlFor, children }: { label: string; description?: ReactNode; htmlFor?: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-2 py-3 first:pt-0 last:pb-0 sm:flex-row sm:items-center sm:justify-between sm:gap-6">
      <div className="min-w-0 space-y-0.5">
        <label htmlFor={htmlFor} className="text-sm font-medium">
          {label}
        </label>
        {description && <p className="text-muted-foreground text-xs text-pretty">{description}</p>}
      </div>
      <div className="w-full shrink-0 sm:w-56">{children}</div>
    </div>
  )
}
