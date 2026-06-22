import { useCallback, useEffect, useId, useState } from 'react'
import { HardDrive, RefreshCw, Trash2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { ZodError } from 'zod'

import type { AdminCacheStats } from '@/components/types/admin'
import { CacheManagementPageSkeleton } from '@/components/Admin/CacheManagementPageSkeleton'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { ErrorAlert } from '@/components/ui/error-alert'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { StatTile } from '@/components/ui/stat-tile'
import { adminApi } from '@/lib/api'
import { formatRelativeTime, formatTimestamp } from '@/lib/date'
import { adminCacheFlushRequestSchema } from '@/lib/schemas'

const REFRESH_INTERVAL_MS = 30_000

export function CacheManagementPage() {
  const planHashInputId = useId()
  const planHashErrorId = useId()
  const [stats, setStats] = useState<AdminCacheStats | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [planHash, setPlanHash] = useState('')
  const [planHashError, setPlanHashError] = useState<string | null>(null)
  const [isFlushAllOpen, setIsFlushAllOpen] = useState(false)
  const [isFlushingAll, setIsFlushingAll] = useState(false)
  const [isFlushingHash, setIsFlushingHash] = useState(false)

  const loadStats = useCallback(async (signal?: AbortSignal, showRefreshing = false) => {
    if (showRefreshing) {
      setIsRefreshing(true)
    }

    try {
      const nextStats = await adminApi.getCacheStats(signal)
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
        const nextStats = await adminApi.getCacheStats(controller.signal)
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

  const handleFlushAll = async () => {
    setIsFlushingAll(true)

    try {
      const result = await adminApi.flushCache({})
      toast.success('Plan cache flushed', {
        description: `Removed ${result.deletedPlanEntries.toLocaleString()} entries from ${result.scope}.`,
      })
      setIsFlushAllOpen(false)
      await loadStats(undefined, true)
    } catch (flushError) {
      toast.error('Flush failed', {
        description: flushError instanceof Error ? flushError.message : 'Unable to flush plan cache.',
      })
    } finally {
      setIsFlushingAll(false)
    }
  }

  const handleFlushByHash = async () => {
    setPlanHashError(null)

    try {
      const payload = adminCacheFlushRequestSchema.parse({ planHash: planHash.trim() || null })
      setIsFlushingHash(true)

      const result = await adminApi.flushCache(payload)
      toast.success('Plan hash flushed', {
        description: `Removed ${result.deletedPlanEntries.toLocaleString()} entries from ${result.scope}.`,
      })
      setPlanHash('')
      await loadStats(undefined, true)
    } catch (validationError) {
      if (validationError instanceof ZodError) {
        const message = validationError.issues[0]?.message ?? 'Enter a valid plan hash.'
        setPlanHashError(message)
        return
      }

      toast.error('Flush failed', {
        description: validationError instanceof Error ? validationError.message : 'Unable to flush plan cache.',
      })
    } finally {
      setIsFlushingHash(false)
    }
  }

  if (!stats && !error) {
    return (
      <main aria-labelledby="cache-management-title">
        <CacheManagementPageSkeleton />
      </main>
    )
  }

  return (
    <main aria-labelledby="cache-management-title" className="mx-auto flex w-full max-w-5xl flex-col gap-8">
      <PageHeader
        title="Cache management"
        titleId="cache-management-title"
        description="Flush distributed plan cache entries by scope. Result and async status caches are reported for visibility."
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
            {isRefreshing ? 'Refreshing…' : 'Refresh stats'}
          </Button>
        }
      />

      {error ? <ErrorAlert error={error} title="Unable to load cache statistics" /> : null}

      {stats ? (
        <>
          <section aria-labelledby="cache-stats-heading" className="space-y-4">
            <div className="space-y-1">
              <h2 id="cache-stats-heading" className="text-xl font-semibold tracking-tight">
                Cache utilization
              </h2>
              <p className="text-sm text-muted-foreground">
                Snapshot generated {formatRelativeTime(stats.generatedAt)} ({formatTimestamp(stats.generatedAt)}).
              </p>
            </div>
            <div className="grid gap-4 md:grid-cols-3">
              <StatTile label="Plan cache entries" icon={HardDrive}>
                <p className="text-2xl font-semibold tabular-nums">{stats.planCacheEntries.toLocaleString()}</p>
              </StatTile>
              <StatTile label="Result cache entries" icon={RefreshCw}>
                <p className="text-2xl font-semibold tabular-nums">{stats.resultCacheEntries.toLocaleString()}</p>
              </StatTile>
              <StatTile label="Async status entries" icon={Trash2}>
                <p className="text-2xl font-semibold tabular-nums">{stats.asyncQueryStatusEntries.toLocaleString()}</p>
              </StatTile>
            </div>
          </section>

          <PageSection
            title="Flush plan cache"
            titleId="cache-flush-heading"
            icon={Trash2}
            description="Flush all cached plans or target a single SHA-256 plan hash. Only plan cache entries are removed by these actions."
          >
            <div className="grid gap-6 lg:grid-cols-2">
              <div className="space-y-4 rounded-xl border border-border/60 bg-background/80 p-5 shadow-xs">
                <div className="space-y-1">
                  <h3 className="text-base font-semibold">Flush all plan cache</h3>
                  <p className="text-sm text-muted-foreground">
                    Removes every cached plan entry. Use when plans are stale after a deployment or schema change.
                  </p>
                </div>
                <Dialog open={isFlushAllOpen} onOpenChange={setIsFlushAllOpen}>
                  <DialogTrigger asChild>
                    <Button type="button" variant="destructive">
                      <Trash2 className="size-4" aria-hidden="true" />
                      Flush all plans
                    </Button>
                  </DialogTrigger>
                  <DialogContent aria-describedby="flush-all-description">
                    <DialogHeader>
                      <DialogTitle>Flush all plan cache entries?</DialogTitle>
                      <DialogDescription id="flush-all-description">
                        This removes all cached query plans. Running queries are unaffected, but subsequent executions may
                        need to rebuild plans.
                      </DialogDescription>
                    </DialogHeader>
                    <DialogFooter>
                      <DialogClose asChild>
                        <Button type="button" variant="outline">
                          Cancel
                        </Button>
                      </DialogClose>
                      <Button
                        type="button"
                        variant="destructive"
                        onClick={() => void handleFlushAll()}
                        aria-busy={isFlushingAll}
                        disabled={isFlushingAll}
                      >
                        {isFlushingAll ? 'Flushing…' : 'Confirm flush all'}
                      </Button>
                    </DialogFooter>
                  </DialogContent>
                </Dialog>
              </div>

              <div className="space-y-4 rounded-xl border border-border/60 bg-background/80 p-5 shadow-xs">
                <div className="space-y-1">
                  <h3 className="text-base font-semibold">Flush by plan hash</h3>
                  <p className="text-sm text-muted-foreground">
                    Target one SHA-256 plan hash (64 hexadecimal characters) to invalidate a specific cached plan.
                  </p>
                </div>
                <div className="space-y-2">
                  <Label htmlFor={planHashInputId}>Plan hash</Label>
                  <Input
                    id={planHashInputId}
                    value={planHash}
                    onChange={(event) => {
                      setPlanHash(event.target.value)
                      if (planHashError) {
                        setPlanHashError(null)
                      }
                    }}
                    placeholder="64-character hexadecimal SHA-256 value"
                    aria-required="true"
                    aria-invalid={planHashError ? true : undefined}
                    aria-describedby={planHashError ? planHashErrorId : undefined}
                    className="font-mono text-xs"
                    autoComplete="off"
                    spellCheck={false}
                  />
                  {planHashError ? (
                    <p id={planHashErrorId} role="alert" aria-live="polite" className="text-sm text-destructive">
                      {planHashError}
                    </p>
                  ) : null}
                </div>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => void handleFlushByHash()}
                  aria-busy={isFlushingHash}
                  disabled={isFlushingHash || planHash.trim().length === 0}
                >
                  {isFlushingHash ? 'Flushing…' : 'Flush plan hash'}
                </Button>
              </div>
            </div>

            <Alert>
              <HardDrive aria-hidden="true" />
              <AlertTitle>Scope note</AlertTitle>
              <AlertDescription>
                Cache flush actions only remove plan cache entries. Result cache and async status entries are listed for
                visibility but are not cleared by these admin endpoints.
              </AlertDescription>
            </Alert>
          </PageSection>
        </>
      ) : null}

      <div className="flex justify-start">
        <Button asChild variant="outline">
          <Link to="/admin">Back to admin overview</Link>
        </Button>
      </div>
    </main>
  )
}
