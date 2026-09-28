// Money is whole paise (integers) everywhere. Rupees are only for display and for reading what staff type.

const rupeeFormat = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

const wholeRupeeFormat = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
})

/** ₹1,234.50 (Indian digit grouping). */
export function rupees(paise: number): string {
  return rupeeFormat.format(paise / 100)
}

/** ₹1,234 when there are no paise, else ₹1,234.50. For menu prices and buttons. */
export function rupeesShort(paise: number): string {
  return paise % 100 === 0 ? wholeRupeeFormat.format(paise / 100) : rupees(paise)
}

/**
 * Reads a rupee amount typed by staff ("120", "1,234.5", "₹99.99") as whole paise, without floating-point maths.
 * Returns null if the text is not a valid amount with at most 2 decimals.
 */
export function parseRupees(text: string): number | null {
  const clean = text.replace(/[₹,\s]/g, '')
  const m = /^(\d{1,9})(?:\.(\d{0,2}))?$/.exec(clean)
  if (!m) return null
  return Number(m[1]) * 100 + Number((m[2] ?? '').padEnd(2, '0'))
}

/** Paise as a plain editable rupee string: 12050 -> "120.50", 12000 -> "120". */
export function paiseToInput(paise: number): string {
  const r = Math.trunc(paise / 100)
  const p = Math.abs(paise % 100)
  return p === 0 ? String(r) : `${r}.${String(p).padStart(2, '0')}`
}

/** Basis points as a percent: 500 -> "5", 250 -> "2.5", 1250 -> "12.5". */
export function percent(bp: number): string {
  const whole = Math.trunc(bp / 100)
  const frac = String(Math.abs(bp % 100)).padStart(2, '0').replace(/0+$/, '')
  return frac ? `${whole}.${frac}` : String(whole)
}

/** Reads a typed percent ("5", "2.5", "12.50") as basis points, or null. */
export function parsePercent(text: string): number | null {
  const m = /^(\d{1,3})(?:\.(\d{0,2}))?$/.exec(text.trim().replace('%', ''))
  if (!m) return null
  return Number(m[1]) * 100 + Number((m[2] ?? '').padEnd(2, '0'))
}
