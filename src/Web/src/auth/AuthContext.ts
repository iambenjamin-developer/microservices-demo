import { createContext } from 'react'
import type { ApiRequest } from '../api/client'
import type { Session } from './session'

export interface AuthContextValue {
  session: Session | null
  signIn: (username: string, password: string) => Promise<void>
  signOut: () => void
  /**
   * Sends a request as the signed-in point of sale. It is handed to the API functions instead of the
   * raw token so that adding the header and reacting to a rejected token happen in exactly one place.
   */
  call: ApiRequest
}

export const AuthContext = createContext<AuthContextValue | null>(null)
