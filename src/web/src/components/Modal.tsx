import { useEffect, useRef, type ReactNode } from 'react'
import { Icon } from './Icon'

interface Props {
  title: string
  subtitle?: ReactNode
  onClose: () => void
  children: ReactNode
  wide?: boolean
}

/** Dialog over the screen. Esc, the ✕ button or a tap outside closes it. */
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
        <div className="modal-head">
          <div>
            <h2>{title}</h2>
            {subtitle && <p className="modal-sub">{subtitle}</p>}
          </div>
          <button type="button" className="icon-btn" onClick={onClose} aria-label="Close" tabIndex={-1}>
            <Icon name="close" />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}
