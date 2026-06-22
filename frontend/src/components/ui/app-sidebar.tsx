import { Activity, DatabaseZap, History, Settings, Shield } from 'lucide-react'

import { NavMain, type NavItem } from '@/components/ui/nav-main'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  useSidebar,
} from '@/components/ui/sidebar'

const navigationItems: NavItem[] = [
  { title: 'Query', url: '/query', icon: DatabaseZap },
  { title: 'History', url: '/history', icon: History },
  { title: 'Operations', url: '/operations', icon: Activity },
  { title: 'Settings', url: '/settings', icon: Settings },
]

const adminItem: NavItem = { title: 'Admin', url: '/admin', icon: Shield }

interface AppSidebarProps {
  showAdmin?: boolean
}

function SidebarBrand() {
  const { showLabels } = useSidebar()

  return (
    <>
      <div
        className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-sidebar-border/60 bg-background shadow-xs"
        aria-hidden="true"
      >
        <DatabaseZap className="size-5" />
      </div>
      {showLabels ? (
        <div className="min-w-0">
          <p className="truncate text-xs text-sidebar-foreground/70">Distributed query engine</p>
        </div>
      ) : null}
    </>
  )
}

export function AppSidebar({ showAdmin = false }: AppSidebarProps) {
  const items = showAdmin ? [...navigationItems, adminItem] : navigationItems

  return (
    <Sidebar aria-label="Application sidebar">
      <SidebarHeader className="border-b border-sidebar-border/60">
        <SidebarBrand />
      </SidebarHeader>
      <SidebarContent className="pt-4">
        <NavMain items={items} />
      </SidebarContent>
      <SidebarFooter className="border-sidebar-border/60 bg-sidebar/50">
        <p className="text-xs leading-relaxed text-sidebar-foreground/70">
          Execute queries. Inspect results. Monitor operations.
        </p>
      </SidebarFooter>
    </Sidebar>
  )
}
