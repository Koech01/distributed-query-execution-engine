import { AlertTriangle } from 'lucide-react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import type { QueryResult } from '@/components/types'

interface DegradationBannerProps {
  result: Pick<QueryResult, 'degraded' | 'degradationReason' | 'failedShards' | 'successfulShards' | 'totalShards'>
}

export function DegradationBanner({ result }: DegradationBannerProps) {
  if (!result.degraded) {
    return null
  }

  const failedShardLabel =
    result.failedShards.length > 0
      ? `Failed shard indices: ${result.failedShards.join(', ')}.`
      : 'One or more shards did not return complete data.'

  return (
    <Alert variant="warning">
      <AlertTriangle className="size-4" aria-hidden="true" />
      <AlertTitle>Partial results returned</AlertTitle>
      <AlertDescription>
        <p>
          {result.successfulShards}/{result.totalShards} shards succeeded. {failedShardLabel}
        </p>
        {result.degradationReason ? <p className="mt-2">{result.degradationReason}</p> : null}
      </AlertDescription>
    </Alert>
  )
}
