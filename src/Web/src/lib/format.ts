import type { BeerStyle } from '../api/types'

/**
 * The backend is `InvariantGlobalization`, so the app formats with a fixed locale too: the same order
 * reads the same way on every machine, which matters when the demo runs on somebody else's laptop.
 */
const locale = 'en-US'

export function formatMoney(amount: number, currency: string): string {
  return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount)
}

export function formatDateTime(isoDate: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(isoDate),
  )
}

/** The enum values are PascalCase on the wire; this is only how they are shown. */
export function formatStyle(style: BeerStyle): string {
  return style === 'Ipa' ? 'IPA' : style.replace(/([a-z])([A-Z])/g, '$1 $2')
}

export function formatVolume(volumeMl: number): string {
  return volumeMl >= 1000 ? `${volumeMl / 1000} L` : `${volumeMl} ml`
}

/**
 * Catalog prices are plain numbers: the currency is a decision of the Ordering service (section
 * `Pricing`), and it comes back on every order. Until an order exists this app has to name one, so it
 * assumes the configured default; the totals shown for an order always use the currency it carries.
 */
export const catalogCurrency = 'USD'
