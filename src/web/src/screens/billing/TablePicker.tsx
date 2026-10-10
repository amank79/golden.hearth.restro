import { useState, type FormEvent } from 'react'
import { Icon } from '../../components/Icon'
import { Modal } from '../../components/Modal'
import { rupeesShort } from '../../lib/money'
import type { BillSummary } from '../../lib/types'

interface Props {
  title: string
  /** Table labels from Settings, in order. */
  tables: string[]
  subtitle?: string
  openBills: BillSummary[]
  currentBillId?: number
  onPick: (table: string) => void
  onOpenExisting: (billId: number) => void
  /** When given, a big Takeaway button is shown above the tables. */
  onTakeaway?: () => void
  onClose: () => void
}

/** Pick a table: tap one of the tables set in Settings, or type any label ("4A", "Garden 2"). Busy tables open their bill instead. */
export function TablePicker({ title, subtitle, tables, openBills, currentBillId, onPick, onOpenExisting, onTakeaway, onClose }: Props) {
  const [text, setText] = useState('')
  const busy = new Map(
    openBills.filter((b) => b.orderType === 'DineIn' && b.id !== currentBillId).map((b) => [b.tableLabel?.toLowerCase(), b]),
  )

  const choose = (label: string) => {
    const table = label.trim()
    if (!table) return
    const existing = busy.get(table.toLowerCase())
    if (existing) onOpenExisting(existing.id)
    else onPick(table)
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    choose(text)
  }

  // Busy tables that are not in the list (typed in, or removed later) get their own buttons too.
  const listed = new Set(tables.map((t) => t.toLowerCase()))
  const extraBusy = [...busy.values()].filter((b) => !listed.has(b.tableLabel?.toLowerCase() ?? ''))

  return (
    <Modal title={title} subtitle={subtitle ?? 'Tap the table number, or type it and press Enter.'} onClose={onClose} wide>
      {onTakeaway && (
        <button className="btn primary big takeaway-btn" onClick={onTakeaway}>
          <Icon name="bag" />Takeaway / parcel
        </button>
      )}
      <p className="picker-label">{onTakeaway ? 'Or dine-in table' : 'Table'} · gold = already has an open bill</p>
      <div className="tables">
        {tables.map((t) => {
          const b = busy.get(t.toLowerCase())
          const cls = [b ? 'busy' : '', t.length > 4 ? 'long' : ''].filter(Boolean).join(' ')
          return (
            <button key={t} className={cls} onClick={() => choose(t)} title={b ? `Open the bill for table ${t}` : `New bill for table ${t}`}>
              {t}
              {b && <small>{rupeesShort(b.totalPaise)}</small>}
            </button>
          )
        })}
        {extraBusy.map((b) => (
          <button key={b.id} className={`busy${(b.tableLabel?.length ?? 0) > 4 ? ' long' : ''}`} onClick={() => onOpenExisting(b.id)}>
            {b.tableLabel}
            <small>{rupeesShort(b.totalPaise)}</small>
          </button>
        ))}
      </div>
      {tables.length === 0 && extraBusy.length === 0 && (
        <p className="muted" style={{ margin: '4px 0 0' }}>No tables are set up yet. Add them in Settings, or type the table below.</p>
      )}
      <form className="row" style={{ flexWrap: 'nowrap', marginTop: 14 }} onSubmit={submit}>
        <input className="input" placeholder="Other table, e.g. 4A or Garden 2" value={text} onChange={(e) => setText(e.target.value)} maxLength={20} autoFocus />
        <button className="btn dark" disabled={!text.trim()}>Open <kbd>Enter</kbd></button>
      </form>
    </Modal>
  )
}
