import { useState, type FormEvent } from 'react'
import { Modal } from '../../components/Modal'
import { dishName } from '../../lib/menuSearch'
import { rupees } from '../../lib/money'
import type { BillLine } from '../../lib/types'

interface Props {
  line: BillLine
  onSave: (qty: number, note: string | null) => void
  onRemove: () => void
  onClose: () => void
}

const NOTES = ['Less spicy', 'Extra spicy', 'No onion', 'No garlic', 'Jain', 'Parcel']

/** Change quantity or note of a line, or take it off the bill. */
export function LineEditor({ line, onSave, onRemove, onClose }: Props) {
  const [qty, setQty] = useState(line.qty)
  const [note, setNote] = useState(line.note ?? '')

  const submit = (e: FormEvent) => {
    e.preventDefault()
    onSave(qty, note.trim() || null)
  }

  return (
    <Modal title={dishName(line.itemName, line.variantName)} subtitle={`${rupees(line.unitPricePaise)} each`} onClose={onClose}>
      <form className="form" onSubmit={submit}>
        <div className="row" style={{ justifyContent: 'center', gap: 16 }}>
          <button type="button" className="btn big" style={{ width: 64 }} onClick={() => setQty((q) => Math.max(1, q - 1))} aria-label="Less">−</button>
          <input className="input" style={{ width: 90, textAlign: 'center', fontSize: 24, fontWeight: 800 }} value={qty} inputMode="numeric" aria-label="Quantity"
            onChange={(e) => setQty(Math.min(999, Math.max(1, Number(e.target.value.replace(/\D/g, '')) || 1)))} />
          <button type="button" className="btn big" style={{ width: 64 }} onClick={() => setQty((q) => Math.min(999, q + 1))} aria-label="More">+</button>
        </div>
        <label className="field">
          <span>Note</span>
          <input value={note} onChange={(e) => setNote(e.target.value)} maxLength={100} placeholder="e.g. less spicy" autoFocus />
        </label>
        <div className="chipbar">
          {NOTES.map((n) => (
            <button type="button" key={n} className="chip" onClick={() => setNote((cur) => (cur ? `${cur}, ${n.toLowerCase()}` : n))}>{n}</button>
          ))}
        </div>
        <div className="modal-actions">
          <button type="button" className="btn danger" onClick={onRemove}>Remove from bill</button>
          <button className="btn primary">Save · {rupees(line.unitPricePaise * qty)}</button>
        </div>
      </form>
    </Modal>
  )
}
