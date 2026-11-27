import type { CSSProperties } from 'react'
import type { DepartmentTheme, RoomAction } from '../types'
import type { StationState } from './stations'

export const THEME_LABELS: Record<DepartmentTheme, string> = {
  INDIGO: 'Chàm',
  EMERALD: 'Lục bảo',
  AMBER: 'Hổ phách',
  SLATE: 'Đá phiến',
}

// Accents keep enough contrast in both light and dark themes; surfaces use them only at low alpha.
const THEME_ACCENTS: Record<DepartmentTheme, string> = {
  INDIGO: 'oklch(0.55 0.19 274)',
  EMERALD: 'oklch(0.58 0.13 160)',
  AMBER: 'oklch(0.66 0.14 70)',
  SLATE: 'oklch(0.55 0.04 255)',
}

/** Exposes the department accent as a CSS variable for the floor plan and cards. */
export const themeStyle = (theme: DepartmentTheme): CSSProperties => ({ '--office-accent': THEME_ACCENTS[theme] }) as CSSProperties

export const ASSET_PRESET_LABELS: Record<string, string> = {
  'planning-table': 'Bàn kế hoạch',
  workbench: 'Bàn làm việc',
  'test-bench': 'Bàn kiểm thử',
  'diff-display': 'Màn hình diff',
  'meeting-table': 'Bàn họp',
  'release-desk': 'Bàn phát hành',
  'reception-desk': 'Quầy tiếp nhận',
  bookshelf: 'Kệ tài liệu',
  lounge: 'Khu chờ',
  'server-rack': 'Tủ máy chủ',
}

export const presetLabel = (key: string) => ASSET_PRESET_LABELS[key] ?? key

export const ACTION_LABELS: Record<RoomAction, string> = {
  OPEN_TASKS: 'Mở danh sách task',
  OPEN_APPROVALS: 'Mở hàng chờ phê duyệt',
  OPEN_PROJECT: 'Mở trang project',
  OPEN_WORKFLOW: 'Mở quy trình repository',
}

/** In-app destination of an informational room. Rooms never carry URLs, so links can only point inside the app. */
export function actionTarget(action: RoomAction, projectId: string, repositoryId: string | undefined): string {
  switch (action) {
    case 'OPEN_TASKS':
      return `/tasks?project=${projectId}`
    case 'OPEN_APPROVALS':
      return '/approvals'
    case 'OPEN_PROJECT':
      return `/projects/${projectId}`
    case 'OPEN_WORKFLOW':
      return repositoryId ? `/repositories/${repositoryId}/workflow` : `/projects/${projectId}`
  }
}

/** Ring and surface treatment of a room on the floor for each station state. */
export const ROOM_STATE_CLASSES: Record<StationState, string> = {
  active: 'border-info/60 bg-info/[0.07] ring-2 ring-info/30',
  waiting: 'border-warning/60 border-dashed bg-warning/[0.06]',
  failed: 'border-danger/60 bg-danger/[0.06]',
  stopped: 'border-neutral/50 bg-neutral/[0.05]',
  done: 'border-success/50 bg-success/[0.06]',
  passed: 'border-success/30 bg-card',
  idle: 'border-border bg-card',
}
