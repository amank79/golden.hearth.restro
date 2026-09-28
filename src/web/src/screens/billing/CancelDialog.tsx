import { useState, type FormEvent } from 'react'
import { Modal } from '../../components/Modal'
import { toast } from '../../components/toast'
import { billLabel } from '../../lib/bills'
import { rupees } from '../../lib/money'
import type { Bill } from '../../lib/types'

interface Props {
  bill: Bill
  onConfirm: (reason: string, copyToNewBill: boolean) => void
  onClose: () => void
}

const REASONS = ['Customer left', 'Wrong order', 'Wrong table', 'Customer added dishes', 'Test bill']

/** Cancel a bill with a reason. It is never deleted: it stays in history as Cancelled (BILL-3). */
export function CancelDialog({ bill, onConfirm, onClose }: Props) {
  const [reason, setReason] = useState('')
  const [copy, setCopy] = useState(bill.isFinalised && bill.status === 'Open')

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (!reason.trim()) return toast('Please give a reason for cancelling.', 'error')
    onConfirm(reason.trim(), copy)
  }

  return (
    <Modal
      title={`Cancel ${bill.billNo ? `bill ${bill.billNo}` : billLabel(bill)}?`}
      subtitle={`${rupees(bill.totalPaise)} · The bill stays in history marked Cancelled.`}
      onClose={onClose}
    >
      <form className="form" onSubmit={submit}>
        <label className="field">
          <span>Reason</span>
          <input value={reason} onChange={(e) => setReason(e.target.value)} maxLength={200} autoFocus />
        </label>
        <div className="chipbar">
          {REASONS.map((r) => (
            <button type="button" key={r} className={`chip${reason === r ? ' on' : ''}`} onClick={() => setReason(r)}>{r}</button>
          ))}
        </div>
        {bill.lines.length > 0 && (
          <label className="check">
            <input type="checkbox" checked={copy} onChange={(e) => setCopy(e.target.checked)} />
            Copy the dishes to a new bill (to correct and print again)
          </label>
        )}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Keep bill</button>
          <button className="btn danger solid">Cancel bill</button>
        </div>
      </form>
    </Modal>
  )
}
