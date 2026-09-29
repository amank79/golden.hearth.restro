// Same rules as MenuSearch.cs on the server, so the billing screen can search instantly without a round trip.
// Lower rank is a better match; null means no match.

export function searchRank(name: string, shortCode: string | null, query: string): number | null {
  const q = query.trim().toLowerCase()
  if (!q) return 0
  const n = name.toLowerCase()
  const code = (shortCode ?? '').toLowerCase()

  if (code && code === q) return 0
  if (code && code.startsWith(q)) return 1
  if (n.startsWith(q)) return 2

  const words = n.split(/[\s\-()&/]+/).filter(Boolean)
  if (words.some((w) => w.startsWith(q))) return 3

  const initials = words.map((w) => w[0]).join('')
  if (q.length >= 2 && initials.startsWith(q)) return 4

  if (n.includes(q)) return 5
  return null
}

export function searchItems<T extends { name: string; shortCode: string | null }>(items: T[], query: string): T[] {
  if (!query.trim()) return items
  return items
    .map((item, index) => ({ item, index, rank: searchRank(item.name, item.shortCode, query) }))
    .filter((x) => x.rank !== null)
    .sort((a, b) => a.rank! - b.rank! || a.index - b.index)
    .map((x) => x.item)
}

/** "Chilli Paneer (Half)"; single-price dishes have one variant called "Regular" which is not shown. */
export function dishName(itemName: string, variantName: string): string {
  return !variantName || variantName === 'Regular' ? itemName : `${itemName} (${variantName})`
}
