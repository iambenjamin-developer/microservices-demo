import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError, validationMessages } from '../api/client'
import { placeOrder } from '../api/endpoints'
import { useAuth } from '../auth/useAuth'
import { useCart } from '../cart/useCart'
import { EmptyState } from '../components/EmptyState'
import { catalogCurrency, formatMoney } from '../lib/format'

export function CartPage() {
  const { call } = useAuth()
  const { lines, totalPacks, estimatedSubtotal, setQuantity, remove, clear } = useCart()
  const navigate = useNavigate()
  const [errors, setErrors] = useState<string[]>([])
  const [isPlacing, setIsPlacing] = useState(false)

  const handleCheckout = async () => {
    setErrors([])
    setIsPlacing(true)

    try {
      const order = await placeOrder(
        call,
        lines.map((line) => ({ sku: line.sku, quantity: line.quantity })),
      )

      // The order exists (202 Accepted) but is still Pending: the order page is where its outcome shows up.
      clear()
      await navigate(`/orders/${order.id}`)
    } catch (failure) {
      setErrors(toMessages(failure))
    } finally {
      setIsPlacing(false)
    }
  }

  if (lines.length === 0) {
    return (
      <EmptyState title="Your cart is empty.">
        <Link to="/catalog">Browse the catalog</Link>
      </EmptyState>
    )
  }

  return (
    <section>
      <header className="page-header">
        <h1>Cart</h1>
      </header>

      <table className="table">
        <thead>
          <tr>
            <th>Product</th>
            <th className="numeric">Price per pack</th>
            <th className="numeric">Packs</th>
            <th className="numeric">Line total</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {lines.map((line) => (
            <tr key={line.sku}>
              <td>
                <strong>{line.name}</strong>
                <br />
                <code className="sku">{line.sku}</code>
              </td>
              <td className="numeric">{formatMoney(line.unitPrice, catalogCurrency)}</td>
              <td className="numeric">
                <label className="sr-only" htmlFor={`cart-quantity-${line.sku}`}>
                  Packs of {line.name}
                </label>
                <input
                  id={`cart-quantity-${line.sku}`}
                  type="number"
                  min={1}
                  max={1000}
                  value={line.quantity}
                  onChange={(event) => setQuantity(line.sku, Number(event.target.value))}
                />
              </td>
              <td className="numeric">
                {formatMoney(line.unitPrice * line.quantity, catalogCurrency)}
              </td>
              <td>
                <button type="button" className="link-button" onClick={() => remove(line.sku)}>
                  Remove
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="card cart-summary">
        <p>
          <span>{totalPacks} packs</span>
          <strong>{formatMoney(estimatedSubtotal, catalogCurrency)}</strong>
        </p>
        <p className="muted">
          Estimated from the catalog prices. The order total is calculated by the Ordering service,
          which snapshots the price again and applies the volume discount.
        </p>

        {errors.length > 0 && (
          <ul className="notice notice-error" role="alert">
            {errors.map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        )}

        <div className="cart-actions">
          <button type="button" className="link-button" onClick={clear}>
            Empty cart
          </button>
          <button
            type="button"
            className="primary"
            onClick={() => void handleCheckout()}
            disabled={isPlacing}
          >
            {isPlacing ? 'Placing the order…' : 'Place order'}
          </button>
        </div>
      </div>
    </section>
  )
}

function toMessages(failure: unknown): string[] {
  if (!(failure instanceof ApiError)) {
    return ['The order could not be placed.']
  }

  // A 400 carries the field errors of the validator; everything else (e.g. 503 when Catalog is
  // unreachable) has a single description written for a human.
  const fieldErrors = validationMessages(failure)
  return fieldErrors.length > 0 ? fieldErrors : [failure.message]
}
