/**
 * The responses this app consumes, mirroring the contracts the services expose.
 * Enums are serialized as strings by every service, so they are string unions here.
 */

export type BeerStyle = 'Lager' | 'Pilsner' | 'Wheat' | 'AmberAle' | 'Ipa' | 'Stout'

export interface Product {
  id: string
  sku: string
  name: string
  style: BeerStyle
  volumeMl: number
  packSize: number
  /** Price of one pack, in the currency the Ordering service is configured with. */
  price: number
}

export interface Stock {
  sku: string
  quantityAvailable: number
  quantityReserved: number
  quantityOnHand: number
  updatedOnUtc: string
}

export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected'

export interface OrderItem {
  sku: string
  productName: string
  unitPrice: number
  quantity: number
  lineTotal: number
}

export interface Order {
  id: string
  customerId: string
  status: OrderStatus
  items: OrderItem[]
  subtotal: number
  discount: number
  total: number
  currency: string
  rejectionReason: string | null
  placedOnUtc: string
  completedOnUtc: string | null
}

export interface OrderSummary {
  id: string
  status: OrderStatus
  itemCount: number
  total: number
  currency: string
  placedOnUtc: string
  completedOnUtc: string | null
}

export type NotificationType = 'OrderConfirmed' | 'OrderRejected'
export type EmailStatus = 'Pending' | 'Skipped' | 'Sent' | 'Failed'

export interface Notification {
  id: string
  orderId: string
  type: NotificationType
  title: string
  body: string
  occurredOnUtc: string
  createdOnUtc: string
  emailStatus: EmailStatus
  emailSentOnUtc: string | null
}

export interface TokenResponse {
  accessToken: string
  tokenType: string
  expiresIn: number
  username: string
  email: string
  role: string
  displayName: string
}
