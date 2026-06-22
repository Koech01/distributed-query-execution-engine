import type { ReactNode } from 'react'

import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

interface PageHeaderProps {
  title: string
  description?: string
  titleId?: string
  badge?: string
  actions?: ReactNode
  className?: string
}

export function PageHeader({ title, description, titleId, badge, actions, className }: PageHeaderProps) {
  return (
    <header
      className={cn(
        'animate-fade-in-up flex flex-col gap-4 border-b border-border/60 pb-6 sm:flex-row sm:items-end sm:justify-between',
        className,
      )}
    >
      <div className="space-y-2">
        {badge ? (
          <Badge variant="secondary" className="w-fit font-normal tracking-wide">
            {badge}
          </Badge>
        ) : null}
        <h1 id={titleId} className="text-3xl font-semibold tracking-tight text-balance sm:text-4xl">
          {title}
        </h1>
        {description ? (
          <p className="max-w-2xl text-base leading-relaxed text-muted-foreground text-pretty">{description}</p>
        ) : null}
      </div>
      {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
    </header>
  )
}
