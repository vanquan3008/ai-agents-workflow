import { describe, expect, it } from 'vitest'
import { resolveEnv } from './env'

const noBuildEnv = {}

describe('resolveEnv', () => {
  it('bản đóng gói đọc môi trường Development từ cấu hình runtime', () => {
    const result = resolveEnv({ ENVIRONMENT: 'Development' }, noBuildEnv, true)

    expect(result.ok).toBe(true)
    if (result.ok) expect(result.env.environment).toBe('Development')
  })

  it('chặn URL API sai định dạng', () => {
    const result = resolveEnv({ API_BASE_URL: 'javascript:alert(1)' }, noBuildEnv, true)

    expect(result.ok).toBe(false)
  })

  it('runtime ghi đè giá trị lúc build, chuỗi rỗng không ghi đè', () => {
    const build = { VITE_APP_NAME: 'Build name', VITE_API_BASE_URL: '' }
    const result = resolveEnv({ ENVIRONMENT: 'Staging', API_BASE_URL: 'https://api.example.com/', APP_NAME: '' }, build, true)

    expect(result.ok).toBe(true)
    if (result.ok) {
      expect(result.env.apiBaseUrl).toBe('https://api.example.com')
      expect(result.env.hubUrl).toBe('https://api.example.com/hubs/tasks')
      expect(result.env.appName).toBe('Build name')
    }
  })

  it('dev server dùng biến VITE_* khi không có cấu hình runtime', () => {
    const build = { VITE_APP_NAME: 'DevAgent local' }
    const result = resolveEnv({}, build, false)

    expect(result.ok).toBe(true)
    if (result.ok) expect(result.env.environment).toBe('Development')
  })
})
