import { MapPin } from 'lucide-react'
import { cn } from '@/lib/utils'
import { DEFAULT_LIMITS } from '../../model/layout'
import { presetLabel, ROOM_STATE_CLASSES, themeStyle } from '../../model/presentation'
import { STATE_LABELS, STATION_LABELS, type StationState } from '../../model/stations'
import type { DepartmentRoom, DepartmentTheme, WorkflowStation } from '../../types'
import { PresetIcon, StationStateBadge } from './RoomVisuals'

interface FloorPlanProps {
  rooms: readonly DepartmentRoom[]
  theme: DepartmentTheme
  /** Accessible name of the floor, e.g. the department name. */
  label: string
  columns?: number
  rows?: number
  states?: Partial<Record<WorkflowStation, StationState>>
  selectedRoomId?: string | null
  /** Station holding the selected task's marker. */
  markerStation?: WorkflowStation | null
  markerLabel?: string
  onSelectRoom?: (room: DepartmentRoom) => void
  className?: string
}
/**
 * Read-only 2D floor rendered from the published layout on the shared 24 × 16 grid. Rooms are buttons in sort order,
 * so keyboard and screen-reader users traverse the floor in the same order as the station list.
 */
export function FloorPlan({
  rooms,
  theme,
  label,
  columns = DEFAULT_LIMITS.columns,
  rows = DEFAULT_LIMITS.rows,
  states = {},
  selectedRoomId,
  markerStation,
  markerLabel = 'Task đang chọn',
  onSelectRoom,
  className,
}: FloorPlanProps) {
  const ordered = [...rooms].sort((a, b) => a.sortOrder - b.sortOrder)

  return (
    <section
      aria-label={`Sơ đồ ${label}`}
      style={{
        ...themeStyle(theme),
        gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))`,
        gridTemplateRows: `repeat(${rows}, minmax(0, 1fr))`,
        aspectRatio: `${columns} / ${rows}`,
        backgroundSize: `calc(100% / ${columns}) calc(100% / ${rows})`,
      }}
      className={cn(
        'bg-muted/40 relative grid w-full gap-1 rounded-lg border p-1.5 sm:gap-1.5 sm:p-2',
        // Faint floor tiles tinted by the department accent.
        '[background-image:radial-gradient(color-mix(in_oklch,var(--office-accent)_22%,transparent)_1px,transparent_1px)]',
        className,
      )}
    >
      {ordered.map((room) => {
        const state = room.stage ? (states[room.stage] ?? 'idle') : null
        const selected = room.roomId === selectedRoomId
        const hasMarker = room.stage !== null && room.stage === markerStation
        return (
          <button
            key={room.roomId}
            type="button"
            onClick={() => onSelectRoom?.(room)}
            aria-pressed={onSelectRoom ? selected : undefined}
            aria-label={[room.label, room.stage ? STATION_LABELS[room.stage] : 'Phòng thông tin', state ? `trạng thái: ${STATE_LABELS[state].toLowerCase()}` : null, hasMarker ? markerLabel : null]
              .filter(Boolean)
              .join(', ')}
            style={{ gridColumn: `${room.x + 1} / span ${room.width}`, gridRow: `${room.y + 1} / span ${room.height}` }}
            className={cn(
              '@container relative flex min-h-0 min-w-0 flex-col items-start justify-between overflow-hidden rounded-md border p-1.5 text-left shadow-xs transition-[box-shadow,background-color] sm:p-2',
              'focus-visible:ring-ring focus-visible:outline-none focus-visible:ring-[3px]',
              state ? ROOM_STATE_CLASSES[state] : 'bg-background/70 border-dashed',
              selected && 'ring-[color:var(--office-accent)] ring-2 ring-offset-1 ring-offset-background',
              onSelectRoom ? 'hover:bg-accent/60 cursor-pointer' : 'cursor-default',
            )}
          >
            <span className="flex w-full min-w-0 items-center gap-1.5">
              <PresetIcon preset={room.assetPreset} className="size-3.5 shrink-0 text-[color:var(--office-accent)] sm:size-4" />
              <span className="truncate text-[11px] leading-tight font-medium sm:text-xs">{room.label}</span>
            </span>
            <span className="hidden w-full min-w-0 flex-col items-start gap-1 @min-[6rem]:flex">
              <span className="text-muted-foreground truncate text-[11px]">{room.stage ? STATION_LABELS[room.stage] : presetLabel(room.assetPreset)}</span>
              {state && state !== 'idle' && <StationStateBadge state={state} />}
            </span>
            {hasMarker && (
              // Keyed by station so the marker animates once when a persisted transition moves it.
              <span
                key={markerStation}
                className="absolute right-1 bottom-1 inline-flex items-center gap-0.5 rounded-full bg-[color:var(--office-accent)] px-1.5 py-0.5 text-[10px] font-medium text-white shadow-sm motion-safe:animate-in motion-safe:fade-in motion-safe:zoom-in-75"
              >
                <MapPin className="size-3" aria-hidden="true" />
                <span className="hidden @min-[6rem]:inline">Task</span>
              </span>
            )}
          </button>
        )
      })}
    </section>
  )
}
