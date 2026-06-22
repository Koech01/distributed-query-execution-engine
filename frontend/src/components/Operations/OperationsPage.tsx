import { useEffect, useMemo, useState } from 'react'
import type { LucideIcon } from 'lucide-react'
import { Activity, AlertTriangle, ExternalLink, Gauge, HeartPulse, Network, RefreshCw, Route } from 'lucide-react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { PageHeader } from '@/components/ui/page-header'
import { PageSection } from '@/components/ui/page-section'
import { StatTile } from '@/components/ui/stat-tile'
import { healthApi, getApiBaseUrl } from '@/lib/api'
import { AppError } from '@/lib/errors'
import { formatRelativeTime, formatTimestamp } from '@/lib/date'

import { HealthStatusCard, type HealthStatus } from './HealthStatusCard'
import { OperationsPageSkeleton } from './OperationsPageSkeleton'

const REFRESH_INTERVAL_MS = 30_000

interface HealthCheckState {
  status: HealthStatus
  responseStatus?: string
  error?: unknown
}

interface HealthSnapshot {
  live: HealthCheckState
  ready: HealthCheckState
  lastCheckedAt: Date
}

const initialHealthState: HealthCheckState = {
  status: 'loading',
}

function toHealthState(result: PromiseSettledResult<{ status: string }>): HealthCheckState {
  if (result.status === 'fulfilled') {
    return {
      status: 'available',
      responseStatus: result.value.status,
    }
  }

  const error = result.reason
  return {
    status: error instanceof AppError && error.status === 503 ? 'unavailable' : 'error',
    error,
  }
}

async function checkHealth(signal?: AbortSignal): Promise<HealthSnapshot> {
  const [live, ready] = await Promise.allSettled([healthApi.checkLive(signal), healthApi.checkReady(signal)])

  return {
    live: toHealthState(live),
    ready: toHealthState(ready),
    lastCheckedAt: new Date(),
  }
}

export function OperationsPage() {
  const [live, setLive] = useState<HealthCheckState>(initialHealthState)
  const [ready, setReady] = useState<HealthCheckState>(initialHealthState)
  const [lastCheckedAt, setLastCheckedAt] = useState<Date | null>(null)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const apiBaseUrl = getApiBaseUrl()
  const grafanaUrl = import.meta.env.VITE_GRAFANA_URL
  const jaegerUrl = import.meta.env.VITE_JAEGER_URL

  const configuredLinks = useMemo(
    () =>
      [
        grafanaUrl
          ? {
              title: 'Grafana',
              description: 'Open dashboards for service health, latency, and saturation signals.',
              href: grafanaUrl,
              icon: Gauge,
            }
          : null,
        jaegerUrl
          ? {
              title: 'Jaeger',
              description: 'Open distributed traces. Query IDs remain the user-facing correlation handle.',
              href: jaegerUrl,
              icon: Route,
            }
          : null,
      ].filter((link): link is NonNullable<typeof link> => Boolean(link)),
    [grafanaUrl, jaegerUrl],
  )

  useEffect(() => {
    let isMounted = true
    const controller = new AbortController()

    const loadInitialHealth = async () => {
      const snapshot = await checkHealth(controller.signal)

      if (!isMounted) {
        return
      }

      setLive(snapshot.live)
      setReady(snapshot.ready)
      setLastCheckedAt(snapshot.lastCheckedAt)
    }

    const intervalId = window.setInterval(() => {
      void checkHealth(controller.signal).then((snapshot) => {
        if (!isMounted) {
          return
        }

        setLive(snapshot.live)
        setReady(snapshot.ready)
        setLastCheckedAt(snapshot.lastCheckedAt)
      })
    }, REFRESH_INTERVAL_MS)

    void loadInitialHealth()

    return () => {
      isMounted = false
      controller.abort()
      window.clearInterval(intervalId)
    }
  }, [])

  const refreshNow = async () => {
    setIsRefreshing(true)

    try {
      const snapshot = await checkHealth()
      setLive(snapshot.live)
      setReady(snapshot.ready)
      setLastCheckedAt(snapshot.lastCheckedAt)
    } finally {
      setIsRefreshing(false)
    }
  }

  if (!lastCheckedAt) {
    return (
      <main aria-labelledby="operations-title" className="space-y-8">
        <OperationsPageSkeleton />
      </main>
    )
  }

  const unavailableCount = [live, ready].filter((state) => state.status === 'unavailable' || state.status === 'error').length

  return (
    <main aria-labelledby="operations-title" className="space-y-8">
      <PageHeader
        title="Operations"
        titleId="operations-title"
        description="Monitor API liveness, readiness, and configured observability destinations without storing credentials or querying backend auth endpoints."
        badge="Health & observability"
        actions={
          <Button type="button" variant="outline" onClick={() => void refreshNow()} aria-busy={isRefreshing} disabled={isRefreshing}>
            <RefreshCw className={isRefreshing ? 'size-4 animate-spin' : 'size-4'} aria-hidden="true" />
            {isRefreshing ? 'Refreshing...' : 'Refresh now'}
          </Button>
        }
      />

      {unavailableCount > 0 ? (
        <Alert variant="warning" aria-live="polite">
          <AlertTriangle className="size-4" aria-hidden="true" />
          <AlertTitle>Health attention needed</AlertTitle>
          <AlertDescription>
            One or more health endpoints are unavailable or could not be checked. The cards below include endpoint-specific
            status text for screen readers and operators.
          </AlertDescription>
        </Alert>
      ) : null}

      <section aria-labelledby="health-status-heading" className="space-y-5">
        <div className="space-y-1">
          <h2 id="health-status-heading" className="text-xl font-semibold tracking-tight">
            Health status
          </h2>
          <p className="text-sm text-muted-foreground">Auto-refreshes every 30 seconds while this page is open.</p>
        </div>
        <div className="grid gap-5 lg:grid-cols-2">
          <HealthStatusCard
            title="API live"
            description="Process-level liveness check for the API service."
            endpoint="/health/live"
            status={live.status}
            responseStatus={live.responseStatus}
            error={live.error}
            icon={HeartPulse}
          />
          <HealthStatusCard
            title="API ready"
            description="Readiness check for serving query traffic."
            endpoint="/health/ready"
            status={ready.status}
            responseStatus={ready.responseStatus}
            error={ready.error}
            icon={Activity}
          />
        </div>
      </section>

      <PageSection
        title="System context"
        titleId="operations-context-heading"
        icon={Network}
        description="Runtime values used by the frontend for health checks and operator handoff."
      >
        <div className="grid gap-4 md:grid-cols-2">
          <StatTile label="API base URL" icon={Network} hint="Configured with VITE_API_BASE_URL or the frontend default.">
            <p className="break-all font-mono text-sm">{apiBaseUrl}</p>
          </StatTile>
          <StatTile label="Last checked" icon={RefreshCw} hint={formatTimestamp(lastCheckedAt)}>
            <p className="text-lg font-semibold">{formatRelativeTime(lastCheckedAt)}</p>
          </StatTile>
        </div>
      </PageSection>

      <PageSection
        title="Observability links"
        titleId="observability-links-heading"
        icon={ExternalLink}
        description="External tools are shown only when their environment variables are configured."
      >
        {configuredLinks.length > 0 ? (
          <div className="grid gap-4 md:grid-cols-2">
            {configuredLinks.map((link) => (
              <ExternalToolCard key={link.title} {...link} />
            ))}
          </div>
        ) : (
          <Empty aria-label="No observability links configured">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <ExternalLink className="size-5" aria-hidden="true" />
              </EmptyMedia>
              <EmptyTitle>No external tools configured</EmptyTitle>
              <EmptyDescription>
                Set VITE_GRAFANA_URL or VITE_JAEGER_URL to show operator handoff links here.
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        )}
      </PageSection>
    </main>
  )
}

function ExternalToolCard({
  title,
  description,
  href,
  icon: Icon,
}: {
  title: string
  description: string
  href: string
  icon: LucideIcon
}) {
  return (
    <Card className="surface-panel overflow-hidden border-border/60">
      <CardHeader className="border-b border-border/60 bg-muted/30">
        <div className="flex items-start gap-3">
          <div className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-border/60 bg-background shadow-xs">
            <Icon className="size-5 text-muted-foreground" aria-hidden="true" />
          </div>
          <div className="space-y-1">
            <CardTitle>{title}</CardTitle>
            <CardDescription>{description}</CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4 p-5">
        <p className="break-all rounded-lg border border-border/60 bg-muted/30 px-3 py-2 font-mono text-xs text-muted-foreground">
          {href}
        </p>
        <Button asChild variant="outline">
          <a href={href} target="_blank" rel="noopener noreferrer">
            Open {title}
            <ExternalLink className="size-4" aria-hidden="true" />
          </a>
        </Button>
      </CardContent>
    </Card>
  )
}
