import { z } from 'zod'

/**
 * Cấu hình runtime của dashboard, đọc từ biến VITE_* lúc build và kiểm tra bằng zod.
 * Không đặt secret ở đây: mọi biến VITE_* đều bị nhúng vào bundle gửi tới trình duyệt.
 */
const EnvSchema = z
  .object({
    VITE_API_BASE_URL: z.string().default(''),
    VITE_SIGNALR_HUB_PATH: z.string().startsWith('/').default('/hubs/tasks'),
    VITE_AUTH_MODE: z.enum(['development', 'oidc']).default('oidc'),
    VITE_DEV_USER_ID: z.uuid().optional().or(z.literal('').transform(() => undefined)),
    VITE_APP_NAME: z.string().default('DevAgent'),
  })
  .superRefine((value, ctx) => {
    if (value.VITE_AUTH_MODE === 'development' && import.meta.env.PROD) {
      ctx.addIssue({
        code: 'custom',
        path: ['VITE_AUTH_MODE'],
        message: 'Chế độ xác thực development không được dùng cho bản build production.',
      })
    }
  })

const parsed = EnvSchema.safeParse(import.meta.env)

if (!parsed.success) {
  // Dừng sớm với thông báo rõ ràng thay vì lỗi khó hiểu lúc chạy.
  throw new Error(`Cấu hình môi trường không hợp lệ:\n${z.prettifyError(parsed.error)}`)
}

export const env = {
  apiBaseUrl: parsed.data.VITE_API_BASE_URL.replace(/\/$/, ''),
  hubUrl: `${parsed.data.VITE_API_BASE_URL.replace(/\/$/, '')}${parsed.data.VITE_SIGNALR_HUB_PATH}`,
  authMode: parsed.data.VITE_AUTH_MODE,
  devUserId: parsed.data.VITE_DEV_USER_ID,
  appName: parsed.data.VITE_APP_NAME,
} as const
