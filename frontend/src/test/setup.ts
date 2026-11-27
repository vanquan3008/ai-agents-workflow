import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// With globals: false Testing Library cannot register cleanup itself, so clean the DOM after each test.
afterEach(() => {
  cleanup()
})
