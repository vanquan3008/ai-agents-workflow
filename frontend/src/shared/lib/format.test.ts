import { describe, expect, it } from 'vitest'
import { formatDuration, initials, prettyJson, shortSha } from './format'

describe('format helpers', () => {
  it('formats durations at second, minute and hour scale', () => {
    expect(formatDuration(null)).toBe('—')
    expect(formatDuration(0.4)).toBe('<1 giây')
    expect(formatDuration(42)).toBe('42 giây')
    expect(formatDuration(125)).toBe('2 phút 5 giây')
    expect(formatDuration(3_900)).toBe('1 giờ 5 phút')
  })

  it('pretty-prints JSON and leaves non-JSON untouched', () => {
    expect(prettyJson('{"a":1}')).toBe('{\n  "a": 1\n}')
    expect(prettyJson('not json')).toBe('not json')
    expect(prettyJson(null)).toBe('')
  })

  it('shortens commit SHAs and builds initials from the last two words', () => {
    expect(shortSha('0123456789abcdef')).toBe('0123456')
    expect(shortSha(undefined)).toBe('—')
    expect(initials('Nguyễn Văn An')).toBe('VA')
  })
})
