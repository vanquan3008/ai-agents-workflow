import { describe, expect, it } from 'vitest'
import type { TaskStatus, TimelineEntry } from '@/shared/api/types'
import { stationStates, taskPosition, waitingFor } from './stations'

const entry = (sequence: number, from: TaskStatus | null, to: TaskStatus): TimelineEntry => ({
  sequence,
  from,
  to,
  actorKind: 'SYSTEM',
  actorId: 'system',
  reason: null,
  occurredAt: '2026-11-27T08:00:00Z',
})

describe('taskPosition', () => {
  it.each<[TaskStatus, string, string]>([
    ['PENDING', 'PLANNING', 'waiting'],
    ['PLANNING', 'PLANNING', 'active'],
    ['NEEDS_CLARIFICATION', 'PLANNING', 'waiting'],
    ['WAITING_FOR_APPROVAL', 'APPROVAL', 'waiting'],
    ['IN_PROGRESS', 'DEVELOPMENT', 'active'],
    ['TESTING', 'TESTING', 'active'],
    ['REVIEWING', 'REVIEW', 'active'],
    ['PUBLISHING', 'PUBLISHING', 'active'],
    ['COMPLETED', 'PUBLISHING', 'done'],
  ])('maps %s to %s (%s)', (status, station, state) => {
    expect(taskPosition(status)).toEqual({ station, state })
  })

  it('keeps a stopped task at the station it stopped in', () => {
    const timeline = [entry(1, null, 'PENDING'), entry(2, 'PENDING', 'PLANNING'), entry(3, 'PLANNING', 'WAITING_FOR_APPROVAL'), entry(4, 'WAITING_FOR_APPROVAL', 'REJECTED')]

    expect(taskPosition('REJECTED', timeline)).toEqual({ station: 'APPROVAL', state: 'failed' })
    expect(taskPosition('CANCELLED', [entry(1, 'TESTING', 'CANCELLED')])).toEqual({ station: 'TESTING', state: 'stopped' })
  })

  it('falls back to planning when the stop transition is not loaded yet', () => {
    expect(taskPosition('FAILED')).toEqual({ station: 'PLANNING', state: 'failed' })
  })
})

describe('stationStates', () => {
  it('marks visited stations from history rather than a fixed order', () => {
    // Plan approval happens before development: review and testing must stay idle.
    const timeline = [entry(1, null, 'PENDING'), entry(2, 'PENDING', 'PLANNING'), entry(3, 'PLANNING', 'WAITING_FOR_APPROVAL')]

    expect(stationStates('WAITING_FOR_APPROVAL', timeline)).toEqual({
      PLANNING: 'passed',
      DEVELOPMENT: 'idle',
      TESTING: 'idle',
      REVIEW: 'idle',
      APPROVAL: 'waiting',
      PUBLISHING: 'idle',
    })
  })
})

describe('waitingFor', () => {
  it('names who must act for waiting tasks only', () => {
    expect(waitingFor('NEEDS_CLARIFICATION')).toContain('Người yêu cầu')
    expect(waitingFor('WAITING_FOR_APPROVAL')).toContain('Approver')
    expect(waitingFor('PLANNING')).toBeNull()
  })
})
