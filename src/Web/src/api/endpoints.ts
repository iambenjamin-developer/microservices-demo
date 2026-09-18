import type { ApiRequest, RequestOptions } from './client'
import { request } from './client'
import type { Notification, Order, OrderSummary, Product, Stock, TokenResponse } from './types'

/**
 * Every call the app makes, in one file: the routes of the Gateway live here and nowhere else.
 * Reads take the caller's `request` (the authenticated one from the session) so the token is never
 * a hidden global; signing in is the exception — it is how a token is obtained in the first place.
 */

export const signIn = (username: string, password: string, signal?: AbortSignal): Promise<TokenResponse> =>
  request<TokenResponse>('/auth/token', { method: 'POST', body: { username, password }, signal })

export const getProducts = (call: ApiRequest, options?: RequestOptions): Promise<Product[]> =>
  call<Product[]>('/api/products', options)

export const getStock = (call: ApiRequest, options?: RequestOptions): Promise<Stock[]> =>
  call<Stock[]>('/api/stock', options)

export const getOrders = (call: ApiRequest, options?: RequestOptions): Promise<OrderSummary[]> =>
  call<OrderSummary[]>('/api/orders', options)

export const getOrder = (call: ApiRequest, id: string, options?: RequestOptions): Promise<Order> =>
  call<Order>(`/api/orders/${id}`, options)

export interface PlaceOrderItem {
  sku: string
  quantity: number
}

/**
 * Only SKUs and quantities are sent: Ordering snapshots the price from Catalog and takes the customer
 * from the token, so neither can be chosen by this app.
 */
export const placeOrder = (call: ApiRequest, items: PlaceOrderItem[]): Promise<Order> =>
  call<Order>('/api/orders', { method: 'POST', body: { items } })

export const getNotifications = (call: ApiRequest, options?: RequestOptions): Promise<Notification[]> =>
  call<Notification[]>('/api/notifications', options)
