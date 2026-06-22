import '@testing-library/jest-dom/vitest'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { cleanup } from '@testing-library/react'
import { server } from './mocks/server.ts'
import { clearMockAuthCookie } from './mocks/auth-cookie.ts'
import { resetMockAccountProfile } from './mocks/handlers.ts'

function createMemoryStorage(): Storage {
  const store = new Map<string, string>()

  return {
    get length() {
      return store.size
    },
    clear: () => store.clear(),
    getItem: (key) => store.get(key) ?? null,
    key: (index) => Array.from(store.keys())[index] ?? null,
    removeItem: (key) => {
      store.delete(key)
    },
    setItem: (key, value) => {
      store.set(key, String(value))
    },
  }
}

function createDomRect(x = 0, y = 0, width = 0, height = 0): DOMRect {
  return {
    x,
    y,
    width,
    height,
    top: y,
    right: x + width,
    bottom: y + height,
    left: x,
    toJSON: () => ({}),
  } as DOMRect
}

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string): MediaQueryList => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => undefined,
    removeListener: () => undefined,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    dispatchEvent: () => false,
  }),
})

Object.defineProperty(Range.prototype, 'getClientRects', {
  configurable: true,
  value: () => [createDomRect(0, 0, 100, 16)],
})

Object.defineProperty(Range.prototype, 'getBoundingClientRect', {
  configurable: true,
  value: () => createDomRect(0, 0, 100, 16),
})

Object.defineProperty(HTMLElement.prototype, 'getClientRects', {
  configurable: true,
  value: () => [createDomRect(0, 0, 100, 16)],
})

Object.defineProperty(HTMLElement.prototype, 'getBoundingClientRect', {
  configurable: true,
  value: () => createDomRect(0, 0, 100, 16),
})

const testStorage = createMemoryStorage()

Object.defineProperty(window, 'localStorage', {
  configurable: true,
  value: testStorage,
})

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))

afterEach(() => {
  cleanup()
  window.localStorage.clear()
  clearMockAuthCookie()
  resetMockAccountProfile()
  document.documentElement.removeAttribute('class')
  document.documentElement.removeAttribute('style')
  server.resetHandlers()
})

afterAll(() => server.close())
