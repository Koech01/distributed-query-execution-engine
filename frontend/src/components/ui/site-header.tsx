import { Link, useLocation } from 'react-router-dom'

import { useAuth } from '@/hooks/use-auth'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'
import { NavUser } from '@/components/ui/nav-user'
import { Separator } from '@/components/ui/separator'
import { SidebarTrigger } from '@/components/ui/sidebar'
import { ThemeToggle } from '@/components/ui/theme-toggle'

const routeLabels: Record<string, string> = {
  query: 'Query',
  history: 'History',
  operations: 'Operations',
  settings: 'Settings',
  admin: 'Admin',
  cache: 'Cache management',
}

function getBreadcrumbs(pathname: string) {
  const segments = pathname.split('/').filter(Boolean)

  if (segments.length === 0) {
    return [{ label: 'Query', href: '/query', current: true }]
  }

  return segments.map((segment, index) => {
    const href = `/${segments.slice(0, index + 1).join('/')}`
    return {
      label: routeLabels[segment] ?? segment,
      href,
      current: index === segments.length - 1,
    }
  })
}

export function SiteHeader() {
  const location = useLocation()
  const { user } = useAuth()
  const breadcrumbs = getBreadcrumbs(location.pathname)

  return (
    <header className="sticky top-0 z-30 flex h-16 shrink-0 items-center gap-2 border-b border-border/60 bg-background/80 px-4 backdrop-blur-md supports-[backdrop-filter]:bg-background/60 md:px-6">
      <SidebarTrigger />
      <Separator orientation="vertical" className="mr-2 h-5" />
      <Breadcrumb className="min-w-0 flex-1">
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink asChild>
              <Link to="/query">Home</Link>
            </BreadcrumbLink>
          </BreadcrumbItem>
          {breadcrumbs.map((item) => (
            <span className="contents" key={item.href}>
              <BreadcrumbSeparator />
              <BreadcrumbItem>
                {item.current ? (
                  <BreadcrumbPage>{item.label}</BreadcrumbPage>
                ) : (
                  <BreadcrumbLink asChild>
                    <Link to={item.href}>{item.label}</Link>
                  </BreadcrumbLink>
                )}
              </BreadcrumbItem>
            </span>
          ))}
        </BreadcrumbList>
      </Breadcrumb>
      <div className="flex items-center gap-1">
        <ThemeToggle />
        <NavUser name={user?.displayName ?? user?.subject} email={user?.email} />
      </div>
    </header>
  )
}
