import { createBrowserRouter } from 'react-router'
import { LoginPage } from '@/features/auth/LoginPage'
import { EmptyBlock } from '@/shared/ui/page'
import { AppShell } from './AppShell'
import { GuestOnly, RequireAuth, RequireSuperAdmin } from './guards'

const page = (loader: () => Promise<{ default: React.ComponentType }>) => async () => ({ Component: (await loader()).default })

export const router = createBrowserRouter([
  {
    path: '/login',
    element: (
      <GuestOnly>
        <LoginPage />
      </GuestOnly>
    ),
  },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    handle: { crumb: 'DevAgent' },
    children: [
      { index: true, lazy: page(() => import('@/features/dashboard/DashboardPage')), handle: { crumb: 'Tổng quan' } },
      { path: 'change-password', lazy: page(() => import('@/features/auth/ChangePasswordPage')), handle: { crumb: 'Đổi mật khẩu' } },
      {
        path: 'tasks',
        handle: { crumb: 'Task' },
        children: [
          { index: true, lazy: page(() => import('@/features/tasks/TasksPage')) },
          { path: ':taskId', lazy: page(() => import('@/features/tasks/TaskDetailPage')), handle: { crumb: 'Chi tiết' } },
        ],
      },
      { path: 'executions/:executionId', lazy: page(() => import('@/features/executions/ExecutionPage')), handle: { crumb: 'Lần chạy' } },
      {
        path: 'approvals',
        handle: { crumb: 'Phê duyệt' },
        children: [
          { index: true, lazy: page(() => import('@/features/approvals/ApprovalsPage')) },
          { path: ':approvalId', lazy: page(() => import('@/features/approvals/ApprovalDetailPage')), handle: { crumb: 'Quyết định' } },
        ],
      },
      {
        path: 'projects',
        handle: { crumb: 'Project' },
        children: [
          { index: true, lazy: page(() => import('@/features/projects/ProjectsPage')) },
          { path: ':projectId', lazy: page(() => import('@/features/projects/ProjectDetailPage')), handle: { crumb: 'Chi tiết' } },
        ],
      },
      {
        path: 'repositories/:repositoryId/workflow',
        lazy: page(() => import('@/features/workflows/WorkflowPage')),
        handle: { crumb: 'Quy trình agent' },
      },
      {
        path: 'admin/users',
        handle: { crumb: 'Người dùng' },
        lazy: async () => {
          const { default: UsersPage } = await import('@/features/admin/UsersPage')
          return {
            element: (
              <RequireSuperAdmin>
                <UsersPage />
              </RequireSuperAdmin>
            ),
          }
        },
      },
      { path: '*', element: <EmptyBlock title="Không tìm thấy trang" description="Đường dẫn không tồn tại hoặc đã bị đổi." /> },
    ],
  },
])
