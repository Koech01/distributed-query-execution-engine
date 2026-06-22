import { Navigate, Outlet } from 'react-router-dom'

import { useAuth } from '@/hooks/use-auth'

export function RequireAdmin() {
  const { canAdmin, isAuthEnabled, isLoading } = useAuth()

  if (isLoading) {
    return (
      <main aria-busy="true" aria-live="polite" className="space-y-3">
        <p className="text-sm text-muted-foreground">Checking administrator access…</p>
      </main>
    )
  }

  if (isAuthEnabled && !canAdmin) {
    return <Navigate to="/query" replace />
  }

  return <Outlet />
}
