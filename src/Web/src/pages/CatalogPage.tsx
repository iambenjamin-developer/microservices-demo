import { useCallback, useState } from 'react'
import { getProducts, getStock } from '../api/endpoints'
import type { Product, Stock } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { useCart } from '../cart/useCart'
import { EmptyState } from '../components/EmptyState'
import { ErrorMessage } from '../components/ErrorMessage'
import { usePolledResource } from '../hooks/usePolledResource'
import { catalogCurrency, formatMoney, formatStyle, formatVolume } from '../lib/format'

/** Catalog and stock are owned by different services; the page is where the two views meet. */
interface CatalogEntry {
  product: Product
  stock: Stock | null
}

const stockRefreshMs = 10_000

export function CatalogPage() {
  const { call } = useAuth()

  const load = useCallback(
    async (signal: AbortSignal): Promise<CatalogEntry[]> => {
      // Two services, one page: nothing depends on the other, so they are asked at the same time.
      const [products, stock] = await Promise.all([
        getProducts(call, { signal }),
        getStock(call, { signal }),
      ])

      const bySku = new Map(stock.map((item) => [item.sku, item]))
      return products.map((product) => ({ product, stock: bySku.get(product.sku) ?? null }))
    },
    [call],
  )

  const { data, isLoading, error, refresh } = usePolledResource(load, { intervalMs: stockRefreshMs })

  if (error !== null && data === null) {
    return <ErrorMessage message={error} onRetry={refresh} />
  }

  return (
    <section>
      <header className="page-header">
        <div>
          <h1>Catalog</h1>
          <p className="muted">Prices are per pack. Availability comes from the Inventory service.</p>
        </div>
      </header>

      {isLoading && data === null && <p className="muted">Loading the catalog…</p>}

      <div className="product-grid">
        {data?.map((entry) => (
          <ProductCard key={entry.product.id} entry={entry} />
        ))}
      </div>

      {data?.length === 0 && <EmptyState title="The catalog is empty." />}
    </section>
  )
}

function ProductCard({ entry }: { entry: CatalogEntry }) {
  const { add } = useCart()
  const [quantity, setQuantity] = useState(1)
  const [added, setAdded] = useState(false)
  const available = entry.stock?.quantityAvailable ?? null

  const handleAdd = () => {
    add(entry.product, quantity)
    setAdded(true)
    setQuantity(1)
    window.setTimeout(() => setAdded(false), 1500)
  }

  return (
    <article className="card product-card">
      <header>
        <h2>{entry.product.name}</h2>
        <p className="muted">
          {formatStyle(entry.product.style)} · {formatVolume(entry.product.volumeMl)} ·{' '}
          {entry.product.packSize} units per pack
        </p>
        <code className="sku">{entry.product.sku}</code>
      </header>

      <p className="price">{formatMoney(entry.product.price, catalogCurrency)}</p>

      <p className={availableClassName(available)}>
        {available === null
          ? 'Availability unknown'
          : available === 0
            ? 'Out of stock'
            : `${available} packs available`}
      </p>

      <div className="product-actions">
        <label className="sr-only" htmlFor={`quantity-${entry.product.sku}`}>
          Packs of {entry.product.name}
        </label>
        <input
          id={`quantity-${entry.product.sku}`}
          type="number"
          min={1}
          max={1000}
          value={quantity}
          onChange={(event) => setQuantity(Number(event.target.value))}
        />
        <button type="button" className="primary" onClick={handleAdd} disabled={quantity < 1}>
          {added ? 'Added' : 'Add to cart'}
        </button>
      </div>
    </article>
  )
}

function availableClassName(available: number | null): string {
  if (available === null) {
    return 'muted'
  }

  // Ordering an amount the warehouse cannot cover is a valid thing to do — the order comes back
  // Rejected, which is the compensation path the demo shows on purpose.
  return available === 0 ? 'stock stock-out' : available < 10 ? 'stock stock-low' : 'stock'
}
