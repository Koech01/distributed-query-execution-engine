import { createContext } from 'react'

import type { AuthState, AuthUser, BackendOAuthProvider } from '@/components/types'

export interface AuthContextValue extends AuthState {
  isLoading: boolean
  loginWithEmail: (email: string, password: string) => Promise<void>
  registerWithEmail: (email: string, password: string, displayName: string) => Promise<void>
  loginWithOAuth: (provider: BackendOAuthProvider, returnTo?: string) => void
  logout: () => void
  completeSignIn: () => Promise<void>
  hasScope: (scope: string) => boolean
  canRead: boolean
  canAdmin: boolean
}

export interface AuthUserState {
  user: AuthUser | null
  setUser: (user: AuthUser | null) => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
