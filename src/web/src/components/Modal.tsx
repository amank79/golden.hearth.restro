import { useEffect, useRef, type ReactNode } from 'react'

interface Props {
  title: string
  subtitle?: ReactNode
  onClose: () => void
  children: ReactNode
  wide?: boolean
}

/** Dialog over the screen. Esc or a tap outside closes it. */
export function Modal({ title, subtitle, onClose, children, wide }: Props) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      // Only the top-most dialog closes.
      const overlays = document.querySelectorAll('.overlay')
      if (e.key === 'Escape' && overlays[overlays.length - 1] === ref.current) {
        e.stopPropagation()
        onClose()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="overlay" ref={ref} onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className={`modal${wide ? ' wide' : ''}`} role="dialog" aria-modal="true" aria-label={title}>
        <h2>{title}</h2>
        {subtitle && <p className="modal-sub">{subtitle}</p>}
        {children}
      </div>
    </div>
  )
}
