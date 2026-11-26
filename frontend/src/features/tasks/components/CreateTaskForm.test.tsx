import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../shared/api/http'
import type { Repository } from '../../../shared/api/types'
import { CreateTaskForm } from './CreateTaskForm'

const repo: Repository = {
  id: 'repo-1',
  projectId: 'p-1',
  owner: 'acme',
  name: 'app',
  fullName: 'acme/app',
  defaultBranch: 'main',
  installationId: 1,
  policy: { version: 1, autoFixPaths: [], sensitivePaths: [], buildCommand: 'b', testCommand: 't', maxChangedFiles: 20 },
  isActive: true,
  createdAt: '2026-11-26T00:00:00Z',
}

describe('CreateTaskForm', () => {
  it('chặn gửi khi thiếu tiêu đề và mô tả', async () => {
    const onSubmit = vi.fn()
    render(<CreateTaskForm repositories={[repo]} onSubmit={onSubmit} />)

    await userEvent.click(screen.getByRole('button', { name: 'Tạo task' }))

    expect(onSubmit).not.toHaveBeenCalled()
    expect(screen.getByText('Nhập tiêu đề.')).toBeInTheDocument()
    expect(screen.getByLabelText('Tiêu đề')).toHaveAttribute('aria-invalid', 'true')
  })

  it('giữ nguyên idempotency key khi gửi lại sau lỗi, đổi key sau khi thành công', async () => {
    const onSubmit = vi
      .fn()
      .mockRejectedValueOnce(new ApiError(503, 'unavailable', 'Máy chủ bận'))
      .mockResolvedValueOnce({ taskId: 't-1', status: 'PENDING', created: true })
      .mockResolvedValueOnce({ taskId: 't-2', status: 'PENDING', created: true })
    const user = userEvent.setup()
    render(<CreateTaskForm repositories={[repo]} onSubmit={onSubmit} />)

    const fill = async () => {
      await user.type(screen.getByLabelText('Tiêu đề'), 'Bộ lọc sai')
      await user.type(screen.getByLabelText('Mô tả lỗi'), 'Lọc DONE trả IN_PROGRESS')
    }

    await fill()
    await user.click(screen.getByRole('button', { name: 'Tạo task' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Máy chủ bận')

    await user.click(screen.getByRole('button', { name: 'Tạo task' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Đã tạo task.')

    await fill()
    await user.click(screen.getByRole('button', { name: 'Tạo task' }))

    const keys = onSubmit.mock.calls.map(([input]) => (input as { idempotencyKey: string }).idempotencyKey)
    expect(keys[0]).toBe(keys[1])
    expect(keys[2]).not.toBe(keys[1])
  })

  it('hiển thị lỗi theo trường từ ProblemDetails của server', async () => {
    const onSubmit = vi.fn().mockRejectedValue(new ApiError(400, 'task.invalid', 'Dữ liệu sai', { title: ['Tiêu đề quá dài.'] }))
    const user = userEvent.setup()
    render(<CreateTaskForm repositories={[repo]} onSubmit={onSubmit} />)

    await user.type(screen.getByLabelText('Tiêu đề'), 'x')
    await user.type(screen.getByLabelText('Mô tả lỗi'), 'y')
    await user.keyboard('{Enter}')
    await user.click(screen.getByRole('button', { name: 'Tạo task' }))

    expect(await screen.findByText('Tiêu đề quá dài.')).toBeInTheDocument()
  })
})
