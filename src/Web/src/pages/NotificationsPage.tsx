import { useCallback } from 'react'
import { getNotifications } from '../api/endpoints'
import type { EmailStatus } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { EmptyState } from '../components/EmptyState'
import { ErrorMessage } from '../components/ErrorMessage'
import { usePolledResource } from '../hooks/usePolledResource'
import { formatDateTime } from '../lib/format'

const refreshMs = 5_000

/**
 * The end of the choreography: the Notifications function records every order outcome it receives and
 * this panel shows the ones that belong to the signed-in point of sale (the service scopes them by the
 * `sub` claim — there is no endpoint that takes a customer id).
 */
export function NotificationsPage() {
  const { call } = useAuth()

  const load = useCallback((signal: AbortSignal) => getNotifications(call, { signal }), [call])
  const { data, isLoading, error, refresh } = usePolledResource(load, { intervalMs: refreshMs })

  if (error !== null && data === null) {
    return <ErrorMessage message={error} onRetry={refresh} />
  }

  if (isLoading && data === null) {
    return <p className="muted">Loading your notifications…</p>
  }

  if (data?.length === 0) {
    return (
      <EmptyState title="No notifications yet.">
        <p className="muted">Place an order and its outcome shows up here.</p>
      </EmptyState>
    )
  }

  return (
    <section>
      <header className="page-header">
        <h1>Notifications</h1>
        <p className="muted">Newest first, refreshed every few seconds.</p>
      </header>

      <ul className="notification-list">
        {data?.map((notification) => (
          <li key={notification.id} className="card notification">
            <header>
              <h2>{notification.title}</h2>
              <span className={`badge badge-${notification.type === 'OrderConfirmed' ? 'confirmed' : 'rejected'}`}>
                {notification.type}
              </span>
            </header>
            <p>{notification.body}</p>
            <footer className="muted">
              <span>{formatDateTime(notification.createdOnUtc)}</span>
              <span>{describeEmail(notification.emailStatus)}</span>
            </footer>
          </li>
        ))}
      </ul>
    </section>
  )
}

/** The e-mail is a second channel: the notification is stored either way, and says how that went. */
function describeEmail(status: EmailStatus): string {
  switch (status) {
    case 'Sent':
      return 'E-mail sent'
    case 'Skipped':
      return 'E-mail disabled'
    case 'Failed':
      return 'E-mail could not be delivered'
    case 'Pending':
      return 'E-mail pending'
  }
}
