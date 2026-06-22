export interface ScrollIntoViewOptions {
  block?: ScrollLogicalPosition
  focus?: boolean
}

export function prefersReducedMotion(): boolean {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return false
  }

  return window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

export function scrollElementIntoView(
  element: HTMLElement | null | undefined,
  { block = 'start', focus = false }: ScrollIntoViewOptions = {},
): void {
  if (!element) {
    return
  }

  element.scrollIntoView({
    behavior: prefersReducedMotion() ? 'auto' : 'smooth',
    block,
  })

  if (!focus) {
    return
  }

  const focusTarget =
    element.querySelector<HTMLElement>('[data-scroll-focus]') ??
    (element.matches('[data-scroll-focus]') ? element : null)

  if (!focusTarget) {
    return
  }

  if (!focusTarget.hasAttribute('tabindex')) {
    focusTarget.tabIndex = -1
  }

  focusTarget.focus({ preventScroll: true })
}
