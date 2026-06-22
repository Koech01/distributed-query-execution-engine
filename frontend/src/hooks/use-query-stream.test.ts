import { act, renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { useQueryStream } from '@/hooks/use-query-stream'
import { mockQueryId, mockQueryResult } from '@/test/mocks/handlers'

describe('useQueryStream', () => {
  it('accumulates rows incrementally and returns the final result', async () => {
    const { result } = renderHook(() => useQueryStream())

    let finalResult: Awaited<ReturnType<typeof result.current.start>> = null
    await act(async () => {
      finalResult = await result.current.start({
        sql: 'SELECT * FROM Orders',
        parameters: [],
      })
    })

    expect(finalResult).toMatchObject({
      queryId: mockQueryId,
      rowCount: mockQueryResult.rowCount,
    })
    expect(result.current.phase).toBe('complete')
    expect(result.current.rows).toHaveLength(mockQueryResult.rows.length)
    expect(result.current.columns).toEqual(mockQueryResult.columns)
  })
})
