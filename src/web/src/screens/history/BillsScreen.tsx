import { useCallback, useEffect, useState } from 'react'
import { BillTotals } from '../../components/BillTotals'
import { Icon } from '../../components/Icon'
import { Modal } from '../../components/Modal'
import { toast } from '../../components/toast'
import { api, errorText } from '../../lib/api'
import { billLabel, dateTime, indiaToday, methodLabel, notifyBillsChanged } from '../../lib/bills'
import { dishName } from '../../lib/menuSearch'
import { rupees } from '../../lib/money'
import type { Bill, BillPage, BillStatus, BillSummary, CancelResult, TodaySummary } from '../../lib/types'
import { CancelDialog } from '../billing/CancelDialog'

interface Props {
  onPrint: (bill: Bill) => Promise<Bill | null>
  onPreview: (bill: Bill) => void
}

const PAGE = 50
const STATUSES: { value: BillStatus | ''; label: string }[] = [
  { value: '', label: 'All' },
  { value: 'Paid', label: 'Paid' },
  { value: 'Open', label: 'Open' },
  { value: 'Cancelled', label: 'Cancelled' },
]

function StatusPill({ b }: { b: Pick<BillSummary, 'status' | 'billNo'> }) {
  if (b.status === 'Paid') return <span className="pill green">Paid</span>
  if (b.status === 'Cancelled') return <span className="pill red">Cancelled</span>
  return <span className="pill amber">{b.billNo ? 'Printed, not paid' : 'Open'}</span>
}

/** Bill history with search, today's total, bill details, reprint and cancel. */
export function BillsScreen({ onPrint, onPreview }: Props) {
  const [today, setToday] = useState<TodaySummary | null>(null)
  const [q, setQ] = useState('')
  const [from, setFrom] = useState(indiaToday)
  const [to, setTo] = useState(indiaToday)
  const [status, setStatus] = useState<BillStatus | ''>('')
  const [page, setPage] = useState<BillPage | null>(null)
  const [detail, setDetail] = useState<Bill | null>(null)
  const [cancelling, setCancelling] = useState(false)

  const params = useCallback(
    (skip: number) => {
      const p = new URLSearchParams({ skip: String(skip), take: String(PAGE) })
      if (q.trim()) p.set('q', q.trim())
      if (from) p.set('from', from)
      if (to) p.set('to', to)
      if (status) p.set('status', status)
      return p.toString()
    },
    [q, from, to, status],
  )

  const load = useCallback(() => {
    api.get<BillPage>(`/bills?${params(0)}`).then(setPage, (e) => toast(errorText(e), 'error'))
    api.get<TodaySummary>('/bills/today').then(setToday, () => {})
  }, [params])

  useEffect(() => {
    const t = window.setTimeout(load, 200) // wait for typing to pause
    return () => window.clearTimeout(t)
  }, [load])

  const more = () =>
    page && api.get<BillPage>(`/bills?${params(page.items.length)}`).then((p) => setPage({ items: [...page.items, ...p.items], total: p.total }))

  const open = (id: number) => api.get<Bill>(`/bills/${id}`).then(setDetail, (e) => toast(errorText(e), 'error'))

  const reprint = async () => {
    if (!detail) return
    const b = await onPrint(detail)
    if (b) {
      setDetail(b)
      load()
    }
  }

  const byMethod = (m: string) => today?.byMethod.find((x) => x.method === m)?.amountPaise ?? 0

  return (
    <>
      {today && (
        <div className="stat-chips">
          <div className="stat-chip"><b>{rupees(today.totalPaise)}</b><span>Today's total · {today.billCount} bills</span></div>
          <div className="stat-chip"><b>{rupees(byMethod('Cash'))}</b><span>Cash</span></div>
          <div className="stat-chip"><b>{rupees(byMethod('Upi'))}</b><span>UPI</span></div>
          <div className="stat-chip"><b>{rupees(byMethod('Card'))}</b><span>Card</span></div>
          {today.unpaidPaise > 0 && <div className="stat-chip"><b>{rupees(today.unpaidPaise)}</b><span>Printed, not paid yet</span></div>}
          <div className="stat-chip"><b>{today.cancelledCount}</b><span>Cancelled today</span></div>
        </div>
      )}

      <div className="history-bar">
        <label className="search">
          <Icon name="search" />
          <input placeholder="Bill no. (e.g. 123), table or dish…" value={q} onChange={(e) => setQ(e.target.value)} autoFocus />
        </label>
        <label className="row" style={{ gap: 6 }}>From <input type="date" className="input" style={{ width: 160 }} value={from} onChange={(e) => setFrom(e.target.value)} /></label>
        <label className="row" style={{ gap: 6 }}>To <input type="date" className="input" style={{ width: 160 }} value={to} onChange={(e) => setTo(e.target.value)} /></label>
        <button className="btn small" onClick={() => { setFrom(''); setTo('') }}>All dates</button>
      </div>
      <div className="chipbar" style={{ marginBottom: 12 }}>
        {STATUSES.map((s) => (
          <button key={s.label} className={`chip${status === s.value ? ' on' : ''}`} onClick={() => setStatus(s.value)}>{s.label}</button>
        ))}
      </div>

      <div className="card" style={{ overflow: 'auto' }}>
        <table className="list-table">
          <thead>
            <tr>
              <th>Bill no.</th>
              <th>Date / time</th>
              <th>Table</th>
              <th className="num">Items</th>
              <th className="num">Total</th>
              <th>Paid by</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {page?.items.map((b) => (
              <tr key={b.id} className="clickable" onClick={() => void open(b.id)}>
                <td><b>{b.billNo ?? '—'}</b></td>
                <td>{dateTime(b.finalisedAt ?? b.openedAt)}</td>
                <td>{billLabel(b)}</td>
                <td className="num">{b.itemCount}</td>
                <td className="num"><b>{rupees(b.totalPaise)}</b></td>
                <td>{b.paymentMethods.map(methodLabel).join(' + ')}</td>
                <td><StatusPill b={b} /></td>
              </tr>
            ))}
            {page && page.items.length === 0 && <tr><td colSpan={7} className="empty">No bills found.</td></tr>}
          </tbody>
        </table>
      </div>
      {page && page.items.length < page.total && (
        <div className="row" style={{ justifyContent: 'center', marginTop: 12 }}>
          <button className="btn" onClick={() => void more()}>Show more ({page.total - page.items.length} left)</button>
        </div>
      )}

      {detail && !cancelling && (
        <Modal title={detail.billNo ? `Bill ${detail.billNo}` : billLabel(detail)} onClose={() => setDetail(null)} wide>
          <div className="detail-grid">
            <div><span>Table: </span>{billLabel(detail)}</div>
            <div><span>Status: </span><StatusPill b={detail} /></div>
            <div><span>Opened: </span>{dateTime(detail.openedAt)}</div>
            <div><span>Printed: </span>{detail.finalisedAt ? `${dateTime(detail.finalisedAt)} (${detail.printCount}×)` : 'not yet'}</div>
            <div><span>Document: </span>{detail.documentTitle}</div>
            <div><span>Paid: </span>{detail.payments.length ? detail.payments.map((p) => `${methodLabel(p.method)} ${rupees(p.amountPaise)}`).join(', ') : '—'}</div>
            {detail.status === 'Cancelled' && (
              <div style={{ gridColumn: '1 / -1', color: 'var(--red)' }}>
                <b>Cancelled</b> {detail.cancelledAt && dateTime(detail.cancelledAt)} · Reason: {detail.cancelReason}
              </div>
            )}
          </div>
          <div className="card" style={{ overflow: 'hidden' }}>
            <table className="list-table">
              <thead><tr><th>Dish</th><th className="num">Qty</th><th className="num">Rate</th><th className="num">Amount</th></tr></thead>
              <tbody>
                {detail.lines.map((l) => (
                  <tr key={l.id}>
                    <td>{dishName(l.itemName, l.variantName)}{l.note && <small className="muted"> · {l.note}</small>}</td>
                    <td className="num">{l.qty}</td>
                    <td className="num">{rupees(l.unitPricePaise)}</td>
                    <td className="num">{rupees(l.amountPaise)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <BillTotals bill={detail} />
          </div>
          <div className="modal-actions">
            {detail.status === 'Open' && <a className="btn" href={`#billing/${detail.id}`}>Open in Billing</a>}
            {detail.status !== 'Cancelled' && <button className="btn danger" onClick={() => setCancelling(true)}>Cancel bill</button>}
            {detail.lines.length > 0 && <button className="btn" onClick={() => onPreview(detail)}>View bill</button>}
            {detail.status !== 'Cancelled' && detail.lines.length > 0 && (
              <button className="btn dark" onClick={() => void reprint()}>
                <Icon name="print" />{detail.printCount > 0 ? 'Reprint (DUPLICATE)' : 'Print'}
              </button>
            )}
            <button className="btn" onClick={() => setDetail(null)}>Close</button>
          </div>
        </Modal>
      )}
      {detail && cancelling && (
        <CancelDialog
          bill={detail}
          onClose={() => setCancelling(false)}
          onConfirm={(reason, copyToNewBill) => {
            api.post<CancelResult>(`/bills/${detail.id}/cancel`, { reason, copyToNewBill }).then(
              (r) => {
                setCancelling(false)
                setDetail(r.cancelled)
                notifyBillsChanged()
                load()
                toast(r.newBill ? `Bill cancelled. A new bill was opened for ${billLabel(r.newBill)} in Billing.` : 'Bill cancelled.')
              },
              (e) => toast(errorText(e), 'error'),
            )
          }}
        />
      )}
    </>
  )
}
