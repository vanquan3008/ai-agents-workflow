import { FolderGit2, LayoutDashboard, ListTodo, ShieldCheck, Users, type LucideIcon } from 'lucide-react'

export interface NavigationItem {
  to: string
  label: string
  icon: LucideIcon
  /** Matches only the exact path (used for the index route). */
  exact?: boolean
  superAdminOnly?: boolean
  /** Extra search terms for the command palette. */
  keywords?: string[]
}

export interface NavigationSection {
  label: string
  items: NavigationItem[]
}

export const APPROVALS_PATH = '/approvals'

export const NAVIGATION: NavigationSection[] = [
  {
    label: 'Vận hành',
    items: [
      { to: '/', label: 'Tổng quan', icon: LayoutDashboard, exact: true, keywords: ['dashboard', 'home'] },
      { to: '/tasks', label: 'Task', icon: ListTodo, keywords: ['công việc', 'bug', 'lỗi'] },
      { to: APPROVALS_PATH, label: 'Phê duyệt', icon: ShieldCheck, keywords: ['approval', 'duyệt'] },
    ],
  },
  {
    label: 'Cấu hình',
    items: [{ to: '/projects', label: 'Project & quy trình', icon: FolderGit2, keywords: ['repository', 'repo', 'workflow'] }],
  },
  {
    label: 'Quản trị',
    items: [{ to: '/admin/users', label: 'Người dùng', icon: Users, superAdminOnly: true, keywords: ['user', 'tài khoản'] }],
  },
]

export function visibleNavigation(isSuperAdmin: boolean): NavigationSection[] {
  return NAVIGATION.map((section) => ({
    ...section,
    items: section.items.filter((item) => !item.superAdminOnly || isSuperAdmin),
  })).filter((section) => section.items.length > 0)
}

export function isNavigationItemActive(item: NavigationItem, pathname: string): boolean {
  return item.exact ? pathname === item.to : pathname === item.to || pathname.startsWith(`${item.to}/`)
}
