import { test as base, expect } from '@playwright/test'

import { installApiMocks, shouldMockApi } from './api-mocks'

export const test = base.extend({
  page: async ({ page }, use) => {
    if (shouldMockApi()) {
      await installApiMocks(page)
    }

    await use(page)
  },
})

export { expect }
