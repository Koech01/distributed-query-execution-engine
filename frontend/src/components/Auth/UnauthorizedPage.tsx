import { ArrowUpRight, ShieldX } from 'lucide-react'
import { Link } from 'react-router-dom'

import { AuthPageLayout } from '@/components/ui/auth-page-layout'
import { Button } from '@/components/ui/button'
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from '@/components/ui/empty'

export function UnauthorizedPage() {
  return (
    <AuthPageLayout>
      <Empty className="border-border/60 bg-card shadow-lg">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <ShieldX className="text-warning" aria-hidden="true" />
          </EmptyMedia>
          <EmptyTitle as="h1" id="unauthorized-title">
            Access denied
          </EmptyTitle>
          <EmptyDescription>
            Your account is signed in but does not include the <code>query:read</code> or required admin scope for this
            page. Contact your administrator if you believe this is an error.
          </EmptyDescription>
        </EmptyHeader>
        <EmptyContent>
          <Button asChild>
            <Link to="/query">Back to query console</Link>
          </Button>
        </EmptyContent>
        <Button variant="link" asChild className="text-muted-foreground" size="sm">
          <Link to="/operations">
            View system health
            <ArrowUpRight aria-hidden="true" />
          </Link>
        </Button>
      </Empty>
    </AuthPageLayout>
  )
}
