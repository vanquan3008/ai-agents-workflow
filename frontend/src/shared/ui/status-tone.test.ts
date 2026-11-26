import { describe, expect, it } from 'vitest'
import { isLiveStatus, toneOf } from './status-tone'

describe('status tone', () => {
  it('maps lifecycle statuses to semantic tones', () => {
    expect(toneOf('PLANNING')).toBe('info')
    expect(toneOf('WAITING_FOR_APPROVAL')).toBe('warning')
    expect(toneOf('COMPLETED')).toBe('success')
    expect(toneOf('FAILED')).toBe('danger')
    expect(toneOf('CANCELLED')).toBe('neutral')
  })

  it('falls back to neutral for unknown statuses', () => {
    expect(toneOf('SOMETHING_NEW')).toBe('neutral')
  })

  it('treats only actively processed statuses as live', () => {
    expect(isLiveStatus('RUNNING')).toBe(true)
    expect(isLiveStatus('BLOCKED')).toBe(false)
  })
})
