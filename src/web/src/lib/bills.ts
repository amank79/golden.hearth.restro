import type { BillSummary, PaymentMethod } from './types'

/** "Table 4" or "Takeaway #57". */
export function billLabel(b: Pick<BillSummary, 'orderType' | 'tableLabel' | 'id'>): string {
  return b.orderType === 'DineIn' ? `Table ${b.tableLabel}` : `Takeaway #${b.id}`
}

export function methodLabel(m: PaymentMethod): string {
  return m === 'Upi' ? 'UPI' : m
}

const timeFormat = new Intl.DateTimeFormat('en-IN', { timeZone: 'Asia/Kolkata', hour: '2-digit', minute: '2-digit' })
const dateTimeFormat = new Intl.DateTimeFormat('en-IN', {
  timeZone: 'Asia/Kolkata',
  day: '2-digit',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
})

/** Times are shown in India time, like the printed bill. */
export function time(iso: string): string {
  return timeFormat.format(new Date(iso))
}

export function dateTime(iso: string): string {
  return dateTimeFormat.format(new Date(iso))
}

export function minutesAgo(iso: string, now = Date.now()): string {
  const m = Math.max(0, Math.round((now - new Date(iso).getTime()) / 60000))
  if (m < 1) return 'just now'
  if (m < 60) return `${m} min ago`
  return `${Math.floor(m / 60)} h ${m % 60} min ago`
}

/** Today's date in India as yyyy-mm-dd (for date inputs and the API). */
export function indiaToday(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Kolkata' }).format(new Date())
}

/** Tell other parts of the screen (like today's total) that bills changed. */
export function notifyBillsChanged() {
  window.dispatchEvent(new Event('pos:bills-changed'))
}
