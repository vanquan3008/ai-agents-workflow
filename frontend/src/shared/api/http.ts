import { session } from '@/features/auth/session'
import { env } from '@/shared/config/env'

/** Lỗi API theo ProblemDetails (RFC 9457) mà backend trả về, kèm mã ổn định `code`. */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly errors: Record<string, string[]>
  readonly traceId: string | undefined

  constructor(status: number, code: string, message: string, errors: Record<string, string[]> = {}, traceId?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.errors = errors
    this.traceId = traceId
  }

  get isForbidden() {
    return this.status === 403
  }

  get isNotFound() {
    return this.status === 404
  }

  /** Lỗi đầu tiên của một trường (để hiện dưới ô nhập). */
  field(name: string): string | undefined {
    return this.errors[name]?.join(' ')
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  headers?: Record<string, string>
  signal?: AbortSignal
  query?: Record<string, string | number | boolean | null | undefined>
  /** Bỏ qua cơ chế tự refresh (dùng cho chính endpoint xác thực). */
  skipAuthRefresh?: boolean
}

function buildUrl(path: string, query?: RequestOptions['query']): string {
  const url = `${env.apiBaseUrl}/api/v1${path}`
  if (!query) return url
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
  }
  const qs = params.toString()
  return qs ? `${url}?${qs}` : url
}

export async function parseProblem(response: Response): Promise<ApiError> {
  let problem: ProblemDetails = {}
  try {
    problem = (await response.json()) as ProblemDetails
  } catch {
    // Phản hồi không phải JSON (proxy lỗi, 502...).
  }
  return new ApiError(
    response.status,
    problem.code ?? `http_${response.status}`,
    problem.detail ?? problem.title ?? response.statusText ?? 'Lỗi không xác định',
    problem.errors ?? {},
    problem.traceId,
  )
}

function send(path: string, options: RequestOptions): Promise<Response> {
  const token = session.accessToken
  return fetch(buildUrl(path, options.query), {
    method: options.method ?? 'GET',
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      'X-DevAgent-Client': 'web',
      ...(options.body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options.signal,
  })
}

/**
 * Gọi API dùng chung: base URL, bearer token, JSON và chuẩn hóa lỗi.
 * Gặp 401 thì refresh phiên một lần rồi gửi lại; refresh thất bại thì phiên bị xóa và giao diện về trang đăng nhập.
 */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  let response = await send(path, options)
  if (response.status === 401 && !options.skipAuthRefresh && (await session.refresh())) {
    response = await send(path, options)
  }

  if (!response.ok) {
    const error = await parseProblem(response)
    if (error.code === 'auth.password_change_required' && session.current.status === 'authenticated') {
      // Server yêu cầu đổi mật khẩu: đồng bộ cờ để route guard chuyển trang.
      session.apply({
        accessToken: session.current.accessToken,
        expiresAt: new Date(session.current.expiresAt).toISOString(),
        user: { ...session.current.user, mustChangePassword: true },
      })
    }
    throw error
  }
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

/** Khóa chống gửi trùng cho thao tác tạo: giữ nguyên khi người dùng bấm lại hoặc mạng thử lại. */
export function newIdempotencyKey(prefix = 'dash'): string {
  return `${prefix}-${crypto.randomUUID()}`
}
