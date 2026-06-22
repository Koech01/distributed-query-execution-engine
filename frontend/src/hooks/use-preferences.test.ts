import { act, renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { DEFAULT_USER_PREFERENCES } from '@/components/types/preferences'

import {
  USER_PREFERENCES_STORAGE_KEY,
  clearStoredUserPreferences,
  readStoredUserPreferences,
  usePreferences,
  writeStoredUserPreferences,
} from './use-preferences'

describe('user preferences storage', () => {
  it('returns defaults when nothing is stored', () => {
    expect(readStoredUserPreferences()).toEqual(DEFAULT_USER_PREFERENCES)
  })

  it('persists and reads validated preferences from localStorage', () => {
    writeStoredUserPreferences({
      defaultTimeoutSeconds: 45,
      defaultFailurePolicy: 'StrictAll',
      defaultAsync: true,
      saveSqlInHistory: true,
    })

    expect(window.localStorage.getItem(USER_PREFERENCES_STORAGE_KEY)).toBeTruthy()
    expect(readStoredUserPreferences()).toMatchObject({
      defaultTimeoutSeconds: 45,
      defaultFailurePolicy: 'StrictAll',
      defaultAsync: true,
      saveSqlInHistory: true,
    })
  })

  it('falls back to defaults for invalid stored JSON', () => {
    window.localStorage.setItem(USER_PREFERENCES_STORAGE_KEY, '{"defaultTimeoutSeconds":"invalid"}')

    expect(readStoredUserPreferences()).toEqual(DEFAULT_USER_PREFERENCES)
  })

  it('clears stored preferences', () => {
    writeStoredUserPreferences({
      defaultTimeoutSeconds: 60,
      defaultFailurePolicy: 'BestEffort',
      defaultAsync: false,
      saveSqlInHistory: false,
    })

    clearStoredUserPreferences()

    expect(window.localStorage.getItem(USER_PREFERENCES_STORAGE_KEY)).toBeNull()
    expect(readStoredUserPreferences()).toEqual(DEFAULT_USER_PREFERENCES)
  })

  it('loads and saves preferences through the hook', async () => {
    writeStoredUserPreferences({
      defaultTimeoutSeconds: 55,
      defaultFailurePolicy: 'BestEffort',
      defaultAsync: true,
      saveSqlInHistory: true,
    })

    const { result } = renderHook(() => usePreferences())

    await waitFor(() => expect(result.current.isLoading).toBe(false))

    expect(result.current.preferences).toMatchObject({
      defaultTimeoutSeconds: 55,
      defaultAsync: true,
      saveSqlInHistory: true,
    })

    await act(async () => {
      await result.current.savePreferences({
        defaultTimeoutSeconds: 90,
        defaultFailurePolicy: 'StrictAll',
        defaultAsync: false,
        saveSqlInHistory: false,
      })
    })

    expect(readStoredUserPreferences()).toMatchObject({
      defaultTimeoutSeconds: 90,
      defaultFailurePolicy: 'StrictAll',
      defaultAsync: false,
      saveSqlInHistory: false,
    })
  })
})
