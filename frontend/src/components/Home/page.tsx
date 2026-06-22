import type { MouseEvent } from 'react'
import { Outlet } from 'react-router-dom'

import { AppSidebar } from '@/components/ui/app-sidebar'
import { SidebarInset, SidebarProvider } from '@/components/ui/sidebar'
import { SiteHeader } from '@/components/ui/site-header'
import { useAuth } from '@/hooks/use-auth'

interface HomeLayoutProps {
  showAdmin?: boolean
}

export function AuthenticatedLayout() {
  const { canAdmin } = useAuth()

  return <HomeLayout showAdmin={canAdmin} />
}

export function HomeLayout({ showAdmin = false }: HomeLayoutProps) {
  const focusMainContent = (event: MouseEvent<HTMLAnchorElement>) => {
    event.preventDefault()
    document.getElementById('main-content')?.focus()
  }

  return (
    <SidebarProvider>
      <div className="flex min-h-svh w-full bg-background text-foreground">
        <a
          href="#main-content"
          onClick={focusMainContent}
          className="sr-only fixed left-3 top-3 z-50 rounded-md bg-background px-3 py-2 text-sm font-medium text-foreground shadow-md focus:not-sr-only focus:outline-none focus:ring-2 focus:ring-ring"
        >
          Skip to main content
        </a>
        <AppSidebar showAdmin={showAdmin} />
        <SidebarInset id="main-content" aria-label="Application content" tabIndex={-1} className="relative">
          <div className="app-grid-pattern pointer-events-none absolute inset-0 opacity-40" aria-hidden="true" />
          <SiteHeader />
          <div className="relative flex-1 p-4 md:p-6 lg:p-8">
            <div className="mx-auto w-full max-w-7xl">
              <Outlet />
            </div>
          </div>
        </SidebarInset>
      </div>
    </SidebarProvider>
  )
}
