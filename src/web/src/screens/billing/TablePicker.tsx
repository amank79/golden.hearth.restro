import { useState, type FormEvent } from 'react'
import { Modal } from '../../components/Modal'
import type { BillSummary } from '../../lib/types'

interface Props {
  title: string
  openBills: BillSummary[]
  currentBillId?: number
  onPick: (table: string) => void
  onOpenExisting: (billId: number) => void
  onClose: () => void
}

const QUICK_TABLES = Array.from({ length: 20 }, (_, i) => String(i + 1))

/** Pick a table number: tap 1–20, or type any label ("4A", "Garden 2"). Busy tables open their bill instead. */
export function TablePicker({ title, openBills, currentBillId, onPick, onOpenExisting, onClose }: Props) {
  const [text, setText] = useState('')
  const busy = new Map(openBills.filter((b) => b.orderType === 'DineIn' && b.id !== currentBillId).map((b) => [b.tableLabel?.toLowerCase(), b.id]))

  const choose = (label: string) => {
    const table = label.trim()
    if (!table) return
    const existing = busy.get(table.toLowerCase())
    if (existing) onOpenExisting(existing)
    else onPick(table)
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    choose(text)
  }

  return (
    <Modal title={title} subtitle="Tap the table number, or type it and press Enter. Orange tables already have an open bill." onClose={onClose}>
      <form className="row" style={{ flexWrap: 'nowrap', marginBottom: 12 }} onSubmit={submit}>
        <input className="input" placeholder="Table, e.g. 4 or 4A" value={text} onChange={(e) => setText(e.target.value)} maxLength={20} autoFocus />
        <button className="btn primary" disabled={!text.trim()}>Open</button>
      </form>
      <div className="numpad">
        {QUICK_TABLES.map((t) => (
          <button key={t} className={busy.has(t) ? 'busy' : ''} onClick={() => choose(t)}>{t}</button>
        ))}
      </div>
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Cancel</button>
      </div>
    </Modal>
  )
}
