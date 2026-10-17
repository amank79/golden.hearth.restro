import { useState, type FormEvent } from 'react'
import { Field } from '../../components/Field'
import { toast } from '../../components/toast'
import { api, ApiError, errorText } from '../../lib/api'
import { formatPhone } from '../../lib/customers'
import { parsePercent, percent } from '../../lib/money'
import type { Customer } from '../../lib/types'

interface Props {
  /** The customer to change, or null for a new one. */
  customer: Customer | null
  /** Starting values for a new customer (e.g. the phone number typed in the search). */
  initial?: { name?: string; phone?: string }
  saveLabel?: string
  onSaved: (c: Customer) => void
  onCancel: () => void
}

/** Name, mobile number and standing discount of a customer. */
export function CustomerForm({ customer, initial, saveLabel, onSaved, onCancel }: Props) {
  const [name, setName] = useState(customer?.name ?? initial?.name ?? '')
  const [phone, setPhone] = useState(customer ? formatPhone(customer.phone) : initial?.phone ?? '')
  const [discount, setDiscount] = useState(customer && customer.discountBp > 0 ? percent(customer.discountBp) : '')
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [saving, setSaving] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const discountBp = discount.trim() === '' ? 0 : parsePercent(discount)
    if (discountBp === null) return setErrors({ discountBp: ['Type the discount as a number, for example 10.'] })
    setSaving(true)
    try {
      const body = { name, phone, discountBp }
      const saved = customer ? await api.put<Customer>(`/customers/${customer.id}`, body) : await api.post<Customer>('/customers', body)
      onSaved(saved)
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fields).length) setErrors(err.fields)
      toast(errorText(err), 'error')
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="form" onSubmit={(e) => void submit(e)}>
      <Field label="Name" error={errors.name}>
        <input value={name} onChange={(e) => setName(e.target.value)} maxLength={60} autoFocus={!initial?.name} placeholder="Ramesh Patel" />
      </Field>
      <Field label="Mobile number" hint="10 digits. Used to find the customer; never printed on the bill." error={errors.phone}>
        <input value={phone} onChange={(e) => setPhone(e.target.value)} inputMode="tel" maxLength={16} placeholder="98250 12345" autoFocus={!!initial?.name} />
      </Field>
      <Field label="Regular discount (%)" hint="Put on this customer's bills automatically. Leave empty for none." error={errors.discountBp}>
        <div className="row" style={{ flexWrap: 'nowrap' }}>
          <input value={discount} onChange={(e) => setDiscount(e.target.value)} inputMode="decimal" style={{ maxWidth: 110 }} placeholder="0" />
          <div className="chipbar">
            {['5', '10', '15'].map((p) => (
              <button type="button" key={p} className={`chip${discount === p ? ' on' : ''}`} onClick={() => setDiscount(discount === p ? '' : p)}>{p}%</button>
            ))}
          </div>
        </div>
      </Field>
      <div className="modal-actions">
        <button type="button" className="btn" onClick={onCancel}>Back</button>
        <button className="btn primary" disabled={saving}>{saving ? 'Saving…' : saveLabel ?? 'Save customer'}</button>
      </div>
    </form>
  )
}
