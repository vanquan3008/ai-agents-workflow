/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string
  readonly VITE_SIGNALR_HUB_PATH?: string
  readonly VITE_AUTH_MODE?: 'development' | 'oidc'
  readonly VITE_DEV_USER_ID?: string
  readonly VITE_APP_NAME?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
