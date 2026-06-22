import { useCallback, useEffect, useState } from 'react'

import {
  DEFAULT_USER_PREFERENCES,
  type UserPreferences,
} from '@/components/types/preferences'
import { validateStoredUserPreferences } from '@/lib/schemas'

export const USER_PREFERENCES_STORAGE_KEY = 'dqee-user-preferences'

export type StoredUserPreferences = Pick<
  UserPreferences,
  'defaultTimeoutSeconds' | 'defaultFailurePolicy' | 'defaultAsync' | 'saveSqlInHistory'
>

interface UsePreferencesState {
  preferences: UserPreferences
  isLoading: boolean
  error: Error | null
  savePreferences: (updates: StoredUserPreferences) => Promise<void>
  resetPreferences: () => Promise<void>
}

function isLocalStorageAvailable(): boolean {
  try {
    const probeKey = '__dqee_preferences_probe__'
    window.localStorage.setItem(probeKey, '1')
    window.localStorage.removeItem(probeKey)
    return true
  } catch {
    return false
  }
}

export function readStoredUserPreferences(): UserPreferences {
  if (!isLocalStorageAvailable()) {
    return DEFAULT_USER_PREFERENCES
  }

  const raw = window.localStorage.getItem(USER_PREFERENCES_STORAGE_KEY)

  if (!raw) {
    return DEFAULT_USER_PREFERENCES
  }

  try {
    const parsed = validateStoredUserPreferences(JSON.parse(raw))
    return {
      ...DEFAULT_USER_PREFERENCES,
      ...parsed,
    }
  } catch {
    return DEFAULT_USER_PREFERENCES
  }
}

export function writeStoredUserPreferences(preferences: StoredUserPreferences): void {
  if (!isLocalStorageAvailable()) {
    throw new Error('Local storage is unavailable in this browser context.')
  }

  const validated = validateStoredUserPreferences(preferences)
  window.localStorage.setItem(USER_PREFERENCES_STORAGE_KEY, JSON.stringify(validated))
}

export function clearStoredUserPreferences(): void {
  if (!isLocalStorageAvailable()) {
    throw new Error('Local storage is unavailable in this browser context.')
  }

  window.localStorage.removeItem(USER_PREFERENCES_STORAGE_KEY)
}

export function usePreferences(): UsePreferencesState {
  const [preferences, setPreferences] = useState<UserPreferences>(DEFAULT_USER_PREFERENCES)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)

  useEffect(() => {
    let isMounted = true

    const loadPreferences = () => {
      try {
        const nextPreferences = readStoredUserPreferences()

        if (!isMounted) {
          return
        }

        setPreferences(nextPreferences)
        setError(null)
      } catch (loadError) {
        if (!isMounted) {
          return
        }

        setPreferences(DEFAULT_USER_PREFERENCES)
        setError(loadError instanceof Error ? loadError : new Error('Could not load user preferences.'))
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadPreferences()

    return () => {
      isMounted = false
    }
  }, [])

  const savePreferences = useCallback(async (updates: StoredUserPreferences) => {
    try {
      writeStoredUserPreferences(updates)
      setPreferences({
        ...DEFAULT_USER_PREFERENCES,
        ...updates,
      })
      setError(null)
    } catch (saveError) {
      const nextError = saveError instanceof Error ? saveError : new Error('Could not save user preferences.')
      setError(nextError)
      throw nextError
    }
  }, [])

  const resetPreferences = useCallback(async () => {
    try {
      clearStoredUserPreferences()
      setPreferences(DEFAULT_USER_PREFERENCES)
      setError(null)
    } catch (resetError) {
      const nextError = resetError instanceof Error ? resetError : new Error('Could not reset user preferences.')
      setError(nextError)
      throw nextError
    }
  }, [])

  return {
    preferences,
    isLoading,
    error,
    savePreferences,
    resetPreferences,
  }
}
