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
 * Kho phiên đăng nhập trong bộ nhớ (không lưu localStorage để XSS không lấy được token lâu dài).
 * Access token ngắn hạn; refresh token nằm trong cookie httpOnly do API đặt, JavaScript không đọc được.
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

  /** Đổi refresh token (cookie) lấy access token mới. Gộp các lời gọi đồng thời thành một (single-flight). */
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
      // Mất mạng: giữ trạng thái hiện tại nếu đang đăng nhập, lần gọi API sau sẽ thử lại.
      if (generation === this.generation && this.state.status === 'loading') this.clear()
      return false
    }
  }

  /** Làm mới trước khi access token hết hạn 60 giây. */
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
