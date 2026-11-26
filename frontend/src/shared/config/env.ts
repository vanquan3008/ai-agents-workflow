import { z } from 'zod'

/**
 * Cấu hình dashboard, gộp từ hai nguồn (nguồn sau ghi đè nguồn trước):
 *  1. Biến VITE_* lúc build (mặc định cho `npm run dev`).
 *  2. Cấu hình runtime `window.__DEVAGENT_CONFIG__` từ `/config.js`, do container sinh lúc khởi động từ biến môi trường.
 * Nhờ (2), một image dashboard dùng được cho mọi môi trường mà không phải build lại.
 * Không đặt secret ở đây: mọi giá trị đều tới được trình duyệt.
 */
export interface RuntimeConfig {
  ENVIRONMENT?: string
  API_BASE_URL?: string
  SIGNALR_HUB_PATH?: string
  APP_NAME?: string
}

declare global {
  interface Window {
    __DEVAGENT_CONFIG__?: RuntimeConfig
  }
}

const Schema = z.object({
  environment: z.string().min(1),
  apiBaseUrl: z.union([z.literal(''), z.url({ protocol: /^https?$/ })]),
  hubPath: z.string().startsWith('/'),
  appName: z.string().min(1),
})

export type AppEnv = z.infer<typeof Schema> & { hubUrl: string }

export type EnvResult = { ok: true; env: AppEnv } | { ok: false; error: string }

const pick = (...values: (string | undefined)[]) => values.find((v) => v !== undefined && v !== '')

export function resolveEnv(runtime: RuntimeConfig | undefined, build: Partial<ImportMetaEnv>, isProductionBuild: boolean): EnvResult {
  const parsed = Schema.safeParse({
    environment: pick(runtime?.ENVIRONMENT) ?? (isProductionBuild ? 'Production' : 'Development'),
    apiBaseUrl: (pick(runtime?.API_BASE_URL, build.VITE_API_BASE_URL) ?? '').replace(/\/$/, ''),
    hubPath: pick(runtime?.SIGNALR_HUB_PATH, build.VITE_SIGNALR_HUB_PATH) ?? '/hubs/tasks',
    appName: pick(runtime?.APP_NAME, build.VITE_APP_NAME) ?? 'DevAgent',
  })

  if (!parsed.success) {
    return { ok: false, error: z.prettifyError(parsed.error) }
  }

  return { ok: true, env: { ...parsed.data, hubUrl: `${parsed.data.apiBaseUrl}${parsed.data.hubPath}` } }
}

export const envResult = resolveEnv(
  typeof window === 'undefined' ? undefined : window.__DEVAGENT_CONFIG__,
  import.meta.env,
  import.meta.env.PROD,
)

/** Cấu hình đã kiểm tra. main.tsx chỉ render ứng dụng khi cấu hình hợp lệ, nên các module khác dùng trực tiếp. */
export const env: AppEnv = envResult.ok
  ? envResult.env
  : new Proxy({} as AppEnv, {
      get: () => {
        throw new Error(`Cấu hình môi trường không hợp lệ:\n${envResult.error}`)
      },
    })
