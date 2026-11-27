import type { ReactNode } from 'react'
import { describe, expect, it } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { useTaskFilters } from './use-task-filters'

const wrapperAt = (url: string) =>
  function Wrapper({ children }: { children: ReactNode }) {
    return <MemoryRouter initialEntries={[url]}>{children}</MemoryRouter>
  }

describe('useTaskFilters', () => {
  it('reads filters from the URL and ignores unknown statuses', () => {
    const { result } = renderHook(() => useTaskFilters(), { wrapper: wrapperAt('/tasks?status=FAILED&q=filter&page=3') })
    expect(result.current.status).toBe('FAILED')
    expect(result.current.search).toBe('filter')
    expect(result.current.page).toBe(3)
    expect(result.current.hasFilters).toBe(true)

    const invalid = renderHook(() => useTaskFilters(), { wrapper: wrapperAt('/tasks?status=NOPE&page=-2') })
    expect(invalid.result.current.status).toBe('')
    expect(invalid.result.current.page).toBe(1)
  })

  it('resets to the first page when a filter changes and keeps the page when paging', () => {
    const { result } = renderHook(() => useTaskFilters(), { wrapper: wrapperAt('/tasks?page=4') })

    act(() => result.current.update({ page: 5 }))
    expect(result.current.page).toBe(5)

    act(() => result.current.update({ status: 'BLOCKED' }))
    expect(result.current.status).toBe('BLOCKED')
    expect(result.current.page).toBe(1)

    act(() => result.current.reset())
    expect(result.current.hasFilters).toBe(false)
  })
})
