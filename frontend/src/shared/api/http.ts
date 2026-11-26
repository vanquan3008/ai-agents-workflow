import { env } from '../config/env'

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
}

interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
}

/** Header xác thực. Chế độ development gửi X-DevAgent-User; chế độ OIDC gắn bearer token (Phase 1.3). */
export function authHeaders(): Record<string, string> {
  if (env.authMode === 'development' && env.devUserId) {
    return { 'X-DevAgent-User': env.devUserId }
  }

  const token = sessionStorage.getItem('devagent.access_token')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  headers?: Record<string, string>
  signal?: AbortSignal
  query?: Record<string, string | number | boolean | null | undefined>
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

/** Gọi API dùng chung cho mọi tính năng: base URL, xác thực, JSON, và chuẩn hóa lỗi. */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await fetch(buildUrl(path, options.query), {
    method: options.method ?? 'GET',
    headers: {
      Accept: 'application/json',
      ...(options.body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...authHeaders(),
      ...options.headers,
    },
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options.signal,
  })

  if (!response.ok) throw await parseProblem(response)
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

/** Khóa chống gửi trùng cho thao tác tạo: giữ nguyên khi người dùng bấm lại hoặc mạng thử lại. */
export function newIdempotencyKey(prefix = 'dash'): string {
  return `${prefix}-${crypto.randomUUID()}`
}
