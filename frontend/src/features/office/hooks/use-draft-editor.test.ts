import { act, renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { DepartmentConfig, DepartmentDraft } from '../types'
import { sameConfig, useDraftEditor } from './use-draft-editor'

const config: DepartmentConfig = {
  name: 'Nền tảng',
  theme: 'INDIGO',
  repositoryIds: ['r1', 'r2'],
  rooms: [
    { roomId: 'a', label: 'Lập kế hoạch', stage: 'PLANNING', x: 0, y: 0, width: 8, height: 5, sortOrder: 0, assetPreset: 'planning-table', action: null },
    { roomId: 'b', label: 'Runbook', stage: null, x: 16, y: 10, width: 8, height: 6, sortOrder: 1, assetPreset: 'bookshelf', action: 'OPEN_WORKFLOW' },
  ],
}

const draft = (editVersion: number, value: DepartmentConfig = config): DepartmentDraft => ({
  id: 'd1',
  projectId: 'p1',
  slug: 'nen-tang',
  editVersion,
  publishedVersion: null,
  hasDraft: true,
  config: value,
  publishIssues: [],
  availableRepositories: [],
  updatedAt: '2026-11-27T08:00:00Z',
})

describe('sameConfig', () => {
  it('ignores server normalization: trimming, room order and repository order', () => {
    const normalized: DepartmentConfig = {
      ...config,
      name: ' Nền tảng ',
      repositoryIds: ['r2', 'r1'],
      rooms: [{ ...config.rooms[1]!, label: 'Runbook ' }, config.rooms[0]!],
    }

    expect(sameConfig(config, normalized)).toBe(true)
  })

  it('detects real edits', () => {
    expect(sameConfig(config, { ...config, theme: 'SLATE' })).toBe(false)
    expect(sameConfig(config, { ...config, rooms: config.rooms.map((r) => (r.roomId === 'a' ? { ...r, x: 1 } : r)) })).toBe(false)
  })
})

describe('useDraftEditor across two tabs', () => {
  it('an unedited tab adopts the version another tab saved', () => {
    const { result, rerender } = renderHook(({ server }) => useDraftEditor(server), { initialProps: { server: draft(3) } })

    // Tab B saved a renamed department; tab A refetches it.
    rerender({ server: draft(4, { ...config, name: 'Đội nền tảng' }) })

    expect(result.current.dirty).toBe(false)
    expect(result.current.editVersion).toBe(4)
    expect(result.current.config.name).toBe('Đội nền tảng')
  })

  it('an edited tab keeps its work and its original version, so saving reports a conflict', () => {
    const { result, rerender } = renderHook(({ server }) => useDraftEditor(server), { initialProps: { server: draft(3) } })
    act(() => result.current.update((c) => ({ ...c, theme: 'EMERALD' })))

    rerender({ server: draft(4, { ...config, name: 'Đội nền tảng' }) })

    expect(result.current.dirty).toBe(true)
    expect(result.current.editVersion).toBe(3)
    expect(result.current.config.theme).toBe('EMERALD')
  })

  it('its own save becomes the new baseline even though the server normalizes the payload', () => {
    const { result, rerender } = renderHook(({ server }) => useDraftEditor(server), { initialProps: { server: draft(3) } })
    act(() => result.current.update((c) => ({ ...c, name: 'Phòng mới ' })))

    rerender({ server: draft(4, { ...config, name: 'Phòng mới' }) })

    expect(result.current.dirty).toBe(false)
    expect(result.current.editVersion).toBe(4)
  })

  it('reverting an edit by hand clears the unsaved state', () => {
    const { result } = renderHook(() => useDraftEditor(draft(3)))
    act(() => result.current.update((c) => ({ ...c, theme: 'AMBER' })))
    act(() => result.current.update((c) => ({ ...c, theme: 'INDIGO' })))

    expect(result.current.dirty).toBe(false)
  })
})
