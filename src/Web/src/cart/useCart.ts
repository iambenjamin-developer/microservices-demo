import { use } from 'react'
import { CartContext, type CartContextValue } from './CartContext'

export function useCart(): CartContextValue {
  const context = use(CartContext)
  if (context === null) {
    throw new Error('useCart must be used inside a CartProvider.')
  }

  return context
}
