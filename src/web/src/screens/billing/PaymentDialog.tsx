import { useState, type FormEvent } from 'react'
import { Icon, type IconName } from '../../components/Icon'
import { Modal } from '../../components/Modal'
import { toast } from '../../components/toast'
import { billLabel } from '../../lib/bills'
import { paiseToInput, parseRupees, rupees, rupeesShort } from '../../lib/money'
import { PAYMENT_METHODS, type Bill, type PaymentMethod } from '../../lib/types'

interface Props {
  bill: Bill
  initialMethod: PaymentMethod
  onConfirm: (payments: { method: PaymentMethod; amountPaise: number }[], changePaise: number) => void
  onClose: () => void
}

const METHOD_ICON: Record<PaymentMethod, IconName> = { Cash: 'cash', Upi: 'upi', Card: 'card' }

/** Next round amounts a customer is likely to hand over for this total. */
function cashSuggestions(total: number): number[] {
  const out = new Set<number>()
  for (const step of [10000, 50000, 200000]) {
    const up = Math.ceil(total / step) * step
    if (up > total) out.add(up)
  }
  return [...out].sort((a, b) => a - b).slice(0, 3)
}

/** Take payment: one method (with change for cash) or split across cash / UPI / card (BILL-6). */
export function PaymentDialog({ bill, initialMethod, onConfirm, onClose }: Props) {
  const total = bill.totalPaise
  const [split, setSplit] = useState(false)
  const [method, setMethod] = useState<PaymentMethod>(initialMethod)
  const [received, setReceived] = useState('')
  const [parts, setParts] = useState<Record<PaymentMethod, string>>({ Cash: '', Upi: '', Card: '' })

  const receivedPaise = received ? parseRupees(received) : total
  const change = receivedPaise !== null ? receivedPaise - total : null

  const partPaise = PAYMENT_METHODS.map((m) => ({ method: m.value, amount: parts[m.value] ? parseRupees(parts[m.value]) : 0 }))
  const splitValid = partPaise.every((p) => p.amount !== null)
  const splitSum = partPaise.reduce((s, p) => s + (p.amount ?? 0), 0)
  const remaining = total - splitSum

  const fillRest = (m: PaymentMethod) => {
    const current = parts[m] ? parseRupees(parts[m]) ?? 0 : 0
    const value = current + remaining
    setParts((p) => ({ ...p, [m]: value > 0 ? paiseToInput(value) : '' }))
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (split) {
      if (!splitValid) return toast('Type each amount in rupees, e.g. 500 or 250.50.', 'error')
      if (remaining !== 0) return toast(remaining > 0 ? `${rupees(remaining)} still to pay.` : `${rupees(-remaining)} too much.`, 'error')
      onConfirm(partPaise.filter((p) => p.amount! > 0).map((p) => ({ method: p.method, amountPaise: p.amount! })), 0)
      return
    }
    if (method === 'Cash') {
      if (change === null) return toast('Type the cash received in rupees.', 'error')
      if (change < 0) return toast(`Cash received is ${rupees(-change)} short.`, 'error')
    }
    onConfirm(total > 0 ? [{ method, amountPaise: total }] : [], method === 'Cash' ? Math.max(0, change ?? 0) : 0)
  }

  return (
    <Modal title={`Payment · ${billLabel(bill)}`} onClose={onClose}>
      <form className="form" onSubmit={submit}>
        <div className="pay-total"><span>Total to pay</span><b>{rupees(total)}</b></div>
        <div className="seg">
          <button type="button" className={split ? '' : 'on'} onClick={() => setSplit(false)}>One method</button>
          <button type="button" className={split ? 'on' : ''} onClick={() => setSplit(true)}>Split payment</button>
        </div>

        {!split ? (
          <>
            <div className="pay-methods">
              {PAYMENT_METHODS.map((m) => (
                <button type="button" key={m.value} className={`pay-method${method === m.value ? ' on' : ''}`} onClick={() => setMethod(m.value)} aria-pressed={method === m.value}>
                  <Icon name={METHOD_ICON[m.value]} />{m.label}
                </button>
              ))}
            </div>
            {method === 'Cash' && (
              <>
                <label className="field">
                  <span>Cash received (₹)</span>
                  <input value={received} onChange={(e) => setReceived(e.target.value)} inputMode="decimal" placeholder={paiseToInput(total)} autoFocus />
                </label>
                <div className="chipbar">
                  <button type="button" className="chip" onClick={() => setReceived('')}>Exact</button>
                  {cashSuggestions(total).map((a) => (
                    <button type="button" key={a} className="chip" onClick={() => setReceived(paiseToInput(a))}>{rupeesShort(a)}</button>
                  ))}
                </div>
                {change !== null && received !== '' && (
                  <div className={`pay-sum big ${change < 0 ? 'bad' : 'ok'}`}>
                    <span>{change < 0 ? 'Short by' : 'Give back change'}</span>
                    <span>{rupees(Math.abs(change))}</span>
                  </div>
                )}
              </>
            )}
            {method === 'Upi' && <p className="note" style={{ margin: 0 }}>Check that the money has arrived on the restaurant phone before you confirm.</p>}
            {method === 'Card' && <p className="note" style={{ margin: 0 }}>Confirm after the card machine prints “Approved”.</p>}
          </>
        ) : (
          <div>
            {PAYMENT_METHODS.map((m) => (
              <div className="pay-line" key={m.value}>
                <b><Icon name={METHOD_ICON[m.value]} />{m.label}</b>
                <input className="input" value={parts[m.value]} onChange={(e) => setParts((p) => ({ ...p, [m.value]: e.target.value }))} inputMode="decimal" placeholder="0" aria-label={`${m.label} amount`} />
                <button type="button" className="btn small" onClick={() => fillRest(m.value)} disabled={remaining === 0}>Rest</button>
              </div>
            ))}
            <div className={`pay-sum ${remaining === 0 ? 'ok' : 'bad'}`} style={{ marginTop: 12 }}>
              <span>{remaining === 0 ? 'Fully paid' : remaining > 0 ? 'Still to pay' : 'Too much'}</span>
              <span>{rupees(Math.abs(remaining))}</span>
            </div>
          </div>
        )}

        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Back</button>
          <button className="btn primary big" autoFocus={method !== 'Cash' && !split}>
            {split ? 'Confirm payment' : `Paid by ${PAYMENT_METHODS.find((m) => m.value === method)!.label}`} <kbd>Enter</kbd>
          </button>
        </div>
      </form>
    </Modal>
  )
}
