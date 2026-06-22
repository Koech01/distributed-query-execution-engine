import type { LucideIcon } from 'lucide-react'
import { ArrowRight } from 'lucide-react'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { PageHeader } from '@/components/ui/page-header'

interface ComingSoonPageProps {
  title: string
  description: string
  titleId?: string
  badge?: string
  icon: LucideIcon
  highlights?: string[]
  primaryAction?: {
    label: string
    href: string
  }
}

export function ComingSoonPage({
  title,
  description,
  titleId = 'page-title',
  badge,
  icon: Icon,
  highlights = [],
  primaryAction = { label: 'Open query console', href: '/query' },
}: ComingSoonPageProps) {
  return (
    <main aria-labelledby={titleId} className="mx-auto flex w-full max-w-5xl flex-col gap-8">
      <PageHeader title={title} description={description} titleId={titleId} badge={badge} />

      <Empty className="animate-fade-in-up min-h-[22rem] border-border/60 bg-card/50 shadow-sm backdrop-blur-sm">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <Icon aria-hidden="true" />
          </EmptyMedia>
          <EmptyTitle as="h2">Coming in a future release</EmptyTitle>
          <EmptyDescription>{description}</EmptyDescription>
        </EmptyHeader>

        {highlights.length > 0 ? (
          <ul className="mx-auto max-w-lg space-y-2 text-left text-sm text-muted-foreground">
            {highlights.map((item) => (
              <li key={item} className="flex items-start gap-2">
                <span aria-hidden="true" className="mt-1.5 size-1.5 shrink-0 rounded-full bg-foreground/40" />
                <span>{item}</span>
              </li>
            ))}
          </ul>
        ) : null}

        <EmptyContent>
          <Button asChild>
            <Link to={primaryAction.href}>
              {primaryAction.label}
              <ArrowRight aria-hidden="true" />
            </Link>
          </Button>
        </EmptyContent>
      </Empty>
    </main>
  )
}
