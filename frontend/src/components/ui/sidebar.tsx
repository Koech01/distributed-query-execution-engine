/* eslint-disable react-refresh/only-export-components */
import * as React from 'react'
import { Slot } from '@radix-ui/react-slot'
import { PanelLeft } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { useMobile } from '@/hooks/use-mobile'
import { cn } from '@/lib/utils'

const MOBILE_SIDEBAR_WIDTH = '16rem'
const DESKTOP_SIDEBAR_WIDTH_EXPANDED = '16rem'
const DESKTOP_SIDEBAR_WIDTH_COLLAPSED = '4.5rem'

interface SidebarContextValue {
  open: boolean
  openMobile: boolean
  isMobile: boolean
  showLabels: boolean
  setOpen: (open: boolean) => void
  setOpenMobile: (open: boolean) => void
  toggleSidebar: () => void
  closeMobileSidebar: () => void
}

const SidebarContext = React.createContext<SidebarContextValue | null>(null)

function useSidebar() {
  const context = React.useContext(SidebarContext)

  if (!context) {
    throw new Error('useSidebar must be used within a SidebarProvider')
  }

  return context
}

function SidebarProvider({ defaultOpen = true, children }: React.PropsWithChildren<{ defaultOpen?: boolean }>) {
  const isMobile = useMobile()
  const [open, setOpen] = React.useState(defaultOpen)
  const [openMobile, setOpenMobile] = React.useState(false)

  const closeMobileSidebar = React.useCallback(() => {
    setOpenMobile(false)
  }, [])

  const toggleSidebar = React.useCallback(() => {
    if (isMobile) {
      setOpenMobile((value) => !value)
      return
    }

    setOpen((value) => !value)
  }, [isMobile])

  React.useEffect(() => {
    if (!isMobile) {
      setOpenMobile(false)
    }
  }, [isMobile])

  React.useEffect(() => {
    if (!isMobile || !openMobile) {
      return undefined
    }

    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpenMobile(false)
      }
    }

    document.addEventListener('keydown', onKeyDown)

    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [isMobile, openMobile])

  const showLabels = isMobile || open

  const value = React.useMemo(
    () => ({
      open,
      openMobile,
      isMobile,
      showLabels,
      setOpen,
      setOpenMobile,
      toggleSidebar,
      closeMobileSidebar,
    }),
    [closeMobileSidebar, isMobile, open, openMobile, showLabels, toggleSidebar],
  )

  return <SidebarContext.Provider value={value}>{children}</SidebarContext.Provider>
}

function SidebarInset({ className, ...props }: React.ComponentProps<'main'>) {
  return <main className={cn('flex min-h-svh flex-1 flex-col bg-background', className)} {...props} />
}

function Sidebar({ className, children, ...props }: React.ComponentProps<'aside'>) {
  const { closeMobileSidebar, isMobile, open, openMobile } = useSidebar()

  if (isMobile) {
    return (
      <>
        {openMobile ? (
          <button
            type="button"
            className="fixed inset-0 z-40 bg-black/50 motion-reduce:transition-none md:hidden"
            aria-label="Close navigation menu"
            onClick={closeMobileSidebar}
          />
        ) : null}
        <aside
          id="app-sidebar"
          data-mobile="true"
          data-state={openMobile ? 'open' : 'closed'}
          aria-hidden={openMobile ? undefined : true}
          className={cn(
            'fixed inset-y-0 left-0 z-50 flex flex-col border-r bg-sidebar text-sidebar-foreground shadow-xl transition-transform duration-200 ease-in-out motion-reduce:transition-none md:hidden',
            openMobile ? 'translate-x-0' : '-translate-x-full pointer-events-none',
            openMobile && 'pointer-events-auto',
            className,
          )}
          style={{ width: MOBILE_SIDEBAR_WIDTH }}
          {...props}
        >
          {children}
        </aside>
      </>
    )
  }

  return (
    <aside
      id="app-sidebar"
      data-state={open ? 'expanded' : 'collapsed'}
      className={cn(
        'hidden min-h-svh shrink-0 flex-col border-r bg-sidebar text-sidebar-foreground transition-[width] duration-200 motion-reduce:transition-none md:flex',
        className,
      )}
      style={{ width: open ? DESKTOP_SIDEBAR_WIDTH_EXPANDED : DESKTOP_SIDEBAR_WIDTH_COLLAPSED }}
      {...props}
    >
      {children}
    </aside>
  )
}

function SidebarTrigger({ className, ...props }: React.ComponentProps<typeof Button>) {
  const { isMobile, open, openMobile, toggleSidebar } = useSidebar()
  const isOpen = isMobile ? openMobile : open

  return (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      className={className}
      onClick={toggleSidebar}
      aria-expanded={isOpen}
      aria-controls="app-sidebar"
      {...props}
    >
      <PanelLeft aria-hidden="true" />
      <span className="sr-only">Toggle sidebar</span>
    </Button>
  )
}

function SidebarHeader({ className, ...props }: React.ComponentProps<'div'>) {
  const { showLabels } = useSidebar()

  return (
    <div
      className={cn('flex h-16 items-center gap-2 px-4', !showLabels && 'justify-center px-2', className)}
      {...props}
    />
  )
}

function SidebarContent({ className, ...props }: React.ComponentProps<'div'>) {
  return <div className={cn('flex min-h-0 flex-1 flex-col gap-2 overflow-auto px-3 py-2', className)} {...props} />
}

function SidebarFooter({ className, ...props }: React.ComponentProps<'div'>) {
  const { showLabels } = useSidebar()

  if (!showLabels) {
    return null
  }

  return <div className={cn('border-t p-3', className)} {...props} />
}

function SidebarGroup({ className, ...props }: React.ComponentProps<'div'>) {
  return <div className={cn('space-y-1', className)} {...props} />
}

function SidebarGroupLabel({ className, ...props }: React.ComponentProps<'div'>) {
  const { showLabels } = useSidebar()

  if (!showLabels) {
    return null
  }

  return <div className={cn('px-2 py-1 text-xs font-medium text-sidebar-foreground/70', className)} {...props} />
}

function SidebarMenu({ className, ...props }: React.ComponentProps<'ul'>) {
  return <ul className={cn('space-y-1', className)} {...props} />
}

function SidebarMenuItem({ className, ...props }: React.ComponentProps<'li'>) {
  return <li className={cn('list-none', className)} {...props} />
}

function SidebarMenuButton({
  asChild = false,
  className,
  isActive,
  ...props
}: React.ComponentProps<'a'> & { asChild?: boolean; isActive?: boolean }) {
  const Comp = asChild ? Slot : 'a'

  return (
    <Comp
      aria-current={isActive ? 'page' : undefined}
      className={cn(
        'flex min-h-9 items-center gap-2 rounded-md px-2 text-sm font-medium text-sidebar-foreground outline-none transition-colors hover:bg-sidebar-accent hover:text-sidebar-accent-foreground focus-visible:ring-2 focus-visible:ring-sidebar-ring',
        isActive && 'bg-sidebar-accent text-sidebar-accent-foreground',
        className,
      )}
      {...props}
    />
  )
}

export {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarTrigger,
  useSidebar,
}
