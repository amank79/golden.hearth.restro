import { useEffect } from 'react'
import { Modal } from '../../components/Modal'
import { rupeesShort } from '../../lib/money'
import type { MenuItem, Variant } from '../../lib/types'

interface Props {
  item: MenuItem
  qty: number
  onPick: (v: Variant) => void
  onClose: () => void
}

/** "Choose the plate size": big buttons; keys 1, 2, 3… or the first letter (H / F) pick a size. */
export function VariantPicker({ item, qty, onPick, onClose }: Props) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const n = Number(e.key)
      const byNumber = Number.isInteger(n) && n >= 1 ? item.variants[n - 1] : undefined
      const byLetter = item.variants.filter((v) => v.name[0]?.toLowerCase() === e.key.toLowerCase())
      const pick = byNumber ?? (byLetter.length === 1 ? byLetter[0] : undefined)
      if (pick) {
        e.preventDefault()
        onPick(pick)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [item, onPick])

  return (
    <Modal title={item.name} subtitle={qty > 1 ? `Choose the size (quantity ${qty})` : 'Choose the size'} onClose={onClose}>
      <div className="variant-opts">
        {item.variants.map((v, i) => (
          <button key={v.id} onClick={() => onPick(v)} autoFocus={i === 0}>
            {v.name}
            <kbd>{i + 1}</kbd>
            <b>{rupeesShort(v.pricePaise)}</b>
          </button>
        ))}
      </div>
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Cancel</button>
      </div>
    </Modal>
  )
}
