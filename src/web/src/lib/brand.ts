/**
 * The wordmark in two lines: "The Golden Hearth Restaurant" -> "The Golden Hearth" over "Restaurant".
 * A trailing word like Restaurant, Restro, Cafe, Dhaba or Kitchen goes on the small second line.
 */
export function brandParts(name: string | undefined): { main: string; sub: string } {
  const words = (name ?? '').trim().split(/\s+/).filter(Boolean)
  if (words.length === 0) return { main: 'Restaurant POS', sub: '' }
  const last = words[words.length - 1]
  if (words.length > 1 && /^(restaurant|restro|cafe|café|dhaba|kitchen|bistro)$/i.test(last)) {
    return { main: words.slice(0, -1).join(' '), sub: last }
  }
  return { main: words.join(' '), sub: '' }
}
