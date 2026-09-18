import { useCallback } from 'react'
import { Link } from 'react-router-dom'
import { getOrders } from '../api/endpoints'
import { useAuth } from '../auth/useAuth'
import { EmptyState } from '../components/EmptyState'
import { ErrorMessage } from '../components/ErrorMessage'
import { StatusBadge } from '../components/StatusBadge'
import { usePolledResource } from '../hooks/usePolledResource'
import { formatDateTime, formatMoney } from '../lib/format'

const pendingRefreshMs = 2_000

export function OrdersPage() {
  const { call } = useAuth()

  const load = useCallback((signal: AbortSignal) => getOrders(call, { signal }), [call])
  // Only an order waiting for Inventory can still change, so the list stops polling as soon as
  // every order has reached a terminal state.
  const { data, isLoading, error, refresh } = usePolledResource(load, {
    intervalMs: pendingRefreshMs,
    isFinal: (orders) => !orders.some((order) => order.status === 'Pending'),
  })

  if (error !== null && data === null) {
    return <ErrorMessage message={error} onRetry={refresh} />
  }

  if (isLoading && data === null) {
    return <p className="muted">Loading your orders…</p>
  }

  if (data?.length === 0) {
    return (
      <EmptyState title="You have not placed any orders yet.">
        <Link to="/catalog">Browse the catalog</Link>
      </EmptyState>
    )
  }

  return (
    <section>
      <header className="page-header">
        <h1>Orders</h1>
        <p className="muted">Newest first. Pending orders refresh on their own.</p>
      </header>

      <table className="table">
        <thead>
          <tr>
            <th>Placed</th>
            <th>Status</th>
            <th className="numeric">Items</th>
            <th className="numeric">Total</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {data?.map((order) => (
            <tr key={order.id}>
              <td>{formatDateTime(order.placedOnUtc)}</td>
              <td>
                <StatusBadge status={order.status} />
              </td>
              <td className="numeric">{order.itemCount}</td>
              <td className="numeric">{formatMoney(order.total, order.currency)}</td>
              <td>
                <Link to={`/orders/${order.id}`}>Details</Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}
