import { useState, type FormEvent } from 'react'
import { Modal } from '../../components/Modal'
import { toast } from '../../components/toast'
import { paiseToInput, parsePercent, parseRupees, percent, rupees } from '../../lib/money'
import type { Bill, DiscountKind } from '../../lib/types'

interface Props {
  bill: Bill
  onSave: (kind: DiscountKind, value: number, reason: string | null) => void
  onClose: () => void
}

const REASONS = ['Family / friend', 'Regular customer', 'Complaint', 'Festival offer']

/** Discount by percent or amount, always with a reason (BILL-4). */
export function DiscountDialog({ bill, onSave, onClose }: Props) {
  const [kind, setKind] = useState<'Percent' | 'Amount'>(bill.discountKind === 'Amount' ? 'Amount' : 'Percent')
  const [value, setValue] = useState(
    bill.discountKind === 'Percent' ? percent(bill.discountValue) : bill.discountKind === 'Amount' ? paiseToInput(bill.discountValue) : '',
  )
  const [reason, setReason] = useState(bill.discountReason ?? '')

  const parsed = kind === 'Percent' ? parsePercent(value) : parseRupees(value)
  const preview =
    parsed === null ? null : kind === 'Percent' ? Math.round((bill.subtotalPaise * Math.min(parsed, 10000)) / 10000) : Math.min(parsed, bill.subtotalPaise)

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (parsed === null || parsed <= 0) return toast(kind === 'Percent' ? 'Type the discount percent, e.g. 10.' : 'Type the discount amount in rupees.', 'error')
    if (!reason.trim()) return toast('Please give a reason for the discount.', 'error')
    onSave(kind, parsed, reason.trim())
  }

  return (
    <Modal title="Discount" subtitle={`Items total ${rupees(bill.subtotalPaise)}`} onClose={onClose}>
      <form className="form" onSubmit={submit}>
        <div className="seg">
          <button type="button" className={kind === 'Percent' ? 'on' : ''} onClick={() => setKind('Percent')}>Percent (%)</button>
          <button type="button" className={kind === 'Amount' ? 'on' : ''} onClick={() => setKind('Amount')}>Amount (₹)</button>
        </div>
        <label className="field">
          <span>{kind === 'Percent' ? 'Discount %' : 'Discount ₹'}</span>
          <input value={value} onChange={(e) => setValue(e.target.value)} inputMode="decimal" autoFocus />
          {preview !== null && preview > 0 && <small>Discount: {rupees(preview)}</small>}
        </label>
        {kind === 'Percent' && (
          <div className="chipbar">
            {['5', '10', '15', '20'].map((p) => (
              <button type="button" key={p} className={`chip${value === p ? ' on' : ''}`} onClick={() => setValue(p)}>{p}%</button>
            ))}
          </div>
        )}
        <label className="field">
          <span>Reason</span>
          <input value={reason} onChange={(e) => setReason(e.target.value)} maxLength={100} placeholder="Why is the discount given?" />
        </label>
        <div className="chipbar">
          {REASONS.map((r) => (
            <button type="button" key={r} className={`chip${reason === r ? ' on' : ''}`} onClick={() => setReason(r)}>{r}</button>
          ))}
        </div>
        <div className="modal-actions">
          {bill.discountKind !== 'None' && (
            <button type="button" className="btn danger" onClick={() => onSave('None', 0, null)}>Remove discount</button>
          )}
          <button className="btn primary">Apply discount</button>
        </div>
      </form>
    </Modal>
  )
}
