import { LogOut, User } from 'lucide-react'
import { useNavigate } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

interface NavUserProps {
  name?: string
  email?: string
  onLogout?: () => void
}

export function NavUser({ name = 'Signed-in user', email = 'query operator', onLogout }: NavUserProps) {
  const navigate = useNavigate()

  const handleLogout = () => {
    onLogout?.()
    window.dispatchEvent(new CustomEvent('dqee:logout'))
    navigate('/login/', { replace: true })
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button type="button" variant="ghost" className="h-9 gap-2 px-2" aria-label="Open user menu">
          <span className="flex size-7 items-center justify-center rounded-full bg-muted text-muted-foreground" aria-hidden="true">
            <User className="size-4" />
          </span>
          <span className="hidden max-w-32 truncate text-left text-sm sm:inline">{name}</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel>
          <span className="block text-sm font-medium">{name}</span>
          <span className="block truncate text-xs font-normal text-muted-foreground">{email}</span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={handleLogout}>
          <LogOut className="size-4" aria-hidden="true" />
          Log out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
