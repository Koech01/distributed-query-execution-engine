import type { LucideIcon } from 'lucide-react'
import { NavLink } from 'react-router-dom'

import {
  SidebarGroup,
  SidebarGroupLabel,
  SidebarMenu,
  SidebarMenuItem,
  useSidebar,
} from '@/components/ui/sidebar'
import { cn } from '@/lib/utils'

export interface NavItem {
  title: string
  url: string
  icon: LucideIcon
}

interface NavMainProps {
  items: NavItem[]
}

export function NavMain({ items }: NavMainProps) {
  const { closeMobileSidebar, isMobile, showLabels } = useSidebar()

  const handleNavigate = () => {
    if (isMobile) {
      closeMobileSidebar()
    }
  }

  return (
    <nav aria-label="Primary navigation">
      <SidebarGroup>
        <SidebarGroupLabel>Navigation</SidebarGroupLabel>
        <SidebarMenu>
          {items.map((item) => (
            <SidebarMenuItem key={item.url}>
              <NavLink
                to={item.url}
                end={item.url === '/query'}
                title={showLabels ? undefined : item.title}
                aria-label={showLabels ? undefined : item.title}
                onClick={handleNavigate}
                className={({ isActive }) =>
                  cn(
                    'relative flex min-h-10 items-center gap-2 rounded-lg text-sm font-medium text-sidebar-foreground outline-none transition-all duration-200 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground focus-visible:ring-2 focus-visible:ring-sidebar-ring motion-reduce:transition-none',
                    showLabels ? 'px-3' : 'justify-center px-2',
                    isActive &&
                      'bg-sidebar-accent text-sidebar-accent-foreground shadow-xs before:absolute before:inset-y-2 before:left-0 before:w-0.5 before:rounded-full before:bg-primary',
                    !showLabels &&
                      isActive &&
                      'before:inset-y-1.5 before:left-1 before:h-auto before:w-0.5',
                  )
                }
              >
                <item.icon className="size-4 shrink-0" aria-hidden="true" />
                {showLabels ? (
                  <span>{item.title}</span>
                ) : (
                  <span className="sr-only">{item.title}</span>
                )}
              </NavLink>
            </SidebarMenuItem>
          ))}
        </SidebarMenu>
      </SidebarGroup>
    </nav>
  )
}
