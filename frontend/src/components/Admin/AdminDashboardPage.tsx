import { useCallback, useEffect, useState } from 'react'
import { Activity, Database, HardDrive, RefreshCw, Server, Trash2 } from 'lucide-react'
import { Link } from 'react-router-dom'

import type { AdminDashboardStats } from '@/components/types/admin'
import { ActiveQueriesPanel } from '@/components/Admin/ActiveQueriesPanel'
import { AdminDashboardPageSkeleton } from '@/components/Admin/AdminDashboardPageSkeleton'
import { WorkerHealthPanel } from '@/components/Admin/WorkerHealthPanel'
import { Button } from '@/components/ui/button'
import { ErrorAlert } from '@/components/ui/error-alert'
import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { StatTile } from '@/components/ui/stat-tile'
import { adminApi } from '@/lib/api'
import { formatRelativeTime, formatTimestamp } from '@/lib/date'

const REFRESH_INTERVAL_MS = 30_000

export function AdminDashboardPage() {
  const [stats, setStats] = useState<AdminDashboardStats | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [isRefreshing, setIsRefreshing] = useState(false)

  const loadStats = useCallback(async (signal?: AbortSignal, showRefreshing = false) => {
    if (showRefreshing) {
      setIsRefreshing(true)
    }

    try {
      const nextStats = await adminApi.getStats(signal)
      setStats(nextStats)
      setError(null)
    } catch (loadError) {
      if (signal?.aborted) {
        return
      }

      setError(loadError)
    } finally {
      setIsRefreshing(false)
    }
  }, [])

  useEffect(() => {
    let isMounted = true
    const controller = new AbortController()

    const loadInitialStats = async () => {
      try {
        const nextStats = await adminApi.getStats(controller.signal)
        if (!isMounted) {
          return
        }

        setStats(nextStats)
        setError(null)
      } catch (loadError) {
        if (controller.signal.aborted || !isMounted) {
          return
        }

        setError(loadError)
      }
    }

    void loadInitialStats()

    const intervalId = window.setInterval(() => {
      void loadStats(controller.signal, true)
    }, REFRESH_INTERVAL_MS)

    return () => {
      isMounted = false
      controller.abort()
      window.clearInterval(intervalId)
    }
  }, [loadStats])

  if (!stats && !error) {
    return (
      <main aria-labelledby="admin-dashboard-title">
        <AdminDashboardPageSkeleton />
      </main>
    )
  }

  return (
    <main aria-labelledby="admin-dashboard-title" className="mx-auto flex w-full max-w-6xl flex-col gap-8">
      <PageHeader
        title="Administration"
        titleId="admin-dashboard-title"
        description="Cluster overview, active query control, worker health, and cache operations for operators with query:admin scope."
        badge="Admin"
        actions={
          <Button
            type="button"
            variant="outline"
            onClick={() => void loadStats(undefined, true)}
            aria-busy={isRefreshing}
            disabled={isRefreshing}
          >
            <RefreshCw className={isRefreshing ? 'size-4 animate-spin' : 'size-4'} aria-hidden="true" />
            {isRefreshing ? 'Refreshing…' : 'Refresh overview'}
          </Button>
        }
      />

      {error ? <ErrorAlert error={error} title="Unable to load admin overview" /> : null}

      {stats ? (
        <>
          <section aria-labelledby="admin-stats-heading" className="space-y-4">
            <div className="space-y-1">
              <h2 id="admin-stats-heading" className="text-xl font-semibold tracking-tight">
                Cluster overview
              </h2>
              <p className="text-sm text-muted-foreground">
                Snapshot generated {formatRelativeTime(stats.generatedAt)} ({formatTimestamp(stats.generatedAt)}).
              </p>
            </div>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <StatTile label="Active queries" icon={Database} hint="Queries currently executing across the cluster.">
                <p className="text-2xl font-semibold tabular-nums">{stats.activeQueries.toLocaleString()}</p>
              </StatTile>
              <StatTile
                label="Healthy workers"
                icon={Server}
                hint={`${stats.healthyWorkers} of ${stats.totalWorkers} workers reporting healthy probes.`}
              >
                <p className="text-2xl font-semibold tabular-nums">
                  {stats.healthyWorkers.toLocaleString()}
                  <span className="text-base font-normal text-muted-foreground"> / {stats.totalWorkers.toLocaleString()}</span>
                </p>
              </StatTile>
              <StatTile label="Plan cache entries" icon={HardDrive} hint="Cached query plans retained by the engine.">
                <p className="text-2xl font-semibold tabular-nums">{stats.planCacheEntries.toLocaleString()}</p>
              </StatTile>
              <StatTile label="Result cache entries" icon={Activity} hint="Cached query results available for reuse.">
                <p className="text-2xl font-semibold tabular-nums">{stats.resultCacheEntries.toLocaleString()}</p>
              </StatTile>
              <StatTile
                label="Async status entries"
                icon={RefreshCw}
                hint="Tracked async query status records awaiting completion or expiry."
              >
                <p className="text-2xl font-semibold tabular-nums">{stats.asyncQueryStatusEntries.toLocaleString()}</p>
              </StatTile>
              <StatTile label="Cache management" icon={Trash2} hint="Flush plan cache entries by scope when needed.">
                <Button asChild variant="outline" size="sm" className="w-full justify-start">
                  <Link to="/admin/cache">Open cache management</Link>
                </Button>
              </StatTile>
            </div>
          </section>

          <ActiveQueriesPanel />
          <WorkerHealthPanel />

          <PageSection
            title="Operator shortcuts"
            titleId="admin-shortcuts-heading"
            icon={Server}
            description="Jump to focused admin workflows without leaving the authenticated shell."
          >
            <div className="flex flex-wrap gap-3">
              <Button asChild variant="outline">
                <Link to="/admin/cache">Manage plan cache</Link>
              </Button>
              <Button asChild variant="outline">
                <Link to="/operations">View API health</Link>
              </Button>
            </div>
          </PageSection>
        </>
      ) : null}
    </main>
  )
}
