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

  it('thử lại một lần khi tab khác vừa xoay refresh token', async () => {
    const superseded = Response.json({ code: 'auth.refresh_superseded' }, { status: 401 })
    const fetch = vi.fn().mockResolvedValueOnce(superseded).mockResolvedValueOnce(Response.json(response))
    vi.stubGlobal('fetch', fetch)
    expect(await session.refresh()).toBe(true)
    expect(fetch).toHaveBeenCalledTimes(2)
    expect(session.accessToken).toBe(response.accessToken)
  })

  it('không thử lại khi phiên đã hết hạn thật', async () => {
    const expired = Response.json({ code: 'auth.session_expired' }, { status: 401 })
    const fetch = vi.fn().mockResolvedValue(expired)
    vi.stubGlobal('fetch', fetch)
    expect(await session.refresh()).toBe(false)
    expect(fetch).toHaveBeenCalledTimes(1)
    expect(session.current.status).toBe('anonymous')
  })

  it('báo token sắp hết hạn để kết nối realtime làm mới trước', () => {
    session.apply({ ...response, expiresAt: new Date(Date.now() + 10_000).toISOString() })
    expect(session.expiresWithin(30_000)).toBe(true)
    session.apply(response)
    expect(session.expiresWithin(30_000)).toBe(false)
  })
})
