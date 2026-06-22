import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import * as scroll from '@/lib/scroll'
import { useQueryExecutionScroll } from './use-query-execution-scroll'

describe('useQueryExecutionScroll', () => {
  beforeEach(() => {
    vi.spyOn(scroll, 'scrollElementIntoView').mockImplementation(() => undefined)
    vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({ matches: false }))
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('scrolls to the results section once per result key', async () => {
    const { result } = renderHook(() => useQueryExecutionScroll())

    const resultsSection = document.createElement('section')
    result.current.resultsRef.current = resultsSection

    await act(async () => {
      result.current.scrollToTarget('results', 'query-1')
      await new Promise<void>((resolve) => {
        requestAnimationFrame(() => resolve())
      })
    })

    expect(scroll.scrollElementIntoView).toHaveBeenCalledTimes(1)
    expect(scroll.scrollElementIntoView).toHaveBeenCalledWith(resultsSection, { focus: true })

    await act(async () => {
      result.current.scrollToTarget('results', 'query-1')
      await new Promise<void>((resolve) => {
        requestAnimationFrame(() => resolve())
      })
    })

    expect(scroll.scrollElementIntoView).toHaveBeenCalledTimes(1)

    await act(async () => {
      result.current.resetScrollMemory()
      result.current.scrollToTarget('results', 'query-2')
      await new Promise<void>((resolve) => {
        requestAnimationFrame(() => resolve())
      })
    })

    expect(scroll.scrollElementIntoView).toHaveBeenCalledTimes(2)
  })
})
