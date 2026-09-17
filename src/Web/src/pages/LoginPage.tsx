import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/useAuth'

/** The demo accounts the Gateway is configured with. They are published credentials, not secrets. */
const demoAccounts = [
  { username: 'bar', description: 'Corner Bar — point of sale' },
  { username: 'market', description: 'Riverside Market — point of sale' },
  { username: 'admin', description: 'Back office — catalog and stock' },
]

const demoPassword = 'demo'

export function LoginPage() {
  const { session, signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [username, setUsername] = useState('bar')
  const [password, setPassword] = useState(demoPassword)
  const [error, setError] = useState<string | null>(null)
  const [isSigningIn, setIsSigningIn] = useState(false)

  if (session !== null) {
    return <Navigate to="/catalog" replace />
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setIsSigningIn(true)

    try {
      await signIn(username, password)
      const from = (location.state as { from?: string } | null)?.from
      await navigate(from ?? '/catalog', { replace: true })
    } catch (failure) {
      // Wrong password and unknown user come back as the same 401 on purpose (no account enumeration).
      setError(failure instanceof ApiError ? failure.message : 'Sign in failed.')
    } finally {
      setIsSigningIn(false)
    }
  }

  return (
    <div className="login">
      <form className="card login-card" onSubmit={(event) => void handleSubmit(event)}>
        <h1>Beer Ordering</h1>
        <p className="muted">Sign in as a point of sale to browse the catalog and place orders.</p>

        <label htmlFor="username">User name</label>
        <input
          id="username"
          name="username"
          autoComplete="username"
          value={username}
          onChange={(event) => setUsername(event.target.value)}
          required
        />

        <label htmlFor="password">Password</label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          required
        />

        {error !== null && (
          <p className="notice notice-error" role="alert">
            {error}
          </p>
        )}

        <button type="submit" className="primary" disabled={isSigningIn}>
          {isSigningIn ? 'Signing in…' : 'Sign in'}
        </button>
      </form>

      <aside className="card login-accounts">
        <h2>Demo accounts</h2>
        <p className="muted">
          Every account uses the password <code>{demoPassword}</code>.
        </p>
        <ul>
          {demoAccounts.map((account) => (
            <li key={account.username}>
              <button
                type="button"
                className="link-button"
                onClick={() => {
                  setUsername(account.username)
                  setPassword(demoPassword)
                }}
              >
                {account.username}
              </button>
              <span className="muted"> — {account.description}</span>
            </li>
          ))}
        </ul>
      </aside>
    </div>
  )
}
