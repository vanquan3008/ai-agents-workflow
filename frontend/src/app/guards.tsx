import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { Spinner } from '@/components/ui/spinner'
import { useAuth } from '@/features/auth/AuthProvider'

function FullScreenLoader() {
  return (
    <output className="text-muted-foreground flex min-h-svh items-center justify-center gap-2 text-sm">
      <Spinner /> Đang khôi phục phiên đăng nhập…
    </output>
  )
}

/** Requires a session; accounts on a temporary password are sent to the password change page. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { state } = useAuth()
  const location = useLocation()

  if (state.status === 'loading') return <FullScreenLoader />
  if (state.status === 'anonymous') {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }
  if (state.user.mustChangePassword && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />
  }
  return children
}

export function RequireSuperAdmin({ children }: { children: ReactNode }) {
  const { isSuperAdmin } = useAuth()
  return isSuperAdmin ? children : <Navigate to="/" replace />
}

/** Sign-in page only: signed-in users go back to where they came from. */
export function GuestOnly({ children }: { children: ReactNode }) {
  const { state } = useAuth()
  const location = useLocation()
  if (state.status === 'loading') return <FullScreenLoader />
  if (state.status === 'authenticated') {
    const from = (location.state as { from?: string } | null)?.from
    return <Navigate to={from && from !== '/login' ? from : '/'} replace />
  }
  return children
}
