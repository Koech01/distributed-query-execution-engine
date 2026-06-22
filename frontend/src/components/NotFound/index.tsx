import { ArrowUpRight, FileQuestion } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from '@/components/ui/empty'

interface NotFoundPageProps {
  standalone?: boolean
}

export function NotFoundPage({ standalone = false }: NotFoundPageProps) {
  const navigate = useNavigate()
  const Wrapper = standalone ? 'main' : 'section'

  return (
    <Wrapper
      aria-labelledby="not-found-title"
      className={
        standalone
          ? 'relative flex min-h-svh items-center justify-center overflow-hidden bg-background p-4'
          : 'flex w-full flex-1 items-center justify-center py-10'
      }
    >
      {standalone ? <div className="auth-grid-pattern pointer-events-none absolute inset-0 opacity-50" aria-hidden="true" /> : null}
      <Empty
        className={`animate-fade-in-up mx-auto min-h-0 w-[93%] max-w-none border-border/60 bg-card/80 shadow-lg backdrop-blur-sm ${
          standalone ? 'relative' : ''
        }`}
      >
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <FileQuestion aria-hidden="true" />
          </EmptyMedia>
          <EmptyTitle as="h1" id="not-found-title">
            Page not found
          </EmptyTitle>
          <EmptyDescription>
            The route you requested is not part of the Distributed Query Execution Engine. Check the URL, use the
            sidebar navigation, or return to the query console to continue working.
          </EmptyDescription>
        </EmptyHeader>
        <EmptyContent className="flex-row justify-center gap-2">
          <Button asChild>
            <Link to="/query">Query Console</Link>
          </Button>
          <Button type="button" variant="outline" onClick={() => navigate(-1)}>
            Go back
          </Button>
        </EmptyContent>
        <Button variant="link" asChild className="text-muted-foreground" size="sm">
          <Link to="/operations">
            View system health
            <ArrowUpRight aria-hidden="true" />
          </Link>
        </Button>
      </Empty>
    </Wrapper>
  )
}
