import { afterEach, describe, expect, it, vi } from 'vitest'
import { session, type SessionResponse } from './session'

const response: SessionResponse = {
  accessToken: 'test-access-token',
  expiresAt: new Date(Date.now() + 900_000).toISOString(),
  user: { id: 'user-1', email: 'user@example.test', displayName: 'User', systemRole: 'USER', mustChangePassword: false },
}

afterEach(() => {
  session.clear()
  vi.unstubAllGlobals()
})

describe('phiên đăng nhập', () => {
  it('gộp các yêu cầu refresh đồng thời thành một lần gọi', async () => {
    const fetch = vi.fn().mockResolvedValue(Response.json(response))
    vi.stubGlobal('fetch', fetch)
    const results = await Promise.all([session.refresh(), session.refresh(), session.refresh()])
    expect(results).toEqual([true, true, true])
    expect(fetch).toHaveBeenCalledTimes(1)
    expect(session.accessToken).toBe(response.accessToken)
  })

  it('refresh hoàn tất sau khi đăng xuất không khôi phục lại phiên cũ', async () => {
    let resolve!: (value: Response) => void
    vi.stubGlobal(
      'fetch',
      vi.fn(
        () =>
          new Promise<Response>((done) => {
            resolve = done
          }),
      ),
    )
    const refresh = session.refresh()
    session.clear()
    resolve(Response.json(response))
    expect(await refresh).toBe(false)
    expect(session.current.status).toBe('anonymous')
  })

  it('refresh của phiên cũ không ghi đè tài khoản vừa đăng nhập', async () => {
    let resolve!: (value: Response) => void
    vi.stubGlobal(
      'fetch',
      vi.fn(
        () =>
          new Promise<Response>((done) => {
            resolve = done
          }),
      ),
    )
    const refresh = session.refresh()
    session.apply({ ...response, accessToken: 'new-user-token', user: { ...response.user, id: 'user-2' } })
    resolve(Response.json(response))
    expect(await refresh).toBe(false)
    expect(session.accessToken).toBe('new-user-token')
  })
})
