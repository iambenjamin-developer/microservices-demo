import { createContext } from 'react'
import type { Product } from '../api/types'

/** A line of the cart. The price is the one the catalog showed, and is only used for the estimate. */
export interface CartLine {
  sku: string
  name: string
  packSize: number
  unitPrice: number
  /** Packs, which is the unit the whole system counts in. */
  quantity: number
}

export interface CartContextValue {
  lines: CartLine[]
  totalPacks: number
  /**
   * What the cart is expected to cost, from the prices the catalog showed. The order total comes from
   * the server: it snapshots the price again and applies the volume discount, so this is an estimate
   * on purpose — the pricing rules are not duplicated here.
   */
  estimatedSubtotal: number
  add: (product: Product, quantity: number) => void
  setQuantity: (sku: string, quantity: number) => void
  remove: (sku: string) => void
  clear: () => void
}

export const CartContext = createContext<CartContextValue | null>(null)
