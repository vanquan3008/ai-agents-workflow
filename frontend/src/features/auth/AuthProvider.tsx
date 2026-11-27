import { createContext, use, useEffect, useMemo, useSyncExternalStore, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { apiRequest } from '@/shared/api/http'
import { taskHub } from '@/shared/realtime/taskHub'
import { session, type CurrentUser, type SessionResponse, type SessionState } from './session'

interface AuthContextValue {
  state: SessionState
  user: CurrentUser | null
  isSuperAdmin: boolean
  login: (email: string, password: string) => Promise<CurrentUser>
  logout: () => Promise<void>
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const state = useSyncExternalStore(
    (listener) => session.subscribe(listener),
    () => session.current,
  )
  const queryClient = useQueryClient()
  const userId = state.status === 'authenticated' ? state.user.id : null

  useEffect(() => {
    if (!userId) void taskHub.disconnect()
  }, [userId])

  // Restore the session from the refresh cookie on page load.
  useEffect(() => {
    if (session.current.status === 'loading') void session.refresh()
  }, [])

  const value = useMemo<AuthContextValue>(() => {
    const user = state.status === 'authenticated' ? state.user : null
    return {
      state,
      user,
      isSuperAdmin: user?.systemRole === 'SUPER_ADMIN',
      login: async (email, password) => {
        const result = await apiRequest<SessionResponse>('/auth/login', { method: 'POST', body: { email, password }, skipAuthRefresh: true })
        queryClient.clear()
        session.apply(result)
        return result.user
      },
      logout: async () => {
        try {
          await apiRequest('/auth/logout', { method: 'POST', skipAuthRefresh: true })
        } finally {
          queryClient.clear()
          session.clear()
        }
      },
      changePassword: async (currentPassword, newPassword) => {
        const result = await apiRequest<SessionResponse>('/auth/change-password', { method: 'POST', body: { currentPassword, newPassword } })
        session.apply(result)
        await queryClient.invalidateQueries()
      },
    }
  }, [state, queryClient])

  return <AuthContext value={value}>{children}</AuthContext>
}

export function useAuth(): AuthContextValue {
  const context = use(AuthContext)
  if (!context) throw new Error('useAuth must be used inside AuthProvider')
  return context
}
