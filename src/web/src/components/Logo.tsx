import { MONOGRAM, MONOGRAM_RING, WORDMARK_MAIN, WORDMARK_SUB } from './logoPaths'

interface Props {
  className?: string
  /** Read out by screen readers; the logo is decorative when left out. */
  title?: string
}

/** "TGH" lettermark in a ring. Drawn in the current text colour. */
export function Monogram({ className, title }: Props) {
  const { cx, cy, r, width } = MONOGRAM_RING
  return (
    <svg className={className} viewBox="13.72 13.04 74.68 74.68" role={title ? 'img' : undefined} aria-label={title} aria-hidden={title ? undefined : true}>
      <circle cx={cx} cy={cy} r={r} fill="none" stroke="currentColor" strokeWidth={width} />
      <g fill="currentColor">
        {MONOGRAM.map((d) => <path key={d} d={d} />)}
      </g>
    </svg>
  )
}

/** "The Golden Hearth / Restaurant & Café". Colours come from --logo-ink and --logo-sub. */
export function Wordmark({ className, title }: Props) {
  return (
    <svg className={className} viewBox="23 9.2 206.8 49.2" role={title ? 'img' : undefined} aria-label={title} aria-hidden={title ? undefined : true}>
      <g fill="var(--logo-ink)">
        {WORDMARK_MAIN.map((d) => <path key={d} d={d} />)}
      </g>
      <g fill="var(--logo-sub)">
        {WORDMARK_SUB.map((d) => <path key={d} d={d} />)}
      </g>
    </svg>
  )
}
