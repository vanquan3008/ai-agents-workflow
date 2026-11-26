import { z } from 'zod'

/**
 * Dashboard configuration merged from two sources (later wins):
 *  1. Build-time VITE_* variables (defaults for `npm run dev`).
 *  2. Runtime `window.__DEVAGENT_CONFIG__` from `/config.js`, rendered by the container from environment variables.
 * Source 2 lets one dashboard image serve every environment without rebuilding.
 * Never put secrets here: every value reaches the browser.
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

/** Validated configuration. main.tsx renders the app only when it is valid, so other modules may use it directly. */
export const env: AppEnv = envResult.ok
  ? envResult.env
  : new Proxy({} as AppEnv, {
      get: () => {
        throw new Error(`Invalid environment configuration:\n${envResult.error}`)
      },
    })
