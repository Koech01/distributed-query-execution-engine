import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'

import { cn } from '@/lib/utils'

function Empty({ className, ...props }: React.ComponentProps<'section'>) {
  return (
    <section
      className={cn(
        'flex min-h-56 w-full flex-col items-center justify-center gap-4 rounded-xl border border-dashed border-border/60 bg-card/50 p-8 text-center shadow-sm',
        className,
      )}
      {...props}
    />
  )
}

function EmptyHeader({ className, ...props }: React.ComponentProps<'div'>) {
  return <div className={cn('flex flex-col items-center gap-2 text-center', className)} {...props} />
}

const emptyMediaVariants = cva('mb-2 flex shrink-0 items-center justify-center', {
  variants: {
    variant: {
      default: 'w-full',
      icon: 'size-10 rounded-lg bg-muted text-foreground [&_svg:not([class*="size-"])]:size-6',
    },
  },
  defaultVariants: {
    variant: 'default',
  },
})

function EmptyMedia({
  className,
  variant = 'default',
  ...props
}: React.ComponentProps<'div'> & VariantProps<typeof emptyMediaVariants>) {
  return <div className={cn(emptyMediaVariants({ variant }), className)} {...props} />
}

type EmptyTitleProps = React.ComponentProps<'h2'> & {
  as?: 'h1' | 'h2' | 'h3'
}

function EmptyTitle({ as: Comp = 'h2', className, ...props }: EmptyTitleProps) {
  return <Comp className={cn('text-lg font-semibold tracking-tight', className)} {...props} />
}

function EmptyDescription({ className, ...props }: React.ComponentProps<'p'>) {
  return <p className={cn('max-w-md text-sm text-muted-foreground', className)} {...props} />
}

function EmptyContent({ className, ...props }: React.ComponentProps<'div'>) {
  return <div className={cn('flex flex-col items-center gap-2 sm:flex-row', className)} {...props} />
}

export { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle }
