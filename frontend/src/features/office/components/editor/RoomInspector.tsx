import { Trash2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { OptionSelect } from '@/shared/ui/option-select'
import { clampRoom, type GridLimits } from '../../model/layout'
import { ACTION_LABELS, presetLabel } from '../../model/presentation'
import { STATION_LABELS } from '../../model/stations'
import type { DepartmentEditorOptions, DepartmentRoom, RoomAction, WorkflowStation } from '../../types'

const NONE = 'none'

interface RoomInspectorProps {
  room: DepartmentRoom
  rooms: readonly DepartmentRoom[]
  options: DepartmentEditorOptions
  limits: GridLimits
  onChange: (room: DepartmentRoom) => void
  onRemove: (roomId: string) => void
}

/** Properties of the selected room. Station choices already bound to another room are disabled, matching the domain rule. */
export function RoomInspector({ room, rooms, options, limits, onChange, onRemove }: RoomInspectorProps) {
  const takenStations = new Set(rooms.filter((r) => r.roomId !== room.roomId && r.stage).map((r) => r.stage))
  const geometry = (key: 'x' | 'y' | 'width' | 'height', value: string) => {
    const parsed = Number.parseInt(value, 10)
    if (Number.isFinite(parsed)) onChange(clampRoom({ ...room, [key]: parsed }, limits))
  }

  return (
    <FieldGroup className="gap-4">
      <Field>
        <FieldLabel htmlFor="room-label">Tên phòng</FieldLabel>
        <Input
          id="room-label"
          value={room.label}
          maxLength={options.roomLabelMaxLength}
          aria-invalid={!room.label.trim() || undefined}
          onChange={(event) => onChange({ ...room, label: event.target.value })}
        />
      </Field>

      <Field>
        <FieldLabel htmlFor="room-stage">Bước quy trình</FieldLabel>
        <OptionSelect
          id="room-stage"
          value={room.stage ?? NONE}
          onValueChange={(value) => {
            const stage = value === NONE ? null : (value as WorkflowStation)
            // Station rooms show pipeline evidence and carry no navigation action.
            onChange({ ...room, stage, action: stage ? null : room.action })
          }}
          options={[
            { value: NONE, label: 'Phòng thông tin (không gắn bước)' },
            ...options.stages.map((stage) => ({
              value: stage,
              label: takenStations.has(stage) ? `${STATION_LABELS[stage]} · đã gắn phòng khác` : STATION_LABELS[stage],
              disabled: takenStations.has(stage),
            })),
          ]}
        />
      </Field>

      <Field>
        <FieldLabel htmlFor="room-preset">Nội thất</FieldLabel>
        <OptionSelect
          id="room-preset"
          value={room.assetPreset}
          onValueChange={(assetPreset) => onChange({ ...room, assetPreset })}
          options={options.assetPresets.map((preset) => ({ value: preset, label: presetLabel(preset) }))}
        />
      </Field>

      {!room.stage && (
        <Field>
          <FieldLabel htmlFor="room-action">Hành động khi chọn</FieldLabel>
          <OptionSelect
            id="room-action"
            value={room.action ?? NONE}
            onValueChange={(value) => onChange({ ...room, action: value === NONE ? null : (value as RoomAction) })}
            options={[{ value: NONE, label: 'Không có' }, ...options.actions.map((action) => ({ value: action, label: ACTION_LABELS[action] }))]}
          />
          <FieldDescription>Chỉ dẫn tới các trang có sẵn trong ứng dụng.</FieldDescription>
        </Field>
      )}

      <fieldset className="grid grid-cols-4 gap-2">
        <legend className="mb-1.5 text-sm font-medium">Vị trí và kích thước (ô lưới)</legend>
        {(
          [
            ['x', 'Cột'],
            ['y', 'Hàng'],
            ['width', 'Rộng'],
            ['height', 'Cao'],
          ] as const
        ).map(([key, label]) => (
          <label key={key} className="space-y-1 text-xs">
            <span className="text-muted-foreground">{label}</span>
            <Input
              type="number"
              inputMode="numeric"
              min={key === 'width' || key === 'height' ? limits.minRoomSize : 1}
              max={key === 'x' || key === 'width' ? limits.columns : limits.rows}
              value={key === 'x' || key === 'y' ? room[key] + 1 : room[key]}
              onChange={(event) =>
                geometry(key, key === 'x' || key === 'y' ? String(Number.parseInt(event.target.value, 10) - 1) : event.target.value)
              }
              className="h-8 tabular-nums"
            />
          </label>
        ))}
      </fieldset>

      <Button type="button" variant="outline" size="sm" className="text-danger self-start" onClick={() => onRemove(room.roomId)}>
        <Trash2 /> Xóa phòng
      </Button>
    </FieldGroup>
  )
}
