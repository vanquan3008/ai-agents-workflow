import { env } from '@/shared/config/env'

export interface CurrentUser {
  id: string
  email: string
  displayName: string
  systemRole: 'USER' | 'SUPER_ADMIN'
  mustChangePassword: boolean
}

export interface SessionResponse {
  accessToken: string
  expiresAt: string
  user: CurrentUser
}

export type SessionState =
  | { status: 'loading' }
  | { status: 'anonymous' }
  | { status: 'authenticated'; accessToken: string; expiresAt: number; user: CurrentUser }

type Listener = (state: SessionState) => void

/**
 * In-memory session store (never localStorage, so XSS cannot steal a long-lived token).
 * The access token is short-lived; the refresh token lives in an HttpOnly cookie that JavaScript cannot read.
 */
class SessionStore {
  private state: SessionState = { status: 'loading' }
  private readonly listeners = new Set<Listener>()
  private refreshing: Promise<boolean> | null = null
  private timer: ReturnType<typeof setTimeout> | undefined
  private generation = 0

  get current(): SessionState {
    return this.state
  }

  get accessToken(): string | null {
    return this.state.status === 'authenticated' ? this.state.accessToken : null
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  apply(session: SessionResponse) {
    this.generation++
    this.set({ status: 'authenticated', accessToken: session.accessToken, expiresAt: Date.parse(session.expiresAt), user: session.user })
    this.schedule()
  }

  clear() {
    this.generation++
    clearTimeout(this.timer)
    this.set({ status: 'anonymous' })
  }

  /** Exchanges the refresh cookie for a new access token; concurrent callers share one request (single flight). */
  refresh(): Promise<boolean> {
    this.refreshing ??= this.doRefresh().finally(() => {
      this.refreshing = null
    })
    return this.refreshing
  }

  private async doRefresh(): Promise<boolean> {
    const generation = this.generation
    try {
      const response = await fetch(`${env.apiBaseUrl}/api/v1/auth/refresh`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'X-DevAgent-Client': 'web', Accept: 'application/json' },
      })
      if (generation !== this.generation) return false
      if (!response.ok) {
        this.clear()
        return false
      }
      const result = (await response.json()) as SessionResponse
      if (generation !== this.generation) return false
      this.apply(result)
      return true
    } catch {
      // Offline: keep an authenticated state; the next API call retries.
      if (generation === this.generation && this.state.status === 'loading') this.clear()
      return false
    }
  }

  /** Refreshes 60 seconds before the access token expires. */
  private schedule() {
    clearTimeout(this.timer)
    if (this.state.status !== 'authenticated') return
    const delay = Math.max(5_000, this.state.expiresAt - Date.now() - 60_000)
    this.timer = setTimeout(() => void this.refresh(), delay)
  }

  private set(state: SessionState) {
    this.state = state
    this.listeners.forEach((listener) => listener(state))
  }
}

export const session = new SessionStore()
