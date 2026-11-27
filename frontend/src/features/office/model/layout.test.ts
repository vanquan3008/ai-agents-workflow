import { describe, expect, it } from 'vitest'
import type { DepartmentConfig, DepartmentRoom, WorkflowStation } from '../types'
import { WORKFLOW_STATIONS } from '../types'
import { DEFAULT_LIMITS, clampRoom, findFreeSpot, layoutIssues, moveRoom, overlappingRoomIds, resizeRoom, slugify } from './layout'

const room = (id: string, x: number, y: number, width = 4, height = 4, stage: WorkflowStation | null = null): DepartmentRoom => ({
  roomId: id,
  label: id,
  stage,
  x,
  y,
  width,
  height,
  sortOrder: 0,
  assetPreset: 'lounge',
  action: null,
})

describe('room geometry', () => {
  it('keeps moved rooms inside the grid', () => {
    expect(moveRoom(room('a', 22, 0), 5, -3, DEFAULT_LIMITS)).toMatchObject({ x: 20, y: 0 })
  })

  it('limits resizing to the grid edge and the minimum size', () => {
    expect(resizeRoom(room('a', 20, 12), 10, 10, DEFAULT_LIMITS)).toMatchObject({ width: 4, height: 4 })
    expect(resizeRoom(room('a', 0, 0), -10, -10, DEFAULT_LIMITS)).toMatchObject({ width: 2, height: 2 })
  })

  it('clamps an oversized room back into bounds', () => {
    expect(clampRoom(room('a', 30, 30, 40, 1), DEFAULT_LIMITS)).toMatchObject({ x: 0, y: 14, width: 24, height: 2 })
  })

  it('detects overlaps but not rooms that only touch', () => {
    const rooms = [room('a', 0, 0), room('b', 4, 0), room('c', 2, 2)]

    expect([...overlappingRoomIds(rooms)].sort()).toEqual(['a', 'b', 'c'])
    expect(overlappingRoomIds([room('a', 0, 0), room('b', 4, 0)]).size).toBe(0)
  })

  it('finds the first free spot in row-major order', () => {
    expect(findFreeSpot([room('a', 0, 0, 24, 4)], 4, 4, DEFAULT_LIMITS)).toEqual({ x: 0, y: 4 })
    expect(findFreeSpot([room('a', 0, 0, 24, 16)], 2, 2, DEFAULT_LIMITS)).toBeNull()
  })
})

describe('layoutIssues', () => {
  const complete: DepartmentConfig = {
    name: 'Phát triển',
    theme: 'INDIGO',
    repositoryIds: ['r1'],
    rooms: WORKFLOW_STATIONS.map((stage, index) => room(stage, index * 4, 0, 4, 4, stage)),
  }

  it('accepts a complete layout', () => {
    expect(layoutIssues(complete, DEFAULT_LIMITS)).toEqual([])
  })

  it('reports missing stations, repositories and overlaps', () => {
    const broken: DepartmentConfig = {
      ...complete,
      repositoryIds: [],
      rooms: [...complete.rooms.filter((r) => r.stage !== 'PUBLISHING'), room('x', 1, 1)],
    }

    const issues = layoutIssues(broken, DEFAULT_LIMITS)
    expect(issues).toContain('Chọn ít nhất một repository trước khi xuất bản.')
    expect(issues).toContain('Chưa có phòng cho bước PUBLISHING.')
    expect(issues).toContain('Có phòng đang chồng lên nhau.')
  })
})

describe('slugify', () => {
  it('strips Vietnamese diacritics and separators', () => {
    expect(slugify('Nền tảng & vận hành')).toBe('nen-tang-van-hanh')
    expect(slugify('  Đội Kiểm thử 2  ')).toBe('doi-kiem-thu-2')
  })

  it('never ends with a dash after truncation', () => {
    expect(slugify(`${'a'.repeat(63)} b`)).toBe('a'.repeat(63))
  })
})
