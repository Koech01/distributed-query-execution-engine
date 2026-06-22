import { useCallback, useEffect, useState } from 'react'

import type { QueryResult } from '@/components/types'

const DB_NAME = 'dqee-query-history'
const DB_VERSION = 1
const STORE_NAME = 'query-history'
const MAX_HISTORY_ENTRIES = 100

export interface LocalQueryHistoryEntry {
  queryId: string
  sqlHash: string
  sql?: string
  timestamp: string
  rowCount: number
  executionMs: number
  degraded: boolean
  async: boolean
}

export interface AddLocalQueryHistoryEntryInput {
  result: Pick<QueryResult, 'queryId' | 'rowCount' | 'executionMs' | 'degraded'>
  sql: string
  async: boolean
  saveSql?: boolean
  timestamp?: Date
}

interface LocalQueryHistoryState {
  entries: LocalQueryHistoryEntry[]
  isLoading: boolean
  error: Error | null
  addEntry: (input: AddLocalQueryHistoryEntryInput) => Promise<void>
  clearHistory: () => Promise<void>
  refresh: () => Promise<void>
}

function isIndexedDbAvailable(): boolean {
  return typeof globalThis.indexedDB !== 'undefined'
}

function requestToPromise<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error ?? new Error('IndexedDB request failed.'))
  })
}

function transactionDone(transaction: IDBTransaction): Promise<void> {
  return new Promise((resolve, reject) => {
    transaction.oncomplete = () => resolve()
    transaction.onabort = () => reject(transaction.error ?? new Error('IndexedDB transaction aborted.'))
    transaction.onerror = () => reject(transaction.error ?? new Error('IndexedDB transaction failed.'))
  })
}

async function openHistoryDb(): Promise<IDBDatabase> {
  if (!isIndexedDbAvailable()) {
    throw new Error('IndexedDB is not available in this browser context.')
  }

  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION)

    request.onupgradeneeded = () => {
      const db = request.result
      const store = db.objectStoreNames.contains(STORE_NAME)
        ? request.transaction?.objectStore(STORE_NAME)
        : db.createObjectStore(STORE_NAME, { keyPath: 'queryId' })

      if (store && !store.indexNames.contains('timestamp')) {
        store.createIndex('timestamp', 'timestamp')
      }
    }

    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error ?? new Error('Could not open query history database.'))
    request.onblocked = () => reject(new Error('Query history database upgrade is blocked by another tab.'))
  })
}

function closeDb(db: IDBDatabase): void {
  db.close()
}

async function sha256Hex(value: string): Promise<string> {
  const subtle = globalThis.crypto?.subtle

  if (!subtle) {
    let hash = 0
    for (let index = 0; index < value.length; index += 1) {
      hash = Math.imul(31, hash) + value.charCodeAt(index)
    }
    return `fallback-${(hash >>> 0).toString(16).padStart(8, '0')}`
  }

  const digest = await subtle.digest('SHA-256', new TextEncoder().encode(value))
  return Array.from(new Uint8Array(digest))
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('')
}

async function trimHistory(db: IDBDatabase): Promise<void> {
  const transaction = db.transaction(STORE_NAME, 'readwrite')
  const store = transaction.objectStore(STORE_NAME)
  const entries = (await requestToPromise(store.getAll())) as LocalQueryHistoryEntry[]
  const entriesToDelete = entries
    .sort((left, right) => new Date(right.timestamp).getTime() - new Date(left.timestamp).getTime())
    .slice(MAX_HISTORY_ENTRIES)

  for (const entry of entriesToDelete) {
    store.delete(entry.queryId)
  }

  await transactionDone(transaction)
}

export async function listLocalQueryHistory(): Promise<LocalQueryHistoryEntry[]> {
  const db = await openHistoryDb()

  try {
    const transaction = db.transaction(STORE_NAME, 'readonly')
    const store = transaction.objectStore(STORE_NAME)
    const entries = (await requestToPromise(store.getAll())) as LocalQueryHistoryEntry[]
    await transactionDone(transaction)

    return entries.sort((left, right) => new Date(right.timestamp).getTime() - new Date(left.timestamp).getTime())
  } finally {
    closeDb(db)
  }
}

export async function addLocalQueryHistoryEntry(input: AddLocalQueryHistoryEntryInput): Promise<LocalQueryHistoryEntry> {
  const db = await openHistoryDb()
  const entry: LocalQueryHistoryEntry = {
    queryId: input.result.queryId,
    sqlHash: await sha256Hex(input.sql),
    ...(input.saveSql ? { sql: input.sql } : {}),
    timestamp: (input.timestamp ?? new Date()).toISOString(),
    rowCount: input.result.rowCount,
    executionMs: input.result.executionMs,
    degraded: input.result.degraded,
    async: input.async,
  }

  try {
    const transaction = db.transaction(STORE_NAME, 'readwrite')
    transaction.objectStore(STORE_NAME).put(entry)
    await transactionDone(transaction)
    await trimHistory(db)
    return entry
  } finally {
    closeDb(db)
  }
}

export async function clearLocalQueryHistory(): Promise<void> {
  const db = await openHistoryDb()

  try {
    const transaction = db.transaction(STORE_NAME, 'readwrite')
    transaction.objectStore(STORE_NAME).clear()
    await transactionDone(transaction)
  } finally {
    closeDb(db)
  }
}

export function useLocalQueryHistory(): LocalQueryHistoryState {
  const [entries, setEntries] = useState<LocalQueryHistoryEntry[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)

  const refresh = useCallback(async () => {
    setIsLoading(true)
    try {
      const nextEntries = await listLocalQueryHistory()
      setEntries(nextEntries)
      setError(null)
    } catch (historyError) {
      setEntries([])
      setError(historyError instanceof Error ? historyError : new Error('Could not load query history.'))
    } finally {
      setIsLoading(false)
    }
  }, [])

  const addEntry = useCallback(
    async (input: AddLocalQueryHistoryEntryInput) => {
      try {
        await addLocalQueryHistoryEntry(input)
        await refresh()
      } catch (historyError) {
        setError(historyError instanceof Error ? historyError : new Error('Could not save query history.'))
      }
    },
    [refresh],
  )

  const clearHistory = useCallback(async () => {
    try {
      await clearLocalQueryHistory()
      setEntries([])
      setError(null)
    } catch (historyError) {
      setError(historyError instanceof Error ? historyError : new Error('Could not clear query history.'))
    }
  }, [])

  useEffect(() => {
    let isMounted = true

    const loadInitialHistory = async () => {
      try {
        const nextEntries = await listLocalQueryHistory()

        if (!isMounted) {
          return
        }

        setEntries(nextEntries)
        setError(null)
      } catch (historyError) {
        if (!isMounted) {
          return
        }

        setEntries([])
        setError(historyError instanceof Error ? historyError : new Error('Could not load query history.'))
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    void loadInitialHistory()

    return () => {
      isMounted = false
    }
  }, [])

  return {
    entries,
    isLoading,
    error,
    addEntry,
    clearHistory,
    refresh,
  }
}
