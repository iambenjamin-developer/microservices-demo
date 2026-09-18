import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useCart } from '../cart/useCart'

/** Shell shared by every signed-in page: navigation, who is signed in, and the way out. */
export function AppLayout() {
  const { session, signOut } = useAuth()
  const { totalPacks } = useCart()
  const navigate = useNavigate()

  const handleSignOut = () => {
    signOut()
    void navigate('/login', { replace: true })
  }

  return (
    <div className="app">
      <header className="app-header">
        <div className="brand">
          <img src="/favicon.svg" alt="" width="28" height="28" />
          <span>Beer Ordering</span>
        </div>

        <nav className="app-nav">
          <NavLink to="/catalog">Catalog</NavLink>
          <NavLink to="/cart">
            Cart
            {totalPacks > 0 && <span className="pill">{totalPacks}</span>}
          </NavLink>
          <NavLink to="/orders">Orders</NavLink>
          <NavLink to="/notifications">Notifications</NavLink>
        </nav>

        <div className="app-user">
          <span className="app-user-name">{session?.displayName}</span>
          <span className="app-user-role">{session?.role}</span>
          <button type="button" className="link-button" onClick={handleSignOut}>
            Sign out
          </button>
        </div>
      </header>

      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
