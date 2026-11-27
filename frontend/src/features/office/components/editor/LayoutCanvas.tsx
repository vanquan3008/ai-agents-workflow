import { useId, useRef, type KeyboardEvent, type PointerEvent } from 'react'
import { cn } from '@/lib/utils'
import { moveRoom, overlappingRoomIds, resizeRoom, type GridLimits } from '../../model/layout'
import { themeStyle } from '../../model/presentation'
import { STATION_LABELS } from '../../model/stations'
import type { DepartmentRoom, DepartmentTheme } from '../../types'
import { PresetIcon } from '../floor/RoomVisuals'

interface LayoutCanvasProps {
  rooms: readonly DepartmentRoom[]
  theme: DepartmentTheme
  limits: GridLimits
  selectedRoomId: string | null
  onSelect: (roomId: string) => void
  onChange: (room: DepartmentRoom) => void
}

interface DragState {
  roomId: string
  mode: 'move' | 'resize'
  startX: number
  startY: number
  origin: DepartmentRoom
  last: DepartmentRoom
}

const KEY_DELTAS: Record<string, [number, number]> = {
  ArrowLeft: [-1, 0],
  ArrowRight: [1, 0],
  ArrowUp: [0, -1],
  ArrowDown: [0, 1],
}

/**
 * Editable floor on the same grid as the published view. Rooms move by dragging or with the arrow keys and resize with
 * the corner handle or Shift + arrow keys. Every change goes through the same clamping as the domain limits.
 */
export function LayoutCanvas({ rooms, theme, limits, selectedRoomId, onSelect, onChange }: LayoutCanvasProps) {
  const containerRef = useRef<HTMLElement>(null)
  const dragRef = useRef<DragState | null>(null)
  const instructionsId = useId()
  const overlapping = overlappingRoomIds(rooms)
  const ordered = [...rooms].sort((a, b) => a.sortOrder - b.sortOrder)

  const startDrag = (event: PointerEvent<HTMLElement>, room: DepartmentRoom, mode: DragState['mode']) => {
    if (event.button !== 0) return
    event.stopPropagation()
    onSelect(room.roomId)
    event.currentTarget.setPointerCapture(event.pointerId)
    dragRef.current = { roomId: room.roomId, mode, startX: event.clientX, startY: event.clientY, origin: room, last: room }
  }

  const onPointerMove = (event: PointerEvent<HTMLElement>) => {
    const drag = dragRef.current
    const container = containerRef.current
    if (!drag || !container) return
    const bounds = container.getBoundingClientRect()
    const dx = Math.round((event.clientX - drag.startX) / (bounds.width / limits.columns))
    const dy = Math.round((event.clientY - drag.startY) / (bounds.height / limits.rows))
    const next = drag.mode === 'move' ? moveRoom(drag.origin, dx, dy, limits) : resizeRoom(drag.origin, dx, dy, limits)
    // Only whole-cell changes reach React state, so dragging does not re-render on every pixel.
    if (next.x !== drag.last.x || next.y !== drag.last.y || next.width !== drag.last.width || next.height !== drag.last.height) {
      drag.last = next
      onChange(next)
    }
  }

  const endDrag = (event: PointerEvent<HTMLElement>) => {
    if (dragRef.current && event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId)
    }
    dragRef.current = null
  }

  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>, room: DepartmentRoom) => {
    const delta = KEY_DELTAS[event.key]
    if (!delta) return
    event.preventDefault()
    const [dx, dy] = delta
    onChange(event.shiftKey ? resizeRoom(room, dx, dy, limits) : moveRoom(room, dx, dy, limits))
  }

  return (
    <div className="space-y-2">
      <p id={instructionsId} className="text-muted-foreground text-xs">
        Kéo phòng để di chuyển, kéo góc dưới bên phải để đổi kích thước. Bàn phím: chọn phòng rồi dùng phím mũi tên để di chuyển, Shift + mũi tên để đổi kích thước.
      </p>
      <section
        ref={containerRef}
        aria-label="Trình chỉnh sửa sơ đồ"
        style={{
          ...themeStyle(theme),
          gridTemplateColumns: `repeat(${limits.columns}, minmax(0, 1fr))`,
          gridTemplateRows: `repeat(${limits.rows}, minmax(0, 1fr))`,
          aspectRatio: `${limits.columns} / ${limits.rows}`,
          backgroundSize: `calc(100% / ${limits.columns}) calc(100% / ${limits.rows})`,
        }}
        className={cn(
          'bg-muted/40 relative grid w-full touch-none rounded-lg border select-none',
          '[background-image:linear-gradient(to_right,var(--border)_1px,transparent_1px),linear-gradient(to_bottom,var(--border)_1px,transparent_1px)]',
        )}
      >
        {ordered.map((room) => {
          const selected = room.roomId === selectedRoomId
          const overlaps = overlapping.has(room.roomId)
          return (
            <button
              key={room.roomId}
              type="button"
              aria-pressed={selected}
              aria-describedby={instructionsId}
              aria-label={`${room.label}${room.stage ? `, ${STATION_LABELS[room.stage]}` : ', phòng thông tin'}, cột ${room.x + 1}, hàng ${room.y + 1}, rộng ${room.width}, cao ${room.height}${overlaps ? ', đang chồng lên phòng khác' : ''}`}
              onClick={() => onSelect(room.roomId)}
              onKeyDown={(event) => onKeyDown(event, room)}
              onPointerDown={(event) => startDrag(event, room, 'move')}
              onPointerMove={onPointerMove}
              onPointerUp={endDrag}
              onPointerCancel={endDrag}
              style={{ gridColumn: `${room.x + 1} / span ${room.width}`, gridRow: `${room.y + 1} / span ${room.height}` }}
              className={cn(
                '@container relative m-px flex min-h-0 min-w-0 cursor-grab flex-col items-start overflow-hidden rounded-md border p-1.5 text-left shadow-xs active:cursor-grabbing',
                'focus-visible:ring-ring focus-visible:outline-none focus-visible:ring-[3px]',
                room.stage ? 'bg-card' : 'bg-background/80 border-dashed',
                overlaps && 'border-danger bg-danger/[0.06]',
                selected && 'ring-[color:var(--office-accent)] z-10 ring-2',
              )}
            >
              <span className="flex w-full min-w-0 items-center gap-1">
                <PresetIcon preset={room.assetPreset} className="size-3.5 shrink-0 text-[color:var(--office-accent)]" />
                <span className="truncate text-[11px] font-medium">{room.label}</span>
              </span>
              <span className="text-muted-foreground hidden truncate text-[10px] @min-[6rem]:block">
                {room.stage ? STATION_LABELS[room.stage] : 'Phòng thông tin'}
              </span>
              {selected && (
                <span
                  aria-hidden="true"
                  onPointerDown={(event) => startDrag(event, room, 'resize')}
                  onPointerMove={onPointerMove}
                  onPointerUp={endDrag}
                  onPointerCancel={endDrag}
                  className="absolute right-0.5 bottom-0.5 size-3 cursor-se-resize rounded-sm border-2 border-[color:var(--office-accent)] bg-background"
                />
              )}
            </button>
          )
        })}
      </section>
    </div>
  )
}
