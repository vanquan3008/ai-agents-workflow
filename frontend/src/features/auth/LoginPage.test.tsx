import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApiError } from '@/shared/api/http'
import { LoginPage } from './LoginPage'

const { login } = vi.hoisted(() => ({ login: vi.fn() }))
vi.mock('./AuthProvider', () => ({ useAuth: () => ({ login }) }))

afterEach(() => {
  cleanup()
  login.mockReset()
})

it('gửi thông tin đã nhập và hiện lỗi đăng nhập từ API', async () => {
  login.mockRejectedValue(new ApiError(401, 'auth.invalid_credentials', 'Email hoặc mật khẩu chưa đúng.'))
  render(<LoginPage />)
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Email'), 'owner@example.test')
  await user.type(screen.getByLabelText('Mật khẩu'), 'Example-pass-123')
  await user.click(screen.getByRole('button', { name: 'Đăng nhập' }))
  expect(login).toHaveBeenCalledWith('owner@example.test', 'Example-pass-123')
  expect(await screen.findByText('Email hoặc mật khẩu chưa đúng.')).toBeInTheDocument()
  await waitFor(() => expect(screen.getByRole('button', { name: 'Đăng nhập' })).toBeEnabled())
})
