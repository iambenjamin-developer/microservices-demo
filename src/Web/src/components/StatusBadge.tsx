import type { OrderStatus } from '../api/types'

/**
 * The order status is the whole point of the demo: `Pending` means the choreography is still running
 * (stock is being reserved), and the two terminal states are what Inventory answered.
 */
export function StatusBadge({ status }: { status: OrderStatus }) {
  return (
    <span className={`badge badge-${status.toLowerCase()}`}>
      {status === 'Pending' && <span className="badge-pulse" aria-hidden="true" />}
      {status}
    </span>
  )
}
