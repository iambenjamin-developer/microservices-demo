import { useCallback, useEffect, useMemo, useReducer, type ReactNode } from 'react'
import type { Product } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { CartContext, type CartContextValue, type CartLine } from './CartContext'

/**
 * The cart, plus the storage key it was read from. Keeping them together is what makes restoring the
 * cart safe: the lines are only written back once they are known to belong to the current key, so the
 * empty initial state can never overwrite what is stored (React mounts effects twice in development).
 */
interface CartState {
  storageKey: string | null
  lines: CartLine[]
}

type CartAction =
  | { type: 'load'; storageKey: string | null; lines: CartLine[] }
  | { type: 'add'; product: Product; quantity: number }
  | { type: 'setQuantity'; sku: string; quantity: number }
  | { type: 'remove'; sku: string }
  | { type: 'clear' }

/** Packs per line, matching the Quantity value object of the ordering domain (1–1000). */
const maxPacksPerLine = 1000

function reducer(state: CartState, action: CartAction): CartState {
  switch (action.type) {
    case 'load':
      return { storageKey: action.storageKey, lines: action.lines }
    case 'add': {
      const existing = state.lines.find((line) => line.sku === action.product.sku)
      const quantity = clamp((existing?.quantity ?? 0) + action.quantity)

      return withLines(
        state,
        existing
          ? state.lines.map((line) => (line.sku === existing.sku ? { ...line, quantity } : line))
          : [
              ...state.lines,
              {
                sku: action.product.sku,
                name: action.product.name,
                packSize: action.product.packSize,
                unitPrice: action.product.price,
                quantity,
              },
            ],
      )
    }
    case 'setQuantity':
      return withLines(
        state,
        action.quantity <= 0
          ? state.lines.filter((line) => line.sku !== action.sku)
          : state.lines.map((line) =>
              line.sku === action.sku ? { ...line, quantity: clamp(action.quantity) } : line,
            ),
      )
    case 'remove':
      return withLines(
        state,
        state.lines.filter((line) => line.sku !== action.sku),
      )
    case 'clear':
      return withLines(state, [])
  }
}

function withLines(state: CartState, lines: CartLine[]): CartState {
  return { ...state, lines }
}

function clamp(quantity: number): number {
  return Math.min(Math.max(Math.trunc(quantity), 1), maxPacksPerLine)
}

/**
 * The cart is client state: it becomes an order only when it is sent. It is kept per signed-in user
 * and in `sessionStorage`, so a reload does not lose it and the next point of sale to sign in on this
 * tab does not inherit it.
 */
export function CartProvider({ children }: { children: ReactNode }) {
  const { session } = useAuth()
  const storageKey = session === null ? null : `beer-demo.cart.${session.username}`
  const [state, dispatch] = useReducer(reducer, { storageKey: null, lines: [] })
  const isRestored = state.storageKey === storageKey

  useEffect(() => {
    if (!isRestored) {
      dispatch({ type: 'load', storageKey, lines: storageKey === null ? [] : read(storageKey) })
    }
  }, [storageKey, isRestored])

  useEffect(() => {
    if (storageKey !== null && isRestored) {
      sessionStorage.setItem(storageKey, JSON.stringify(state.lines))
    }
  }, [storageKey, isRestored, state.lines])

  const add = useCallback((product: Product, quantity: number) => {
    dispatch({ type: 'add', product, quantity })
  }, [])

  const setQuantity = useCallback((sku: string, quantity: number) => {
    dispatch({ type: 'setQuantity', sku, quantity })
  }, [])

  const remove = useCallback((sku: string) => dispatch({ type: 'remove', sku }), [])
  const clear = useCallback(() => dispatch({ type: 'clear' }), [])

  const value = useMemo<CartContextValue>(
    () => ({
      lines: state.lines,
      totalPacks: state.lines.reduce((total, line) => total + line.quantity, 0),
      estimatedSubtotal: state.lines.reduce((total, line) => total + line.quantity * line.unitPrice, 0),
      add,
      setQuantity,
      remove,
      clear,
    }),
    [state.lines, add, setQuantity, remove, clear],
  )

  return <CartContext value={value}>{children}</CartContext>
}

function read(storageKey: string): CartLine[] {
  const stored = sessionStorage.getItem(storageKey)
  if (stored === null) {
    return []
  }

  try {
    return JSON.parse(stored) as CartLine[]
  } catch {
    // A cart is not worth failing over; the customer adds the packs again.
    return []
  }
}
