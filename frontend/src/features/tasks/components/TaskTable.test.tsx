import { describe, expect, it } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import type { TaskSummary } from '@/shared/api/types'
import { TaskTable } from './TaskTable'

const task: TaskSummary = {
  id: '0193-task',
  projectId: 'project-1',
  repositoryId: 'repo-1',
  repositoryFullName: 'acme/web-app',
  title: 'Bộ lọc trạng thái trả về sai task',
  status: 'WAITING_FOR_APPROVAL',
  statusReason: null,
  channel: 'DISCORD',
  pullRequestUrl: null,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
}

describe('TaskTable', () => {
  it('links each task to its detail page and shows its status label', () => {
    render(
      <MemoryRouter>
        <TaskTable tasks={[task]} />
      </MemoryRouter>,
    )
    const link = screen.getByRole('link', { name: task.title })
    expect(link).toHaveAttribute('href', '/tasks/0193-task')
    const row = link.closest('tr')!
    expect(within(row).getByText('Chờ duyệt')).toBeInTheDocument()
    expect(within(row).getByText('acme/web-app')).toBeInTheDocument()
    expect(within(row).getByText('Discord')).toBeInTheDocument()
  })

  it('hides the channel column in compact mode', () => {
    render(
      <MemoryRouter>
        <TaskTable tasks={[task]} compact />
      </MemoryRouter>,
    )
    expect(screen.queryByRole('columnheader', { name: 'Kênh' })).not.toBeInTheDocument()
  })
})
