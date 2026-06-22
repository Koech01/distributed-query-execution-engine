import { Navigate, Route, Routes } from 'react-router-dom'

import { AdminDashboardPage } from '@/components/Admin/AdminDashboardPage'
import { CacheManagementPage } from '@/components/Admin/CacheManagementPage'
import { RequireAdmin } from '@/components/Admin/RequireAdmin'
import { AuthCallbackPage } from '@/components/Auth/AuthCallbackPage'
import { LoginPage } from '@/components/Auth/LoginPage'
import { SignupPage } from '@/components/Auth/SignupPage'
import { UnauthorizedPage } from '@/components/Auth/UnauthorizedPage'
import { AuthenticatedLayout } from '@/components/Home/page'
import { NotFoundPage } from '@/components/NotFound'
import { OperationsPage } from '@/components/Operations/OperationsPage'
import { ProtectedRoute } from '@/components/ProtectedRoute'
import { QueryConsolePage } from '@/components/QueryConsole/QueryConsolePage'
import { QueryResultPage } from '@/components/QueryConsole/QueryResultPage'
import { QueryHistoryPage } from '@/components/QueryHistory/QueryHistoryPage'
import { SettingsPage } from '@/components/Settings/SettingsPage'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login/" element={<LoginPage />} />
      <Route path="/signup/" element={<SignupPage />} />
      <Route path="/auth/callback" element={<AuthCallbackPage />} />
      <Route path="/unauthorized" element={<UnauthorizedPage />} />
      <Route
        element={
          <ProtectedRoute>
            <AuthenticatedLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="/query" replace />} />
        <Route path="query" element={<QueryConsolePage />} />
        <Route path="query/:queryId" element={<QueryResultPage />} />
        <Route path="history" element={<QueryHistoryPage />} />
        <Route path="operations" element={<OperationsPage />} />
        <Route path="settings" element={<SettingsPage />} />
        <Route path="admin" element={<RequireAdmin />}>
          <Route index element={<AdminDashboardPage />} />
          <Route path="cache" element={<CacheManagementPage />} />
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Route>
      <Route path="*" element={<NotFoundPage standalone />} />
    </Routes>
  )
}
