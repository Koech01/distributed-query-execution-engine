import { Activity, History, Settings } from 'lucide-react'

import { ComingSoonPage } from '@/components/ui/coming-soon-page'

export function QueryHistoryComingSoonPage() {
  return (
    <ComingSoonPage
      title="Query History"
      description="Review recent executions, re-run saved queries, and jump back to prior results from one place."
      badge="Local history"
      icon={History}
      highlights={[
        'Browse recent query metadata without storing parameter values.',
        'Re-run prior SQL from the console with one click.',
        'Open completed async results by query ID.',
      ]}
    />
  )
}

export function OperationsComingSoonPage() {
  return (
    <ComingSoonPage
      title="Operations"
      description="Monitor API health, readiness, and external observability tools from a single operational view."
      badge="Health & observability"
      icon={Activity}
      highlights={[
        'Track live and ready health endpoints with clear status indicators.',
        'Surface API base URL and last-checked timestamps for operators.',
        'Link out to Grafana and Jaeger when configured in the environment.',
      ]}
    />
  )
}

export function SettingsComingSoonPage() {
  return (
    <ComingSoonPage
      title="Settings"
      description="Personalize default query options and control how local history behaves across sessions."
      badge="Preferences"
      icon={Settings}
      highlights={[
        'Set default timeout, failure policy, and async execution preferences.',
        'Choose whether full SQL is stored in local history.',
        'Persist UI preferences locally without storing credentials.',
      ]}
    />
  )
}
