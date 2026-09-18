import { useCallback } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getOrder } from '../api/endpoints'
import { useAuth } from '../auth/useAuth'
import { ErrorMessage } from '../components/ErrorMessage'
import { StatusBadge } from '../components/StatusBadge'
import { usePolledResource } from '../hooks/usePolledResource'
import { formatDateTime, formatMoney } from '../lib/format'

const pendingRefreshMs = 2_000

/**
 * Where the asynchronous flow becomes visible: the order was accepted (202) as `Pending`, and the
 * status changes here on its own once Inventory answers and Ordering confirms or rejects it.
 */
export function OrderDetailsPage() {
  const { call } = useAuth()
  const { id = '' } = useParams<{ id: string }>()

  const load = useCallback((signal: AbortSignal) => getOrder(call, id, { signal }), [call, id])
  const { data: order, isLoading, error, refresh } = usePolledResource(load, {
    intervalMs: pendingRefreshMs,
    isFinal: (current) => current.status !== 'Pending',
  })

  if (error !== null && order === null) {
    return <ErrorMessage message={error} onRetry={refresh} />
  }

  if (order === null) {
    return <p className="muted">{isLoading ? 'Loading the order…' : 'Order not found.'}</p>
  }

  return (
    <section>
      <header className="page-header">
        <div>
          <Link to="/orders">← All orders</Link>
          <h1>Order {order.id.slice(0, 8)}</h1>
          <p className="muted">Placed {formatDateTime(order.placedOnUtc)}</p>
        </div>
        <StatusBadge status={order.status} />
      </header>

      {order.status === 'Pending' && (
        <p className="notice notice-info">
          Waiting for the warehouse to reserve the stock. This page updates itself.
        </p>
      )}

      {order.status === 'Rejected' && (
        <p className="notice notice-error" role="alert">
          {order.rejectionReason ?? 'The order was rejected.'}
        </p>
      )}

      {order.status === 'Confirmed' && order.completedOnUtc !== null && (
        <p className="notice notice-success">
          Stock reserved and confirmed {formatDateTime(order.completedOnUtc)}.
        </p>
      )}

      <table className="table">
        <thead>
          <tr>
            <th>Product</th>
            <th className="numeric">Price per pack</th>
            <th className="numeric">Packs</th>
            <th className="numeric">Line total</th>
          </tr>
        </thead>
        <tbody>
          {order.items.map((item) => (
            <tr key={item.sku}>
              <td>
                <strong>{item.productName}</strong>
                <br />
                <code className="sku">{item.sku}</code>
              </td>
              <td className="numeric">{formatMoney(item.unitPrice, order.currency)}</td>
              <td className="numeric">{item.quantity}</td>
              <td className="numeric">{formatMoney(item.lineTotal, order.currency)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="card totals">
        <p>
          <span>Subtotal</span>
          <span>{formatMoney(order.subtotal, order.currency)}</span>
        </p>
        <p>
          <span>Volume discount</span>
          <span>−{formatMoney(order.discount, order.currency)}</span>
        </p>
        <p className="total">
          <span>Total</span>
          <strong>{formatMoney(order.total, order.currency)}</strong>
        </p>
      </div>
    </section>
  )
}
