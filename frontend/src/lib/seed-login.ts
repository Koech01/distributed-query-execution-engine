export type SeedLoginAccount = {
  label: string
  email: string
  password: string
}

export function isSeedLoginPrefillEnabled(): boolean {
  return import.meta.env.VITE_SEED_LOGIN_PREFILL_ENABLED === 'true'
}

function readSeedCredential(
  email: string | undefined,
  password: string | undefined,
): Pick<SeedLoginAccount, 'email' | 'password'> | null {
  if (typeof email !== 'string' || typeof password !== 'string') {
    return null
  }

  const trimmedEmail = email.trim()
  const trimmedPassword = password.trim()
  if (!trimmedEmail || !trimmedPassword) {
    return null
  }

  return {
    email: trimmedEmail,
    password: trimmedPassword,
  }
}

export function getSeedLoginAccounts(): SeedLoginAccount[] {
  if (!isSeedLoginPrefillEnabled()) {
    return []
  }

  const accounts: SeedLoginAccount[] = []

  const admin = readSeedCredential(
    import.meta.env.VITE_SEED_LOGIN_ADMIN_EMAIL,
    import.meta.env.VITE_SEED_LOGIN_ADMIN_PASSWORD,
  )
  if (admin) {
    accounts.push({ label: 'Admin', ...admin })
  }

  const user = readSeedCredential(
    import.meta.env.VITE_SEED_LOGIN_USER_EMAIL,
    import.meta.env.VITE_SEED_LOGIN_USER_PASSWORD,
  )
  if (user) {
    accounts.push({ label: 'Standard user', ...user })
  }

  return accounts
}

export function getDefaultSeedLoginPrefill(): Pick<SeedLoginAccount, 'email' | 'password'> | null {
  const account = getSeedLoginAccounts()[0]
  if (!account) {
    return null
  }

  return {
    email: account.email,
    password: account.password,
  }
}