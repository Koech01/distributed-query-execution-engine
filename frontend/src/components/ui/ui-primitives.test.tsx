import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Breadcrumb, BreadcrumbItem, BreadcrumbList, BreadcrumbPage } from '@/components/ui/breadcrumb'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from '@/components/ui/empty'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Textarea } from '@/components/ui/textarea'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'

describe('ui primitives', () => {
  it('renders documented shadcn primitives without runtime errors', () => {
    render(
      <TooltipProvider>
        <div>
          <Button>Run</Button>
          <Label htmlFor="query-name">Query name</Label>
          <Input id="query-name" />
          <Textarea aria-label="SQL query" />
          <Checkbox aria-label="Save setting" />
          <Switch aria-label="Async mode" />
          <Badge>Ready</Badge>
          <Skeleton aria-label="Loading" className="h-4 w-20" />
          <Separator />
          <Alert>
            <AlertTitle>Notice</AlertTitle>
            <AlertDescription>System message</AlertDescription>
          </Alert>
          <Card>
            <CardHeader>
              <CardTitle>Card title</CardTitle>
            </CardHeader>
            <CardContent>Card body</CardContent>
          </Card>
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem>
                <BreadcrumbPage>Query</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead scope="col">Column</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow>
                <TableCell>Value</TableCell>
              </TableRow>
            </TableBody>
          </Table>
          <Tabs defaultValue="one">
            <TabsList>
              <TabsTrigger value="one">One</TabsTrigger>
            </TabsList>
            <TabsContent value="one">Tab content</TabsContent>
          </Tabs>
          <Select>
            <SelectTrigger aria-label="Select policy">
              <SelectValue placeholder="Policy" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="BestEffort">BestEffort</SelectItem>
            </SelectContent>
          </Select>
          <Dialog>
            <DialogTrigger asChild>
              <Button type="button">Open dialog</Button>
            </DialogTrigger>
            <DialogContent>
              <DialogTitle>Dialog title</DialogTitle>
              <DialogDescription>Dialog description</DialogDescription>
            </DialogContent>
          </Dialog>
          <Tooltip>
            <TooltipTrigger asChild>
              <Button type="button">Tooltip trigger</Button>
            </TooltipTrigger>
            <TooltipContent>Tooltip content</TooltipContent>
          </Tooltip>
          <ScrollArea className="h-10 w-10">Scrollable content</ScrollArea>
          <Empty>
            <EmptyHeader>
              <EmptyTitle>No rows</EmptyTitle>
              <EmptyDescription>Run a query to see results.</EmptyDescription>
            </EmptyHeader>
          </Empty>
        </div>
      </TooltipProvider>,
    )

    expect(screen.getByRole('button', { name: /run/i })).toBeInTheDocument()
    expect(screen.getByRole('table')).toBeInTheDocument()
    expect(screen.getByText(/no rows/i)).toBeInTheDocument()
  })
})
