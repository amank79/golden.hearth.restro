import type { Bill } from './types'

/** "9825012345" → "98250 12345", the way people say it. */
export function formatPhone(phone: string): string {
  return phone.length === 10 ? `${phone.slice(0, 5)} ${phone.slice(5)}` : phone
}

/** True when the text looks like (part of) a phone number rather than a name. */
export function isPhoneText(text: string): boolean {
  return /\d/.test(text) && /^[\d\s+-]+$/.test(text.trim())
}

/** The discount on this bill is the customer's standing discount (put on when they were added). */
export function hasStandingDiscount(bill: Bill): boolean {
  return bill.discountKind === 'Percent' && bill.discountReason === 'Regular customer' && bill.customerId !== null
}
