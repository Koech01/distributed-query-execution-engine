import { Info } from 'lucide-react'

import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Switch } from '@/components/ui/switch'
import type { FailurePolicy } from '@/components/types'
import { MAX_MAX_NODES, MAX_TIMEOUT_SECONDS, MIN_MAX_NODES, MIN_TIMEOUT_SECONDS } from '@/lib/schemas'

interface QueryOptionsPanelProps {
  timeoutSeconds: number
  onTimeoutSecondsChange: (value: number) => void
  failurePolicy: FailurePolicy
  onFailurePolicyChange: (value: FailurePolicy) => void
  asyncEnabled: boolean
  onAsyncEnabledChange: (value: boolean) => void
  streamEnabled: boolean
  onStreamEnabledChange: (value: boolean) => void
  maxNodes?: number
  onMaxNodesChange: (value: number | undefined) => void
  showAdvanced?: boolean
  onShowAdvancedChange?: (value: boolean) => void
  timeoutError?: string
  maxNodesError?: string
  disabled?: boolean
}

export function QueryOptionsPanel({
  timeoutSeconds,
  onTimeoutSecondsChange,
  failurePolicy,
  onFailurePolicyChange,
  asyncEnabled,
  onAsyncEnabledChange,
  streamEnabled,
  onStreamEnabledChange,
  maxNodes,
  onMaxNodesChange,
  showAdvanced = false,
  onShowAdvancedChange,
  timeoutError,
  maxNodesError,
  disabled = false,
}: QueryOptionsPanelProps) {
  const timeoutErrorId = 'query-timeout-error'
  const maxNodesErrorId = 'query-max-nodes-error'
  const failurePolicyHelpId = 'failure-policy-help'
  const asyncHelpId = 'async-toggle-help'
  const streamHelpId = 'stream-toggle-help'

  return (
    <div className="space-y-4">
      <div className="grid gap-4">
        <div className="space-y-2">
          <Label htmlFor="query-timeout">Timeout (seconds)</Label>
          <Input
            id="query-timeout"
            type="number"
            min={MIN_TIMEOUT_SECONDS}
            max={MAX_TIMEOUT_SECONDS}
            value={timeoutSeconds}
            onChange={(event) => onTimeoutSecondsChange(Number(event.target.value))}
            disabled={disabled}
            aria-required="true"
            aria-invalid={timeoutError ? 'true' : undefined}
            aria-describedby={timeoutError ? timeoutErrorId : undefined}
          />
          {timeoutError ? (
            <p id={timeoutErrorId} className="text-sm text-destructive" role="alert">
              {timeoutError}
            </p>
          ) : (
            <p className="text-sm text-muted-foreground">
              Allowed range: {MIN_TIMEOUT_SECONDS}–{MAX_TIMEOUT_SECONDS} seconds.
            </p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor="failure-policy">Failure policy</Label>
          <Select
            value={failurePolicy}
            onValueChange={(value) => onFailurePolicyChange(value as FailurePolicy)}
            disabled={disabled}
          >
            <SelectTrigger id="failure-policy" aria-describedby={failurePolicyHelpId}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="BestEffort">Best effort</SelectItem>
              <SelectItem value="StrictAll">Strict all</SelectItem>
            </SelectContent>
          </Select>
          <p id={failurePolicyHelpId} className="flex items-start gap-2 text-sm text-muted-foreground">
            <Info className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <span>
              The backend accepts <strong>StrictAll</strong> but does not currently enforce strict-all execution.
              Results may still include partial shard data.
            </span>
          </p>
        </div>
      </div>

      <div className="flex items-center justify-between gap-4 rounded-xl border border-border/60 bg-muted/20 p-4 transition-colors">
        <div className="space-y-1">
          <Label htmlFor="stream-toggle">Stream results</Label>
          <p id={streamHelpId} className="text-sm text-muted-foreground">
            Use POST /queries/stream to render rows incrementally. Available for synchronous queries only.
          </p>
        </div>
        <Switch
          id="stream-toggle"
          checked={streamEnabled}
          onCheckedChange={onStreamEnabledChange}
          disabled={disabled || asyncEnabled}
          aria-describedby={streamHelpId}
        />
      </div>

      <div className="flex items-center justify-between gap-4 rounded-xl border border-border/60 bg-muted/20 p-4 transition-colors">
        <div className="space-y-1">
          <Label htmlFor="async-toggle">Run asynchronously</Label>
          <p id={asyncHelpId} className="text-sm text-muted-foreground">
            Submit with async=true and poll status until the backend marks the query completed.
          </p>
        </div>
        <Switch
          id="async-toggle"
          checked={asyncEnabled}
          onCheckedChange={onAsyncEnabledChange}
          disabled={disabled}
          aria-describedby={asyncHelpId}
        />
      </div>

      {onShowAdvancedChange ? (
        <div className="flex items-center justify-between gap-4">
          <Label htmlFor="advanced-options-toggle">Show advanced options</Label>
          <Switch
            id="advanced-options-toggle"
            checked={showAdvanced}
            onCheckedChange={onShowAdvancedChange}
            disabled={disabled}
          />
        </div>
      ) : null}

      {showAdvanced ? (
        <div className="space-y-2">
          <Label htmlFor="max-nodes">Max nodes (optional)</Label>
          <Input
            id="max-nodes"
            type="number"
            min={MIN_MAX_NODES}
            max={MAX_MAX_NODES}
            value={maxNodes ?? ''}
            onChange={(event) => {
              const nextValue = event.target.value
              onMaxNodesChange(nextValue === '' ? undefined : Number(nextValue))
            }}
            disabled={disabled}
            aria-invalid={maxNodesError ? 'true' : undefined}
            aria-describedby={maxNodesError ? maxNodesErrorId : undefined}
            placeholder="Use cluster default"
          />
          {maxNodesError ? (
            <p id={maxNodesErrorId} className="text-sm text-destructive" role="alert">
              {maxNodesError}
            </p>
          ) : (
            <p className="text-sm text-muted-foreground">
              Optional limit between {MIN_MAX_NODES} and {MAX_MAX_NODES.toLocaleString()} nodes.
            </p>
          )}
        </div>
      ) : null}
    </div>
  )
}
