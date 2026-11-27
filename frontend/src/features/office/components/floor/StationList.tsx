import { ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'
import { presetLabel } from '../../model/presentation'
import { STATION_LABELS, type StationState } from '../../model/stations'
import type { DepartmentRoom, WorkflowStation } from '../../types'
import { PresetIcon, StationStateBadge } from './RoomVisuals'

interface StationListProps {
  rooms: readonly DepartmentRoom[]
  /** Station states of the selected task; omitted in the editor, where rooms carry no progress. */
  states?: Partial<Record<WorkflowStation, StationState>>
  selectedRoomId?: string | null
  onSelectRoom: (room: DepartmentRoom) => void
  className?: string
}

/** The floor as a stacked list in sort order: the primary view on phones and an accessible alternative everywhere. */
export function StationList({ rooms, states, selectedRoomId, onSelectRoom, className }: StationListProps) {
  const ordered = [...rooms].sort((a, b) => a.sortOrder - b.sortOrder)
  return (
    <ul className={cn('divide-y rounded-lg border', className)} aria-label="Danh sách phòng">
      {ordered.map((room) => {
        const state = room.stage && states ? (states[room.stage] ?? 'idle') : null
        return (
          <li key={room.roomId}>
            <button
              type="button"
              onClick={() => onSelectRoom(room)}
              aria-current={room.roomId === selectedRoomId || undefined}
              className={cn(
                'hover:bg-accent/50 focus-visible:ring-ring flex w-full items-center gap-3 px-3 py-2.5 text-left focus-visible:ring-[3px] focus-visible:outline-none',
                room.roomId === selectedRoomId && 'bg-accent/60',
              )}
            >
              <PresetIcon preset={room.assetPreset} className="text-muted-foreground size-4 shrink-0" />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-medium">{room.label}</span>
                <span className="text-muted-foreground block truncate text-xs">
                  {room.stage ? STATION_LABELS[room.stage] : presetLabel(room.assetPreset)}
                </span>
              </span>
              {state && <StationStateBadge state={state} />}
              <ChevronRight className="text-muted-foreground size-4 shrink-0" aria-hidden="true" />
            </button>
          </li>
        )
      })}
    </ul>
  )
}
