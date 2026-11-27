import { afterEach, expect, it, vi } from 'vitest'
import { apiRequest, ApiError } from './http'
import { session, type SessionResponse } from '@/features/auth/session'

afterEach(() => {
  session.clear()
  vi.unstubAllGlobals()
})

it('refresh một lần khi nhận 401 rồi gửi lại yêu cầu với token mới', async () => {
  const updated: SessionResponse = {
    accessToken: 'new-token',
    expiresAt: new Date(Date.now() + 900_000).toISOString(),
    user: { id: 'u', displayName: 'User', email: 'u@example.test', systemRole: 'USER', mustChangePassword: false },
  }
  const fetch = vi
    .fn()
    .mockResolvedValueOnce(new Response(null, { status: 401 }))
    .mockResolvedValueOnce(Response.json(updated))
    .mockResolvedValueOnce(Response.json({ id: 'task' }))
  vi.stubGlobal('fetch', fetch)
  await expect(apiRequest('/tasks/task')).resolves.toEqual({ id: 'task' })
  expect(fetch).toHaveBeenCalledTimes(3)
  expect(fetch.mock.calls[2]?.[1]?.headers.Authorization).toBe('Bearer new-token')
})

it('lỗi 403 giữ nguyên thông tin và không refresh phiên', async () => {
  const fetch = vi.fn().mockResolvedValue(Response.json({ code: 'forbidden', detail: 'Không có quyền.' }, { status: 403 }))
  vi.stubGlobal('fetch', fetch)
  await expect(apiRequest('/projects')).rejects.toMatchObject({ code: 'forbidden', status: 403 })
  expect(fetch).toHaveBeenCalledTimes(1)
})

it('refresh bị từ chối trả lỗi 401 và xóa phiên', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })))
  await expect(apiRequest('/projects')).rejects.toBeInstanceOf(ApiError)
  expect(session.current.status).toBe('anonymous')
})
