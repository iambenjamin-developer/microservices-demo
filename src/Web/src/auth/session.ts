import type { TokenResponse } from '../api/types'

/** The signed-in point of sale, as this app remembers it between page loads. */
export interface Session {
  accessToken: string
  username: string
  email: string
  role: string
  displayName: string
  /** When the token stops being accepted, so an expired session is dropped before it is used. */
  expiresAtEpochMs: number
}

export const Roles = {
  PointOfSale: 'PointOfSale',
  Admin: 'Admin',
} as const

const storageKey = 'beer-demo.session'

/**
 * The token lives in `sessionStorage`: it is gone when the tab closes and it is not shared with other
 * tabs, which keeps the demo honest about the token being short-lived. A production system would put
 * it in an http-only, same-site cookie so that a script injected into this page cannot read it at all
 * — that requires the token to be issued for a browser client, which the demo endpoint is not.
 */
export function loadSession(): Session | null {
  const stored = sessionStorage.getItem(storageKey)
  if (stored === null) {
    return null
  }

  try {
    const session = JSON.parse(stored) as Session
    return isExpired(session) ? null : session
  } catch {
    // Anything unreadable is treated as "not signed in" instead of crashing the app on start.
    sessionStorage.removeItem(storageKey)
    return null
  }
}

export function saveSession(session: Session): void {
  sessionStorage.setItem(storageKey, JSON.stringify(session))
}

export function clearSession(): void {
  sessionStorage.removeItem(storageKey)
}

export function toSession(token: TokenResponse, nowEpochMs: number): Session {
  return {
    accessToken: token.accessToken,
    username: token.username,
    email: token.email,
    role: token.role,
    displayName: token.displayName,
    expiresAtEpochMs: nowEpochMs + token.expiresIn * 1000,
  }
}

export function isExpired(session: Session): boolean {
  return session.expiresAtEpochMs <= Date.now()
}
