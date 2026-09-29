// Colour palette, chosen in Settings and remembered on this laptop only.

export type Theme = 'emerald' | 'ivory' | 'noir'

export const THEMES: { id: Theme; name: string; note: string }[] = [
  { id: 'emerald', name: 'Emerald & Gold', note: 'Deep green with antique gold' },
  { id: 'ivory', name: 'Ivory & Champagne', note: 'Warm cream with espresso brown' },
  { id: 'noir', name: 'Noir & Gold', note: 'Black and gold, easy on the eyes at night' },
]

const KEY = 'pos-theme'

/** Reads the saved palette; the old Light/Dark choices map to Emerald and Noir. */
export function toTheme(value: string | null): Theme {
  if (value === 'dark') return 'noir'
  return THEMES.find((t) => t.id === value)?.id ?? 'emerald'
}

export function savedTheme(): Theme {
  try {
    return toTheme(localStorage.getItem(KEY))
  } catch {
    return 'emerald'
  }
}

export function applyTheme(theme: Theme) {
  document.documentElement.dataset.theme = theme
  try {
    localStorage.setItem(KEY, theme)
  } catch {
    // Storage blocked: the choice lasts until the window is closed.
  }
}
