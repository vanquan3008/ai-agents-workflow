import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ApprovalStatusBadge } from './ApprovalStatusBadge'

describe('ApprovalStatusBadge', () => {
  it('presents a pending approval as awaiting a decision', () => {
    render(<ApprovalStatusBadge status="PENDING" />)
    expect(screen.getByText('Chờ duyệt')).toBeInTheDocument()
    expect(screen.queryByText('Chờ xử lý')).not.toBeInTheDocument()
  })

  it('uses the shared label for decided approvals', () => {
    render(<ApprovalStatusBadge status="APPROVED" />)
    expect(screen.getByText('Đã duyệt')).toBeInTheDocument()
  })
})
