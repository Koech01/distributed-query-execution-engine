import { useCallback, useRef } from 'react'

import { scrollElementIntoView } from '@/lib/scroll'

export type QueryExecutionScrollTarget = 'feedback' | 'results' | 'streaming-results'

export function useQueryExecutionScroll() {
  const feedbackRef = useRef<HTMLElement | null>(null)
  const resultsRef = useRef<HTMLElement | null>(null)
  const streamingResultsRef = useRef<HTMLElement | null>(null)
  const lastScrollKeyRef = useRef<string | null>(null)

  const scrollToTarget = useCallback((target: QueryExecutionScrollTarget, key: string) => {
    const scrollKey = `${target}:${key}`
    if (lastScrollKeyRef.current === scrollKey) {
      return
    }

    lastScrollKeyRef.current = scrollKey

    const element =
      target === 'feedback'
        ? feedbackRef.current
        : target === 'results'
          ? resultsRef.current
          : streamingResultsRef.current

    requestAnimationFrame(() => {
      scrollElementIntoView(element, { focus: true })
    })
  }, [])

  const resetScrollMemory = useCallback(() => {
    lastScrollKeyRef.current = null
  }, [])

  return {
    feedbackRef,
    resultsRef,
    streamingResultsRef,
    scrollToTarget,
    resetScrollMemory,
  }
}
