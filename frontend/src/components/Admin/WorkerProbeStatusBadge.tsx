import type { WorkerProbeStatus } from '@/components/types/admin'
import { AlertTriangle, CheckCircle2, HelpCircle } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { formatWorkerProbeStatus } from '@/lib/admin-display'
import { cn } from '@/lib/utils'

interface WorkerProbeStatusBadgeProps {
  label: string
  status: WorkerProbeStatus
  latencyMs?: number | null
}

function statusVariant(status: WorkerProbeStatus): 'default' | 'secondary' | 'destructive' | 'outline' {
  switch (status) {
    case 0:
      return 'default'
    case 1:
      return 'destructive'
    case 2:
      return 'secondary'
    default:
      return 'outline'
  }
}

function StatusIcon({ status }: { status: WorkerProbeStatus }) {
  switch (status) {
    case 0:
      return <CheckCircle2 className="size-3.5" aria-hidden="true" />
    case 1:
      return <AlertTriangle className="size-3.5" aria-hidden="true" />
    case 2:
      return <HelpCircle className="size-3.5" aria-hidden="true" />
    default:
      return <HelpCircle className="size-3.5" aria-hidden="true" />
  }
}

export function WorkerProbeStatusBadge({ label, status, latencyMs }: WorkerProbeStatusBadgeProps) {
  const statusText = formatWorkerProbeStatus(status)
  const latencyText = latencyMs != null ? `${latencyMs} ms` : 'Latency unavailable'

  return (
    <Badge
      variant={statusVariant(status)}
      className={cn('gap-1.5 font-normal', status === 0 && 'bg-emerald-600/10 text-emerald-700 dark:text-emerald-300')}
    >
      <StatusIcon status={status} />
      <span>
        {label}: {statusText}
        {latencyMs != null ? ` (${latencyText})` : ''}
      </span>
      <span className="sr-only">
        {label} probe is {statusText}. {latencyText}.
      </span>
    </Badge>
  )
}
