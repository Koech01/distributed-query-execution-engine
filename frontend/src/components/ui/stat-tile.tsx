import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

import { cn } from '@/lib/utils'

interface StatTileProps {
  label: string
  icon?: LucideIcon
  children: ReactNode
  hint?: string
  className?: string
}

export function StatTile({ label, icon: Icon, children, hint, className }: StatTileProps) {
  return (
    <div
      className={cn(
        'group rounded-xl border border-border/60 bg-background/80 p-4 shadow-xs transition-shadow hover:shadow-sm',
        className,
      )}
    >
      <div className="mb-3 flex items-center gap-2">
        {Icon ? (
          <div
            className="flex size-8 items-center justify-center rounded-md border border-border/60 bg-muted/50"
            aria-hidden="true"
          >
            <Icon className="size-4 text-muted-foreground" />
          </div>
        ) : null}
        <p className="text-sm font-medium text-muted-foreground">{label}</p>
      </div>
      <div className="space-y-1 text-sm">{children}</div>
      {hint ? <p className="mt-3 text-xs leading-relaxed text-muted-foreground">{hint}</p> : null}
    </div>
  )
}
