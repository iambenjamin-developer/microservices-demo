import { useCallback, useMemo, useState, type ReactNode } from 'react'
import { ApiError, request, type RequestOptions } from '../api/client'
import { signIn as requestToken } from '../api/endpoints'
import { AuthContext, type AuthContextValue } from './AuthContext'
import { clearSession, isExpired, loadSession, saveSession, toSession, type Session } from './session'

/** Owns the session: signing in, signing out, and every call made on the customer's behalf. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(loadSession)

  const signOut = useCallback(() => {
    clearSession()
    setSession(null)
  }, [])

  const signIn = useCallback(async (username: string, password: string) => {
    const token = await requestToken(username, password)
    const newSession = toSession(token, Date.now())

    saveSession(newSession)
    setSession(newSession)
  }, [])

  const call = useCallback(
    async <T,>(path: string, options: RequestOptions = {}): Promise<T> => {
      if (session === null) {
        throw new ApiError(401, 'You are not signed in.')
      }

      // A token this app already knows is past its lifetime is not worth a round trip.
      if (isExpired(session)) {
        signOut()
        throw new ApiError(401, 'Your session expired. Please sign in again.')
      }

      try {
        return await request<T>(path, { ...options, accessToken: session.accessToken })
      } catch (error) {
        // The services are the ones that decide whether a token is still good; a 401 from any of
        // them ends the session here, so the app never keeps retrying with a token nobody accepts.
        if (error instanceof ApiError && error.isUnauthenticated) {
          signOut()
        }

        throw error
      }
    },
    [session, signOut],
  )

  const value = useMemo<AuthContextValue>(
    () => ({ session, signIn, signOut, call }),
    [session, signIn, signOut, call],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}
