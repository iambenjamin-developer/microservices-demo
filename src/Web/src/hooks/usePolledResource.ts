import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../api/client'

export interface PolledResource<T> {
  data: T | null
  /** True only while there is nothing to show yet; a refresh in the background does not blank the page. */
  isLoading: boolean
  error: string | null
  refresh: () => void
}

export interface PolledResourceOptions<T> {
  /** How often to ask again. `null` loads once, which is all a page with static data needs. */
  intervalMs?: number | null
  /**
   * Answers "is this the last word?". Polling stops as soon as it returns true, so an order that
   * reached a terminal state does not keep costing a request every couple of seconds.
   */
  isFinal?: (data: T) => boolean
}

/**
 * Loads data and keeps it fresh by asking again.
 *
 * Polling is the honest client for this backend: placing an order answers 202 Accepted and the
 * outcome is decided later, by other services, over the message bus. Nothing can push that change to
 * the browser today, so the app asks — and stops as soon as the answer is final. Server-sent events
 * or SignalR would remove both the delay and the wasted calls; they need a component that holds the
 * connection and knows which customer to notify, which this MVP does not have.
 *
 * `load` must be stable (wrap it in `useCallback`): it is what decides when polling restarts.
 */
export function usePolledResource<T>(
  load: (signal: AbortSignal) => Promise<T>,
  { intervalMs = null, isFinal }: PolledResourceOptions<T> = {},
): PolledResource<T> {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [reloadCount, setReloadCount] = useState(0)

  // Kept in a ref so that passing an inline predicate does not restart the polling on every render.
  const isFinalRef = useRef(isFinal)
  useEffect(() => {
    isFinalRef.current = isFinal
  })

  const refresh = useCallback(() => setReloadCount((count) => count + 1), [])

  useEffect(() => {
    const controller = new AbortController()
    let timer: number | undefined

    const run = async () => {
      try {
        const result = await load(controller.signal)
        if (controller.signal.aborted) {
          return
        }

        setData(result)
        setError(null)

        if (isFinalRef.current?.(result) === true && timer !== undefined) {
          clearInterval(timer)
        }
      } catch (failure) {
        if (controller.signal.aborted || failure instanceof DOMException) {
          return
        }

        setError(failure instanceof ApiError ? failure.message : 'Something went wrong.')
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false)
        }
      }
    }

    void run()

    if (intervalMs !== null) {
      timer = window.setInterval(() => {
        // A tab nobody is looking at does not need fresh data; it catches up when it is shown again.
        if (!document.hidden) {
          void run()
        }
      }, intervalMs)
    }

    return () => {
      if (timer !== undefined) {
        clearInterval(timer)
      }

      controller.abort()
    }
  }, [load, intervalMs, reloadCount])

  return { data, isLoading, error, refresh }
}
