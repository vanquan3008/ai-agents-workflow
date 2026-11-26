import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// globals: false nên Testing Library không tự đăng ký cleanup; dọn DOM sau mỗi test.
afterEach(() => {
  cleanup()
})
