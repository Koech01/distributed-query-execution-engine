import { test, expect } from './fixtures/test'

const mainRoutes = [
  { path: '/query', heading: 'Query Console' },
  { path: '/history', heading: 'Query History' },
  { path: '/operations', heading: 'Operations', waitFor: /health status/i },
  { path: '/settings', heading: 'Settings' },
  { path: '/admin', heading: 'Administration' },
] as const

test.describe('Main navigation', () => {
  for (const route of mainRoutes) {
    test(`loads ${route.path}`, async ({ page }) => {
      await page.goto(route.path)

      if ('waitFor' in route && route.waitFor) {
        await expect(page.getByRole('heading', { name: route.waitFor })).toBeVisible({ timeout: 15_000 })
      }

      await expect(page.getByRole('heading', { level: 1, name: route.heading })).toBeVisible()
      await expect(page.getByRole('complementary', { name: 'Application sidebar' })).toBeVisible()
    })
  }

  test('highlights the active sidebar route', async ({ page }) => {
    await page.goto('/history')

    const primaryNavigation = page.getByRole('navigation', { name: 'Primary navigation' })
    await expect(primaryNavigation.getByRole('link', { name: 'History', exact: true })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  test('redirects the root path to the query console', async ({ page }) => {
    await page.goto('/')

    await expect(page).toHaveURL(/\/query$/)
    await expect(page.getByRole('heading', { level: 1, name: 'Query Console' })).toBeVisible()
  })
})
