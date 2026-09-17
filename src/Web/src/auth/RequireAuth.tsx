import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './useAuth'

/**
 * Keeps the signed-out visitor on the login page. This is convenience, not security: the services
 * reject an unauthenticated call regardless of what this app decides to render.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { session } = useAuth()
  const location = useLocation()

  if (session === null) {
    // Remember where the visitor was heading so signing in continues instead of starting over.
    return <Navigate to="/login" state={{ from: location.pathname }} replace />
  }

  return <>{children}</>
}
