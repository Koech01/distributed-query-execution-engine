import 'fake-indexeddb/auto'

import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'

import type { QueryResult } from '@/components/types'

import {
  addLocalQueryHistoryEntry,
  clearLocalQueryHistory,
  listLocalQueryHistory,
  useLocalQueryHistory,
} from './use-local-query-history'

const queryResult: Pick<QueryResult, 'queryId' | 'rowCount' | 'executionMs' | 'degraded'> = {
  queryId: '11111111-1111-4111-8111-111111111111',
  rowCount: 2,
  executionMs: 87,
  degraded: false,
}

async function deleteHistoryDb(): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const request = indexedDB.deleteDatabase('dqee-query-history')
    request.onsuccess = () => resolve()
    request.onerror = () => reject(request.error ?? new Error('Could not delete test database.'))
    request.onblocked = () => resolve()
  })
}

describe('local query history storage', () => {
  afterEach(async () => {
    await deleteHistoryDb()
  })

  it('adds, lists, and clears query history entries', async () => {
    await addLocalQueryHistoryEntry({
      result: queryResult,
      sql: 'SELECT * FROM Orders WHERE Id = @id',
      async: false,
      timestamp: new Date('2026-06-15T09:00:00.000Z'),
    })

    const entries = await listLocalQueryHistory()

    expect(entries).toHaveLength(1)
    expect(entries[0]).toMatchObject({
      queryId: queryResult.queryId,
      timestamp: '2026-06-15T09:00:00.000Z',
      rowCount: 2,
      executionMs: 87,
      degraded: false,
      async: false,
    })
    expect(entries[0]?.sqlHash).toBeTruthy()
    expect(entries[0]?.sql).toBeUndefined()

    await clearLocalQueryHistory()
    await expect(listLocalQueryHistory()).resolves.toEqual([])
  })

  it('stores SQL only when explicitly requested and never stores parameters or tokens', async () => {
    await addLocalQueryHistoryEntry({
      result: queryResult,
      sql: 'SELECT * FROM Orders WHERE Id = @id',
      async: true,
      saveSql: true,
      timestamp: new Date('2026-06-15T10:00:00.000Z'),
    })

    const [entry] = await listLocalQueryHistory()

    expect(entry?.sql).toBe('SELECT * FROM Orders WHERE Id = @id')
    expect(entry).not.toHaveProperty('parameters')
    expect(entry).not.toHaveProperty('parameterValues')
    expect(entry).not.toHaveProperty('token')
    expect(JSON.stringify(entry)).not.toContain('secret-token')
    expect(JSON.stringify(entry)).not.toContain('"@id":"42"')
  })
})

describe('useLocalQueryHistory', () => {
  afterEach(async () => {
    await deleteHistoryDb()
  })

  it('loads history and exposes CRUD operations through the hook', async () => {
    const { result } = renderHook(() => useLocalQueryHistory())

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.entries).toEqual([])

    await act(async () => {
      await result.current.addEntry({
        result: queryResult,
        sql: 'SELECT TOP 10 * FROM Orders',
        async: false,
        timestamp: new Date('2026-06-15T11:00:00.000Z'),
      })
    })

    await waitFor(() => expect(result.current.entries).toHaveLength(1))
    expect(result.current.entries[0]?.queryId).toBe(queryResult.queryId)
    expect(result.current.entries[0]?.sql).toBeUndefined()

    await act(async () => {
      await result.current.clearHistory()
    })

    expect(result.current.entries).toEqual([])
  })
})
