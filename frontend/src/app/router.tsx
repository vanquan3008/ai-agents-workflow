import { createBrowserRouter } from 'react-router'
import { TaskListPage } from '../features/tasks/TaskListPage'
import { Empty } from '../shared/ui/States'
import { AppLayout } from './AppLayout'

// Trang đầu tải ngay; các trang còn lại tách chunk và tải khi điều hướng tới.
export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <TaskListPage /> },
      {
        path: 'tasks/:taskId',
        lazy: async () => ({ Component: (await import('../features/tasks/TaskDetailPage')).TaskDetailPage }),
      },
      {
        path: 'executions/:executionId',
        lazy: async () => ({ Component: (await import('../features/executions/ExecutionPage')).ExecutionPage }),
      },
      {
        path: 'approvals',
        lazy: async () => ({ Component: (await import('../features/approvals/ApprovalsPage')).ApprovalsPage }),
      },
      {
        path: 'approvals/:approvalId',
        lazy: async () => ({ Component: (await import('../features/approvals/ApprovalDetailPage')).ApprovalDetailPage }),
      },
      { path: '*', element: <Empty title="Không tìm thấy trang." /> },
    ],
  },
])
