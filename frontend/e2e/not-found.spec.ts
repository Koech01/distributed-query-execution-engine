import { test, expect } from './fixtures/test'

test.describe('404 page', () => {
  test('renders an in-shell not-found state with recovery actions', async ({ page }) => {
    await page.goto('/this-route-does-not-exist')

    await expect(page.getByRole('region', { name: 'Page not found' })).toBeVisible()
    await expect(page.getByRole('heading', { level: 1, name: 'Page not found' })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Query Console' })).toBeVisible()
    await expect(page.getByRole('link', { name: 'View system health' })).toBeVisible()
    await expect(page.getByRole('complementary', { name: 'Application sidebar' })).toBeVisible()
  })

  test('returns to the query console from the recovery action', async ({ page }) => {
    await page.goto('/missing-feature-route')
    await page.getByRole('link', { name: 'Query Console' }).click()

    await expect(page).toHaveURL(/\/query$/)
    await expect(page.getByRole('heading', { level: 1, name: 'Query Console' })).toBeVisible()
  })
})
