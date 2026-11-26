import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiRequest, newIdempotencyKey } from './http'

describe('apiRequest', () => {
  afterEach(() => vi.restoreAllMocks())

  it('gửi header xác thực development và dựng query string', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ ok: true }), { status: 200 }))

    await apiRequest('/tasks', { query: { status: 'PENDING', page: 2, search: '' } })

    const [url, init] = fetchMock.mock.calls[0]!
    expect(url).toBe('/api/v1/tasks?status=PENDING&page=2')
    expect((init!.headers as Record<string, string>)['X-DevAgent-User']).toBe('0193a1c0-0000-7000-8000-000000000001')
  })

  it('chuyển ProblemDetails thành ApiError có mã ổn định', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ status: 403, title: 'Không có quyền', detail: 'Bạn không có quyền.', code: 'auth.forbidden' }), {
        status: 403,
      }),
    )

    const error = await apiRequest('/tasks/x').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).code).toBe('auth.forbidden')
    expect((error as ApiError).isForbidden).toBe(true)
  })

  it('sinh idempotency key duy nhất, hợp lệ với backend', () => {
    const a = newIdempotencyKey()
    const b = newIdempotencyKey()
    expect(a).not.toBe(b)
    expect(a).toMatch(/^[A-Za-z0-9._:-]+$/)
  })
})
