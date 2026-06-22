import { useCallback, useEffect, useState } from 'react'

import type { ChangePasswordRequest, UpdateProfileRequest, UserProfile } from '@/components/types'
import { useAuth } from '@/hooks/use-auth'
import { captureError } from '@/lib/observability'
import { accountApi } from '@/lib/api'

interface UseAccountProfileState {
  profile: UserProfile | null
  isLoading: boolean
  isRefreshing: boolean
  error: unknown
  isAvailable: boolean
  refreshProfile: () => Promise<void>
  updateProfile: (request: UpdateProfileRequest) => Promise<UserProfile>
  changePassword: (request: ChangePasswordRequest) => Promise<void>
  deleteAccount: () => Promise<void>
}

export function useAccountProfile(): UseAccountProfileState {
  const { completeSignIn, isAuthEnabled, isAuthenticated, logout } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const isAvailable = isAuthEnabled && isAuthenticated

  const loadProfile = useCallback(
    async (signal?: AbortSignal, options?: { background?: boolean }) => {
      if (!isAvailable) {
        setProfile(null)
        setError(null)
        setIsLoading(false)
        setIsRefreshing(false)
        return
      }

      if (options?.background) {
        setIsRefreshing(true)
      } else {
        setIsLoading(true)
      }

      setError(null)

      try {
        const nextProfile = await accountApi.getProfile(signal)
        if (signal?.aborted) {
          return
        }

        setProfile(nextProfile)
      } catch (loadError) {
        if (signal?.aborted) {
          return
        }

        setError(loadError)
        captureError(loadError, { route: '/settings' })
      } finally {
        if (!signal?.aborted) {
          setIsLoading(false)
          setIsRefreshing(false)
        }
      }
    },
    [isAvailable],
  )

  useEffect(() => {
    let isMounted = true
    const controller = new AbortController()

    const load = async () => {
      if (!isAvailable) {
        if (!isMounted) {
          return
        }

        setProfile(null)
        setError(null)
        setIsLoading(false)
        setIsRefreshing(false)
        return
      }

      setIsLoading(true)
      setError(null)

      try {
        const nextProfile = await accountApi.getProfile(controller.signal)
        if (!isMounted || controller.signal.aborted) {
          return
        }

        setProfile(nextProfile)
      } catch (loadError) {
        if (!isMounted || controller.signal.aborted) {
          return
        }

        setError(loadError)
        captureError(loadError, { route: '/settings' })
      } finally {
        if (isMounted && !controller.signal.aborted) {
          setIsLoading(false)
          setIsRefreshing(false)
        }
      }
    }

    void load()

    return () => {
      isMounted = false
      controller.abort()
    }
  }, [isAvailable])

  const refreshProfile = useCallback(async () => {
    await loadProfile(undefined, { background: true })
  }, [loadProfile])

  const updateProfile = useCallback(
    async (request: UpdateProfileRequest) => {
      const response = await accountApi.updateProfile(request)
      setProfile(response.profile)

      if (response.token) {
        await completeSignIn()
      }

      return response.profile
    },
    [completeSignIn],
  )

  const changePassword = useCallback(
    async (request: ChangePasswordRequest) => {
      await accountApi.changePassword(request)
      await completeSignIn()
    },
    [completeSignIn],
  )

  const deleteAccount = useCallback(async () => {
    await accountApi.deleteAccount()
    logout()
  }, [logout])

  return {
    profile,
    isLoading,
    isRefreshing,
    error,
    isAvailable,
    refreshProfile,
    updateProfile,
    changePassword,
    deleteAccount,
  }
}
