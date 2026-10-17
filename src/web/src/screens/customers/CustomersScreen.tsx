import { useCallback, useEffect, useState } from 'react'
import { Icon } from '../../components/Icon'
import { Modal } from '../../components/Modal'
import { toast } from '../../components/toast'
import { api, errorText } from '../../lib/api'
import { dateTime, plural } from '../../lib/bills'
import { formatPhone, isPhoneText } from '../../lib/customers'
import { percent, rupees } from '../../lib/money'
import type { Customer } from '../../lib/types'
import { CustomerForm } from './CustomerForm'

/** Saved customers with their visits and spending; add one or change a regular's discount. */
export function CustomersScreen() {
  const [q, setQ] = useState('')
  const [list, setList] = useState<Customer[] | null>(null)
  const [editing, setEditing] = useState<Customer | 'new' | null>(null)

  const load = useCallback(() => {
    api.get<Customer[]>(`/customers?take=50&q=${encodeURIComponent(q.trim())}`).then(setList, (e) => toast(errorText(e), 'error'))
  }, [q])

  useEffect(() => {
    const t = window.setTimeout(load, q ? 200 : 0)
    return () => window.clearTimeout(t)
  }, [load, q])

  const saved = (c: Customer) => {
    toast(editing === 'new' ? `${c.name} saved.` : `${c.name} updated.`)
    setEditing(null)
    load()
  }

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h1>Customers</h1>
          <p>Regulars and their discount. Add a customer to a bill from the Billing screen.</p>
        </div>
        <div className="actions">
          <button className="btn primary" onClick={() => setEditing('new')}><Icon name="plus" />New customer</button>
        </div>
      </div>

      <div className="filters">
        <label className="search">
          <Icon name="search" />
          <input placeholder="Mobile number or name…" value={q} onChange={(e) => setQ(e.target.value)} autoFocus aria-label="Search customers" />
          {q && <button className="icon-btn" onClick={() => setQ('')} aria-label="Clear search"><Icon name="close" /></button>}
        </label>
      </div>

      <div className="card cust-list">
        <table className="list-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Mobile</th>
              <th>Regular discount</th>
              <th className="num">Visits</th>
              <th className="num">Spent</th>
              <th>Last visit</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {list?.map((c) => (
              <tr key={c.id} className="clickable" onClick={() => setEditing(c)}>
                <td><b>{c.name}</b></td>
                <td style={{ whiteSpace: 'nowrap' }}>{formatPhone(c.phone)}</td>
                <td>{c.discountBp > 0 ? <span className="pill green">{percent(c.discountBp)}%</span> : <span className="muted">—</span>}</td>
                <td className="num">{c.visits}</td>
                <td className="num">{rupees(c.spentPaise)}</td>
                <td style={{ whiteSpace: 'nowrap' }}>{c.lastVisitAt ? dateTime(c.lastVisitAt) : <span className="muted">—</span>}</td>
                <td><button className="link-btn" onClick={(e) => { e.stopPropagation(); setEditing(c) }}><Icon name="edit" />Edit</button></td>
              </tr>
            ))}
            {list && list.length === 0 && (
              <tr><td colSpan={7} className="empty">{q.trim() ? 'No customer found.' : 'No customers yet. Add one here or from a bill.'}</td></tr>
            )}
          </tbody>
        </table>
        {list && list.length === 50 && <p className="muted" style={{ padding: '8px 16px' }}>Showing the 50 most recent. Search to find others.</p>}
      </div>

      {editing && (
        <Modal
          title={editing === 'new' ? 'New customer' : editing.name}
          subtitle={editing === 'new' ? undefined : `${plural(editing.visits, 'visit')} · ${rupees(editing.spentPaise)} spent`}
          onClose={() => setEditing(null)}
        >
          <CustomerForm
            customer={editing === 'new' ? null : editing}
            initial={editing === 'new' ? (isPhoneText(q) ? { phone: q.trim() } : { name: q.trim() }) : undefined}
            onSaved={saved}
            onCancel={() => setEditing(null)}
          />
        </Modal>
      )}
    </div>
  )
}
