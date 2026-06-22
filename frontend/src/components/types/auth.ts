export interface AuthUser {
  subject: string
  displayName?: string
  email?: string
  scopes: string[]
  roles: string[]
}

export interface JwtClaims {
  sub?: string
  name?: string
  email?: string
  scope?: string | string[]
  scp?: string | string[]
  roles?: string | string[]
  role?: string | string[]
  exp?: number
  iat?: number
  iss?: string
  aud?: string | string[]
}

export interface AuthState {
  user: AuthUser | null
  isAuthenticated: boolean
  isAuthEnabled: boolean
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  displayName: string
}

export interface ExchangeTokenRequest {
  exchangeCode: string
}

export interface AuthTokenResponse {
  accessToken: string
  expiresIn: number
  tokenType: string
}

export interface UserProfile {
  userId: string
  email: string
  displayName: string | null
  hasPasswordLogin: boolean
  linkedProviders: string[]
  scopes: string[]
  createdAt: string
  updatedAt: string
}

export interface UpdateProfileRequest {
  displayName?: string
  email?: string
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}

export interface UpdateProfileResponse {
  profile: UserProfile
  token: AuthTokenResponse | null
}

export type BackendOAuthProvider = 'google' | 'github'
