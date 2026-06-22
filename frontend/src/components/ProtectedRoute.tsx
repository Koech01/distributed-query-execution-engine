import { Navigate, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'

import { useAuth } from '@/hooks/use-auth'
import { sanitizeReturnTo } from '@/lib/auth'

interface ProtectedRouteProps {
  children: ReactNode
}

export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const location = useLocation()
  const { canRead, isAuthEnabled, isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <main aria-busy="true" aria-live="polite" className="flex min-h-svh items-center justify-center p-4">
        <p className="text-sm text-muted-foreground">Checking authentication…</p>
      </main>
    )
  }

  if (!isAuthEnabled) {
    return <>{children}</>
  }

  if (!isAuthenticated || !canRead) {
    const returnTo = sanitizeReturnTo(`${location.pathname}${location.search}`)
    return <Navigate to={`/login/?returnTo=${encodeURIComponent(returnTo)}`} replace />
  }

  return <>{children}</>
}
