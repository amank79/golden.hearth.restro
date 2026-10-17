import { useEffect, useState, type KeyboardEvent } from 'react'
import { Icon } from '../../components/Icon'
import { Modal } from '../../components/Modal'
import { api } from '../../lib/api'
import { plural } from '../../lib/bills'
import { formatPhone, isPhoneText } from '../../lib/customers'
import { percent, rupeesShort } from '../../lib/money'
import type { Bill, Customer } from '../../lib/types'
import { CustomerForm } from '../customers/CustomerForm'

interface Props {
  bill: Bill
  onPick: (c: Customer) => void
  onRemove: () => void
  onClose: () => void
}

/** Find the customer by mobile number or name and add them to the bill, or save a new customer. */
export function CustomerDialog({ bill, onPick, onRemove, onClose }: Props) {
  const [q, setQ] = useState('')
  const [results, setResults] = useState<Customer[] | null>(null)
  const [adding, setAdding] = useState(false)

  useEffect(() => {
    const t = window.setTimeout(() => {
      api.get<Customer[]>(`/customers?take=8&q=${encodeURIComponent(q.trim())}`).then(setResults, () => setResults([]))
    }, q ? 200 : 0)
    return () => window.clearTimeout(t)
  }, [q])

  const onKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key !== 'Enter') return
    e.preventDefault()
    if (q.trim() && results?.length) onPick(results[0])
    else if (q.trim()) setAdding(true)
  }

  if (adding) {
    const phone = isPhoneText(q) ? q.trim() : ''
    return (
      <Modal title="New customer" subtitle="Saved for next time: find them by mobile number." onClose={onClose}>
        <CustomerForm
          customer={null}
          initial={{ phone, name: phone ? '' : q.trim() }}
          saveLabel="Save and add to bill"
          onSaved={onPick}
          onCancel={() => setAdding(false)}
        />
      </Modal>
    )
  }

  return (
    <Modal title="Customer" subtitle="Type the mobile number or name. Regulars get their discount automatically." onClose={onClose}>
      <label className="search" style={{ marginBottom: 12 }}>
        <Icon name="search" />
        <input value={q} onChange={(e) => setQ(e.target.value)} onKeyDown={onKey} placeholder="98250 12345 or name" inputMode="search" autoFocus aria-label="Mobile number or name" />
      </label>
      <div className="cust-results">
        {results?.map((c) => (
          <button key={c.id} className={`cust-row${bill.customerId === c.id ? ' on' : ''}`} onClick={() => onPick(c)}>
            <span className="cust-ic"><Icon name="user" /></span>
            <span className="cust-main">
              <b>{c.name}</b>
              <small>{formatPhone(c.phone)} · {c.visits ? `${plural(c.visits, 'visit')} · ${rupeesShort(c.spentPaise)}` : 'no visits yet'}</small>
            </span>
            {c.discountBp > 0 && <span className="pill green">{percent(c.discountBp)}% off</span>}
          </button>
        ))}
        {results && results.length === 0 && (
          <div className="empty" style={{ padding: 16 }}>{q.trim() ? 'No customer found. Add them as a new customer.' : 'No customers saved yet.'}</div>
        )}
      </div>
      <div className="modal-actions">
        {bill.customerId !== null && (
          <button type="button" className="btn danger" onClick={onRemove}>Remove {bill.customerName}</button>
        )}
        <button type="button" className="btn primary" onClick={() => setAdding(true)}><Icon name="plus" />New customer</button>
      </div>
    </Modal>
  )
}
