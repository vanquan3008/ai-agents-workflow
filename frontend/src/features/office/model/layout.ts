import { WORKFLOW_STATIONS, type DepartmentConfig, type DepartmentRoom } from '../types'

/** Mirrors the backend OfficeGrid limits; the editor loads the authoritative values from /departments/options. */
export interface GridLimits {
  columns: number
  rows: number
  minRoomSize: number
  minRooms: number
  maxRooms: number
}

export const DEFAULT_LIMITS: GridLimits = { columns: 24, rows: 16, minRoomSize: 2, minRooms: 6, maxRooms: 12 }

const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), max)

/** Keeps a room inside the grid with at least the minimum size, preserving its position where possible. */
export function clampRoom(room: DepartmentRoom, limits: GridLimits): DepartmentRoom {
  const width = clamp(room.width, limits.minRoomSize, limits.columns)
  const height = clamp(room.height, limits.minRoomSize, limits.rows)
  return {
    ...room,
    width,
    height,
    x: clamp(room.x, 0, limits.columns - width),
    y: clamp(room.y, 0, limits.rows - height),
  }
}

export const moveRoom = (room: DepartmentRoom, dx: number, dy: number, limits: GridLimits) =>
  clampRoom({ ...room, x: room.x + dx, y: room.y + dy }, limits)

export function resizeRoom(room: DepartmentRoom, dw: number, dh: number, limits: GridLimits): DepartmentRoom {
  const width = clamp(room.width + dw, limits.minRoomSize, limits.columns - room.x)
  const height = clamp(room.height + dh, limits.minRoomSize, limits.rows - room.y)
  return { ...room, width, height }
}

export const roomsOverlap = (a: DepartmentRoom, b: DepartmentRoom) =>
  a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height

export function overlappingRoomIds(rooms: readonly DepartmentRoom[]): Set<string> {
  const ids = new Set<string>()
  rooms.forEach((a, index) => {
    for (const b of rooms.slice(index + 1)) {
      if (roomsOverlap(a, b)) {
        ids.add(a.roomId)
        ids.add(b.roomId)
      }
    }
  })
  return ids
}

/** First free top-left position (row-major) where a room of this size fits without overlapping. */
export function findFreeSpot(rooms: readonly DepartmentRoom[], width: number, height: number, limits: GridLimits): { x: number; y: number } | null {
  for (let y = 0; y + height <= limits.rows; y++) {
    for (let x = 0; x + width <= limits.columns; x++) {
      const candidate = { x, y, width, height } as DepartmentRoom
      if (!rooms.some((room) => roomsOverlap(room, candidate))) return { x, y }
    }
  }
  return null
}

export const nextSortOrder = (rooms: readonly DepartmentRoom[]) => rooms.reduce((max, room) => Math.max(max, room.sortOrder), -1) + 1

/**
 * Live publish checks for the editor. The server re-validates every rule, so this only needs to be helpful,
 * not exhaustive; messages match the backend wording.
 */
export function layoutIssues(config: DepartmentConfig, limits: GridLimits): string[] {
  const issues: string[] = []
  if (!config.name.trim()) issues.push('Tên phòng ban là bắt buộc.')
  if (config.repositoryIds.length === 0) issues.push('Chọn ít nhất một repository trước khi xuất bản.')
  if (config.rooms.length < limits.minRooms) issues.push(`Phòng ban cần ít nhất ${limits.minRooms} phòng.`)
  if (config.rooms.length > limits.maxRooms) issues.push(`Một phòng ban có tối đa ${limits.maxRooms} phòng.`)
  for (const station of WORKFLOW_STATIONS) {
    const count = config.rooms.filter((room) => room.stage === station).length
    if (count === 0) issues.push(`Chưa có phòng cho bước ${station}.`)
    if (count > 1) issues.push(`Bước ${station} đang gắn với nhiều phòng.`)
  }
  if (config.rooms.some((room) => !room.label.trim())) issues.push('Mỗi phòng cần có tên.')
  if (overlappingRoomIds(config.rooms).size > 0) issues.push('Có phòng đang chồng lên nhau.')
  return issues
}

/** Slug suggestion from a Vietnamese name: strips diacritics (including đ), keeps [a-z0-9-], at most 64 characters. */
export function slugify(text: string): string {
  return text
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/đ/gi, 'd')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 64)
    .replace(/-+$/g, '')
}
