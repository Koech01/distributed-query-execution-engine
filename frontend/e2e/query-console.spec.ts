import { test, expect } from './fixtures/test'

test.describe('Query console', () => {
  test('loads the workspace with an accessible SQL editor', async ({ page }) => {
    await page.goto('/query')

    await expect(page.getByRole('heading', { level: 1, name: 'Query Console' })).toBeVisible()
    await expect(page.getByLabel('SQL query')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Execute' })).toBeEnabled()
  })

  test('submits a sync query and renders tabular results', async ({ page }) => {
    await page.goto('/query')

    await page.getByRole('button', { name: 'Execute' }).click()

    const resultsTable = page.getByLabel('Query results table')
    await expect(page.getByRole('columnheader', { name: 'id' })).toBeVisible({ timeout: 15_000 })
    await expect(page.getByRole('columnheader', { name: 'name' })).toBeVisible()
    await expect(page.getByRole('region', { name: 'Query execution metadata' })).toBeVisible()
    await expect(resultsTable.getByText('Ada', { exact: true })).toBeVisible()
    await expect(resultsTable.getByText('Grace', { exact: true })).toBeVisible()
  })

  test('submits an async query, polls status, and renders terminal results', async ({ page }) => {
    await page.goto('/query')

    await page.getByLabel('Run asynchronously').click()
    await page.getByRole('button', { name: 'Execute' }).click()

    await expect(page.getByRole('status', { name: 'Async query status' })).toContainText('Async query completed', {
      timeout: 15_000,
    })
    const resultsTable = page.getByLabel('Query results table')
    await expect(page.getByRole('columnheader', { name: 'id' })).toBeVisible()
    await expect(resultsTable.getByText('Ada', { exact: true })).toBeVisible()
  })
})
