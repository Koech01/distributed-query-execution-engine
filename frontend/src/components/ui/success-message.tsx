import { CheckCircle2 } from 'lucide-react'
import { useEffect, type ReactNode } from 'react'

import { cn } from '@/lib/utils'

export const SUCCESS_MESSAGE_AUTO_HIDE_MS = 5000

interface SuccessMessageProps {
  children: ReactNode
  id?: string
  className?: string
  autoHideMs?: number
  onDismiss?: () => void
}

export function SuccessMessage({
  children,
  id,
  className,
  autoHideMs = SUCCESS_MESSAGE_AUTO_HIDE_MS,
  onDismiss,
}: SuccessMessageProps) {
  useEffect(() => {
    if (!onDismiss || autoHideMs <= 0) {
      return
    }

    const timer = window.setTimeout(onDismiss, autoHideMs)
    return () => window.clearTimeout(timer)
  }, [autoHideMs, onDismiss, children])

  return (
    <p
      id={id}
      role="status"
      aria-live="polite"
      className={cn(
        'flex items-start gap-2 rounded-lg border border-success/40 bg-success/10 px-3 py-2 text-sm text-success-foreground dark:border-success/50 dark:bg-success/15',
        className,
      )}
    >
      <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-success" aria-hidden="true" />
      <span>{children}</span>
    </p>
  )
}
