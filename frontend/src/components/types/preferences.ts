import type { FailurePolicy } from './query'

export type ThemePreference = 'light' | 'dark' | 'system'

export interface UserPreferences {
  theme: ThemePreference
  defaultTimeoutSeconds: number
  defaultFailurePolicy: FailurePolicy
  defaultAsync: boolean
  saveSqlInHistory: boolean
}

export const DEFAULT_USER_PREFERENCES: UserPreferences = {
  theme: 'system',
  defaultTimeoutSeconds: 30,
  defaultFailurePolicy: 'BestEffort',
  defaultAsync: false,
  saveSqlInHistory: false,
}