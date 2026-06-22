import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { prefersReducedMotion, scrollElementIntoView } from './scroll'

describe('scrollElementIntoView', () => {
  let matchMediaMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    matchMediaMock = vi.fn().mockReturnValue({ matches: false })
    vi.stubGlobal('matchMedia', matchMediaMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('no-ops when the target element is missing', () => {
    expect(() => scrollElementIntoView(null)).not.toThrow()
  })

  it('scrolls smoothly by default', () => {
    const element = document.createElement('section')
    element.scrollIntoView = vi.fn()

    scrollElementIntoView(element)

    expect(element.scrollIntoView).toHaveBeenCalledWith({
      behavior: 'smooth',
      block: 'start',
    })
  })

  it('uses instant scrolling when reduced motion is preferred', () => {
    matchMediaMock.mockReturnValue({ matches: true })
    const element = document.createElement('section')
    element.scrollIntoView = vi.fn()

    scrollElementIntoView(element)

    expect(element.scrollIntoView).toHaveBeenCalledWith({
      behavior: 'auto',
      block: 'start',
    })
    expect(prefersReducedMotion()).toBe(true)
  })

  it('focuses the designated scroll target without scrolling again', () => {
    const element = document.createElement('section')
    const heading = document.createElement('h2')
    heading.dataset.scrollFocus = 'true'
    element.append(heading)
    element.scrollIntoView = vi.fn()
    heading.focus = vi.fn()

    scrollElementIntoView(element, { focus: true })

    expect(heading.tabIndex).toBe(-1)
    expect(heading.focus).toHaveBeenCalledWith({ preventScroll: true })
  })
})
