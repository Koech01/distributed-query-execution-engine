import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'

import type { BackendOAuthProvider } from '@/components/types'

import { AuthContext, type AuthContextValue } from '@/hooks/auth-context'
import {
  clearSession,
  createDevBypassUser,
  initiateBackendOAuthLogin,
  isAuthEnabled,
  isSessionAuthenticated,
  profileToAuthUser,
  setSessionUser,
  userHasScope,
} from '@/lib/auth'
import { accountApi, authApi } from '@/lib/api'

export function AuthProvider({ children }: { children: ReactNode }) {
  const authEnabled = isAuthEnabled()
  const [user, setUser] = useState(() => (authEnabled ? null : createDevBypassUser()))
  const [isLoading, setIsLoading] = useState(authEnabled)

  const establishSession = useCallback(async (signal?: AbortSignal) => {
    const profile = await accountApi.getProfile(signal, { redirectOnUnauthorized: false })
    const authUser = profileToAuthUser(profile)
    setSessionUser(authUser)
    setUser(authUser)
    return authUser
  }, [])

  useEffect(() => {
    if (!authEnabled) {
      setUser(createDevBypassUser())
      setIsLoading(false)
      return
    }

    const controller = new AbortController()

    void establishSession(controller.signal)
      .catch(() => {
        clearSession()
        setUser(null)
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false)
        }
      })

    return () => {
      controller.abort()
    }
  }, [authEnabled, establishSession])

  useEffect(() => {
    const handleLogout = () => {
      clearSession()
      setUser(null)
    }

    window.addEventListener('dqee:logout', handleLogout)
    return () => window.removeEventListener('dqee:logout', handleLogout)
  }, [])

  const completeSignIn = useCallback(async () => {
    await establishSession()
  }, [establishSession])

  const loginWithEmail = useCallback(
    async (email: string, password: string) => {
      await authApi.login({ email, password })
      await establishSession()
    },
    [establishSession],
  )

  const registerWithEmail = useCallback(
    async (email: string, password: string, displayName: string) => {
      await authApi.register({ email, password, displayName })
      await establishSession()
    },
    [establishSession],
  )

  const loginWithOAuth = useCallback((provider: BackendOAuthProvider, returnTo?: string) => {
    initiateBackendOAuthLogin(provider, returnTo)
  }, [])

  const logout = useCallback(() => {
    clearSession()
    setUser(null)
    window.dispatchEvent(new CustomEvent('dqee:logout'))
  }, [])

  const checkScope = useCallback(
    (scope: string) => {
      if (!authEnabled) {
        return true
      }

      return userHasScope(user, scope)
    },
    [authEnabled, user],
  )

  const canRead = authEnabled ? Boolean(user && isSessionAuthenticated() && checkScope('query:read')) : true
  const canAdmin = authEnabled ? Boolean(user && isSessionAuthenticated() && checkScope('query:admin')) : true

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: authEnabled ? Boolean(user && isSessionAuthenticated()) : true,
      isAuthEnabled: authEnabled,
      isLoading,
      loginWithEmail,
      registerWithEmail,
      loginWithOAuth,
      logout,
      completeSignIn,
      hasScope: checkScope,
      canRead,
      canAdmin,
    }),
    [authEnabled, canAdmin, canRead, checkScope, completeSignIn, isLoading, loginWithEmail, loginWithOAuth, logout, registerWithEmail, user],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
