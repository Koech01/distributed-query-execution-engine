import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { mockQueryId, mockQueryResult } from '@/test/mocks/handlers'
import { queryApi } from '@/lib/api'
import { useQueryPoll } from './use-query-poll'

vi.mock('@/lib/api', () => ({
  queryApi: {
    getStatus: vi.fn(),
    getResult: vi.fn(),
  },
}))

const mockedQueryApi = vi.mocked(queryApi)
const hiddenDescriptor = Object.getOwnPropertyDescriptor(Document.prototype, 'hidden')

function setDocumentHidden(value: boolean) {
  Object.defineProperty(document, 'hidden', {
    configurable: true,
    value,
  })
}

describe('useQueryPoll', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-06-15T08:00:00.000Z'))
    setDocumentHidden(false)
    mockedQueryApi.getStatus.mockReset()
    mockedQueryApi.getResult.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
    Reflect.deleteProperty(document, 'hidden')
    if (hiddenDescriptor) {
      Object.defineProperty(Document.prototype, 'hidden', hiddenDescriptor)
    }
  })

  it('polls with bounded backoff and fetches the terminal result', async () => {
    mockedQueryApi.getStatus
      .mockResolvedValueOnce({ queryId: mockQueryId, status: 'running', message: null })
      .mockResolvedValueOnce({ queryId: mockQueryId, status: 'running', message: null })
      .mockResolvedValueOnce({ queryId: mockQueryId, status: 'completed', message: 'Ready' })
    mockedQueryApi.getResult.mockResolvedValueOnce(mockQueryResult)

    const { result } = renderHook(() => useQueryPoll({ queryId: mockQueryId }))

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(mockedQueryApi.getStatus).toHaveBeenCalledTimes(1)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(999)
    })
    expect(mockedQueryApi.getStatus).toHaveBeenCalledTimes(1)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1)
    })
    expect(mockedQueryApi.getStatus).toHaveBeenCalledTimes(2)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(2_000)
    })

    expect(result.current.phase).toBe('completed')
    expect(mockedQueryApi.getStatus).toHaveBeenCalledTimes(3)
    expect(mockedQueryApi.getResult).toHaveBeenCalledWith(mockQueryId, expect.any(AbortSignal))
    expect(result.current.result).toEqual(mockQueryResult)
  })

  it('cancels scheduled polling and ignores later timers', async () => {
    mockedQueryApi.getStatus.mockResolvedValue({ queryId: mockQueryId, status: 'running', message: null })

    const { result } = renderHook(() => useQueryPoll({ queryId: mockQueryId }))

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(mockedQueryApi.getStatus).toHaveBeenCalledTimes(1)

    act(() => {
      result.current.cancel()
    })

    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000)
    })

    expect(result.current.phase).toBe('cancelled')
    expect(mockedQueryApi.getStatus).toHaveBeenCalledTimes(1)
    expect(mockedQueryApi.getResult).not.toHaveBeenCalled()
  })

  it('pauses while the document is hidden and resumes when visible', async () => {
    mockedQueryApi.getStatus.mockResolvedValue({ queryId: mockQueryId, status: 'running', message: null })

    const { result } = renderHook(() => useQueryPoll({ queryId: mockQueryId }))

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(mockedQueryApi.getStatus).toHaveBeenCalledTimes(1)

    act(() => {
      setDocumentHidden(true)
      document.dispatchEvent(new Event('visibilitychange'))
    })

    await act(async () => {
      await vi.advanceTimersByTimeAsync(10_000)
    })

    expect(result.current.phase).toBe('paused')
    expect(mockedQueryApi.getStatus).toHaveBeenCalledTimes(1)

    act(() => {
      setDocumentHidden(false)
      document.dispatchEvent(new Event('visibilitychange'))
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })

    expect(mockedQueryApi.getStatus).toHaveBeenCalledTimes(2)
  })
})
