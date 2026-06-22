import { DatabaseZap } from 'lucide-react'
import type { ReactNode } from 'react'

import { cn } from '@/lib/utils'

interface AuthPageLayoutProps {
  children: ReactNode
  className?: string
  showBrandPanel?: boolean
}

export function AuthPageLayout({ children, className, showBrandPanel = true }: AuthPageLayoutProps) {
  return (
    <main className={cn('relative flex min-h-svh bg-background', className)}>
      {showBrandPanel ? (
      <div
        className="relative hidden w-[min(44%,520px)] shrink-0 flex-col justify-between overflow-hidden border-r border-border/60 bg-muted/40 p-10 lg:flex"
        aria-hidden="true"
      >
        <div className="auth-grid-pattern pointer-events-none absolute inset-0 opacity-70" />
        <div className="relative space-y-6">
          <div className="flex size-11 items-center justify-center rounded-xl border border-border/60 bg-background shadow-sm">
            <DatabaseZap className="size-5" />
          </div>
          <div className="space-y-3">
            <p className="text-sm font-medium tracking-[0.2em] uppercase text-muted-foreground">DQEE</p>
            <h1 className="max-w-sm text-3xl font-semibold tracking-tight text-balance">
              Distributed Query Execution Engine
            </h1>
            <p className="max-w-md text-base leading-relaxed text-muted-foreground">
              Compose parameterized T-SQL, execute across shards, and inspect results with operational clarity.
            </p>
          </div>
        </div>
        <p className="relative text-sm text-muted-foreground">
          Secure access. Neutral observability. Backend contracts drive every interaction.
        </p>
      </div>
      ) : null}
      <div className="flex flex-1 items-center justify-center p-4 sm:p-8">
        <div className="animate-fade-in-up w-full max-w-md">{children}</div>
      </div>
    </main>
  )
}
