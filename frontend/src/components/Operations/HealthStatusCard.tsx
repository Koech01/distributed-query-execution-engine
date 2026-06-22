import type { LucideIcon } from 'lucide-react'
import { AlertTriangle, CheckCircle2, Loader2, XCircle } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { getErrorMessage } from '@/lib/errors'
import { cn } from '@/lib/utils'

export type HealthStatus = 'loading' | 'available' | 'unavailable' | 'error'

export interface HealthStatusCardProps {
  title: string
  description: string
  endpoint: string
  status: HealthStatus
  responseStatus?: string
  error?: unknown
  icon: LucideIcon
}

const statusCopy: Record<HealthStatus, { label: string; detail: string }> = {
  loading: {
    label: 'Checking',
    detail: 'Waiting for the health endpoint to respond.',
  },
  available: {
    label: 'Available',
    detail: 'The endpoint responded successfully.',
  },
  unavailable: {
    label: 'Unavailable',
    detail: 'The endpoint responded, but the service is not currently available.',
  },
  error: {
    label: 'Error',
    detail: 'The endpoint could not be checked.',
  },
}

function StatusIcon({ status }: { status: HealthStatus }) {
  if (status === 'loading') {
    return <Loader2 className="size-5 animate-spin" aria-hidden="true" />
  }

  if (status === 'available') {
    return <CheckCircle2 className="size-5" aria-hidden="true" />
  }

  if (status === 'unavailable') {
    return <AlertTriangle className="size-5" aria-hidden="true" />
  }

  return <XCircle className="size-5" aria-hidden="true" />
}

export function HealthStatusCard({
  title,
  description,
  endpoint,
  status,
  responseStatus,
  error,
  icon: Icon,
}: HealthStatusCardProps) {
  const copy = statusCopy[status]
  const errorMessage = error ? getErrorMessage(error) : null

  return (
    <Card
      className={cn(
        'surface-panel animate-fade-in-up overflow-hidden border-border/60 shadow-sm',
        status === 'available' && 'border-emerald-500/30',
        status === 'unavailable' && 'border-warning/40',
        status === 'error' && 'border-destructive/40',
      )}
    >
      <CardHeader className="border-b border-border/60 bg-muted/30">
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-start gap-3">
            <div className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-border/60 bg-background shadow-xs">
              <Icon className="size-5 text-muted-foreground" aria-hidden="true" />
            </div>
            <div className="space-y-1">
              <CardTitle>{title}</CardTitle>
              <CardDescription>{description}</CardDescription>
            </div>
          </div>
          <Badge
            variant={status === 'available' ? 'secondary' : status === 'error' ? 'destructive' : 'outline'}
            className="gap-1.5"
          >
            <StatusIcon status={status} />
            {copy.label}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4 p-5">
        <div role="status" aria-live="polite" className="space-y-1">
          <p className="font-medium">{copy.detail}</p>
          {responseStatus ? <p className="text-sm text-muted-foreground">Backend status: {responseStatus}</p> : null}
          {errorMessage ? <p className="text-sm text-muted-foreground">{errorMessage}</p> : null}
        </div>
        <p className="rounded-lg border border-border/60 bg-muted/30 px-3 py-2 font-mono text-xs text-muted-foreground">
          {endpoint}
        </p>
      </CardContent>
    </Card>
  )
}
