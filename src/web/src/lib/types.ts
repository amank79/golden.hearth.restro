export type TaxMode = 'Regular' | 'Composition'

export interface Settings {
  name: string
  address: string
  phone: string
  gstin: string
  fssaiNo: string
  taxMode: TaxMode
  gstRateBp: number
  billFooter: string
  /** Tables shown on the billing screen, in order. */
  tables: string[]
  /** Windows printer bills are sent to directly; empty when bills are printed through the browser. */
  printerName?: string
}

/** The printers Windows has, and the one bills go to ("" = none). */
export interface Printers {
  printers: string[]
  current: string
}

export type FoodType = 'Veg' | 'NonVeg' | 'Egg'

export interface Category {
  id: number
  name: string
  sortOrder: number
  isActive: boolean
}

export interface Variant {
  id: number
  name: string
  pricePaise: number
  sortOrder: number
}

export interface MenuItem {
  id: number
  categoryId: number
  name: string
  shortCode: string | null
  description: string | null
  foodType: FoodType
  gstRateBp: number | null
  isAvailable: boolean
  isActive: boolean
  sortOrder: number
  variants: Variant[]
}

export interface Menu {
  categories: Category[]
  items: MenuItem[]
}

export interface MenuItemInput {
  categoryId: number
  name: string
  shortCode: string | null
  description: string | null
  foodType: FoodType
  gstRateBp: number | null
  isAvailable: boolean
  sortOrder: number | null
  variants: { id: number | null; name: string | null; pricePaise: number }[]
}

export type OrderType = 'DineIn' | 'Takeaway'
export type BillStatus = 'Open' | 'Paid' | 'Cancelled'
export type PaymentMethod = 'Cash' | 'Upi' | 'Card'
export type DiscountKind = 'None' | 'Percent' | 'Amount'

export const PAYMENT_METHODS: { value: PaymentMethod; label: string }[] = [
  { value: 'Cash', label: 'Cash' },
  { value: 'Upi', label: 'UPI' },
  { value: 'Card', label: 'Card' },
]

export interface BillLine {
  id: number
  menuItemId: number
  itemVariantId: number
  itemName: string
  variantName: string
  unitPricePaise: number
  qty: number
  note: string | null
  gstRateBp: number
  amountPaise: number
}

export interface TaxGroup {
  gstRateBp: number
  halfRateBp: number
  taxablePaise: number
  cgstPaise: number
  sgstPaise: number
}

export interface Payment {
  method: PaymentMethod
  amountPaise: number
  at: string
}

export interface Bill {
  id: number
  publicId: string
  billNo: string | null
  financialYear: string | null
  orderType: OrderType
  tableLabel: string | null
  status: BillStatus
  isFinalised: boolean
  openedAt: string
  finalisedAt: string | null
  settledAt: string | null
  cancelledAt: string | null
  cancelReason: string | null
  taxMode: TaxMode
  documentTitle: string
  lines: BillLine[]
  discountKind: DiscountKind
  discountValue: number
  discountReason: string | null
  subtotalPaise: number
  discountPaise: number
  taxablePaise: number
  taxGroups: TaxGroup[]
  cgstPaise: number
  sgstPaise: number
  roundOffPaise: number
  totalPaise: number
  payments: Payment[]
  paidPaise: number
  balancePaise: number
  printCount: number
}

export interface BillSummary {
  id: number
  billNo: string | null
  orderType: OrderType
  tableLabel: string | null
  status: BillStatus
  openedAt: string
  finalisedAt: string | null
  settledAt: string | null
  itemCount: number
  totalPaise: number
  paymentMethods: PaymentMethod[]
  cancelReason: string | null
}

export interface BillPage {
  items: BillSummary[]
  total: number
}

export interface TodaySummary {
  date: string
  billCount: number
  totalPaise: number
  paidPaise: number
  unpaidPaise: number
  byMethod: { method: PaymentMethod; amountPaise: number }[]
  cancelledCount: number
  cancelledPaise: number
  openCount: number
}

export interface CancelResult {
  cancelled: Bill
  newBill: Bill | null
}

export interface PrintTextLine {
  text: string
  bold: boolean
  /** Printed at twice the normal size. */
  big: boolean
}

export interface PrintOutput {
  bill: Bill
  duplicate: boolean
  isDraft: boolean
  text: string
  escPosBase64: string
  /** The bill line by line as the browser prints it (42 characters; large lines hold 21). */
  lines: PrintTextLine[]
  /** The printer the bill was sent to, or null if it was not sent to one. */
  printedTo?: string | null
  /** Why the bill printer did not print it (the bill is still numbered). */
  printerError?: string | null
}

export interface BackupResult {
  success: boolean
  fileName: string | null
  at: string
  messages: string[]
}

export interface BackupStatus {
  /** Local laptop time of the newest backup file, or null if there is none. */
  newestBackupAt: string | null
  overdue: boolean
  folder: string
  extraFolders: { path: string; connected: boolean }[]
  lastResult: BackupResult | null
}
