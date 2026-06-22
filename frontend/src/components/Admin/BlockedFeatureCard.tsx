import type { LucideIcon } from 'lucide-react'
import { Construction } from 'lucide-react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

interface BlockedFeatureCardProps {
  title: string
  description: string
  icon: LucideIcon
  highlights?: string[]
}

export function BlockedFeatureCard({ title, description, icon: Icon, highlights = [] }: BlockedFeatureCardProps) {
  return (
    <Card className="border-border/60 bg-card/80 shadow-sm">
      <CardHeader className="space-y-3">
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-start gap-3">
            <div
              className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-border/60 bg-muted/40"
              aria-hidden="true"
            >
              <Icon className="size-5 text-muted-foreground" />
            </div>
            <div className="min-w-0 space-y-1">
              <CardTitle className="text-base">{title}</CardTitle>
              <CardDescription>{description}</CardDescription>
            </div>
          </div>
          <Badge variant="outline" className="shrink-0">
            Phase 3
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <Alert variant="warning">
          <Construction aria-hidden="true" />
          <AlertTitle>Backend admin API not available</AlertTitle>
          <AlertDescription>
            This surface is wired for navigation and authorization, but the Distributed Query Execution Engine admin
            endpoints are not implemented yet. Actions remain disabled until the backend ships Phase 3 admin APIs.
          </AlertDescription>
        </Alert>

        {highlights.length > 0 ? (
          <ul className="space-y-2 text-sm text-muted-foreground">
            {highlights.map((item) => (
              <li key={item} className="flex items-start gap-2">
                <span aria-hidden="true" className="mt-1.5 size-1.5 shrink-0 rounded-full bg-foreground/40" />
                <span>{item}</span>
              </li>
            ))}
          </ul>
        ) : null}
      </CardContent>
    </Card>
  )
}
