import { Bell, Lock } from 'lucide-react'
import { Checkbox } from '@/components/ui/checkbox'
import type { NotificationEvent, WorkflowDefinition, WorkflowLimits } from '@/shared/api/types'
import { notificationLabel } from '@/shared/lib/labels'
import { Panel } from '@/shared/ui/panel'

interface NotificationsSectionProps {
  definition: WorkflowDefinition
  limits: WorkflowLimits
  onChange: (patch: Partial<WorkflowDefinition>) => void
}

export function NotificationsSection({ definition, limits, onChange }: NotificationsSectionProps) {
  function toggle(event: NotificationEvent, enabled: boolean) {
    onChange({
      notifyOn: enabled ? [...new Set([...definition.notifyOn, event])] : definition.notifyOn.filter((value) => value !== event),
    })
  }

  return (
    <Panel title="Thông báo Discord" description="Sự kiện có khóa luôn được gửi để người duyệt và người giao việc không bỏ lỡ." icon={<Bell />}>
      <div className="grid gap-2 sm:grid-cols-2">
        {limits.notificationOptions.map((event) => {
          const mandatory = limits.mandatoryNotifications.includes(event)
          const id = `notify-${event}`
          return (
            <label
              key={event}
              htmlFor={id}
              className="hover:bg-muted/50 has-[[data-state=checked]]:border-primary/30 has-[[data-state=checked]]:bg-primary/[0.04] flex cursor-pointer items-center gap-2.5 rounded-md border px-3 py-2 text-sm transition-colors has-[:disabled]:cursor-default"
            >
              <Checkbox
                id={id}
                checked={definition.notifyOn.includes(event) || mandatory}
                disabled={mandatory}
                onCheckedChange={(checked) => toggle(event, checked === true)}
              />
              <span className="flex-1">{notificationLabel(event)}</span>
              {mandatory && <Lock className="text-muted-foreground size-3.5" aria-label="Bắt buộc" />}
            </label>
          )
        })}
      </div>
    </Panel>
  )
}
