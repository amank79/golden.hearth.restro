// Colour palette, chosen in Settings and remembered on this laptop only.

export type Theme = 'navy' | 'burgundy' | 'midnight'

export const THEMES: { id: Theme; name: string; note: string }[] = [
  { id: 'navy', name: 'Navy & Champagne', note: 'Deep ink navy with champagne gold' },
  { id: 'burgundy', name: 'Burgundy & Champagne', note: 'Rich wine red with champagne gold' },
  { id: 'midnight', name: 'Midnight', note: 'Dark navy and gold, easy on the eyes at night' },
]

const KEY = 'pos-theme'

/**
 * Reads the saved palette. Choices saved by other designs map to the nearest one here:
 * dark ones (Noir, the old Dark setting) to Midnight, every light one to Navy.
 */
export function toTheme(value: string | null): Theme {
  if (value === 'noir' || value === 'dark') return 'midnight'
  return THEMES.find((t) => t.id === value)?.id ?? 'navy'
}

export function savedTheme(): Theme {
  try {
    return toTheme(localStorage.getItem(KEY))
  } catch {
    return 'navy'
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
