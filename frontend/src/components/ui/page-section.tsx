import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

import { cn } from '@/lib/utils'

interface PageSectionProps {
  title: string
  description?: string
  titleId?: string
  icon?: LucideIcon
  srOnlyTitle?: boolean
  children: ReactNode
  className?: string
  contentClassName?: string
}

export function PageSection({
  title,
  description,
  titleId,
  icon: Icon,
  srOnlyTitle = false,
  children,
  className,
  contentClassName,
}: PageSectionProps) {
  return (
    <section aria-labelledby={srOnlyTitle ? titleId : undefined} className={cn('surface-panel animate-fade-in-up overflow-hidden', className)}>
      <div className="border-b border-border/60 bg-muted/30 px-5 py-4 sm:px-6">
        <div className="flex items-start gap-3">
          {Icon ? (
            <div
              className="flex size-9 shrink-0 items-center justify-center rounded-lg border border-border/60 bg-background shadow-xs"
              aria-hidden="true"
            >
              <Icon className="size-4 text-foreground" />
            </div>
          ) : null}
          <div className="min-w-0 space-y-1">
            <h2
              id={titleId}
              className={cn('text-base font-semibold tracking-tight', srOnlyTitle && 'sr-only')}
            >
              {title}
            </h2>
            {description ? <p className="text-sm leading-relaxed text-muted-foreground">{description}</p> : null}
          </div>
        </div>
      </div>
      <div className={cn('space-y-5 p-5 sm:p-6', contentClassName)}>{children}</div>
    </section>
  )
}
