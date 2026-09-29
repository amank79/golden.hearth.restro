import { useCallback, useEffect, useState } from 'react'
import { BillTotals } from '../../components/BillTotals'
import { Icon } from '../../components/Icon'
import { toast } from '../../components/toast'
import { api, errorText } from '../../lib/api'
import { billLabel, dateTime, indiaToday, methodLabel, notifyBillsChanged, plural } from '../../lib/bills'
import { rupees, rupeesShort } from '../../lib/money'
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
  { value: 'Open', label: 'Open / not paid' },
  { value: 'Cancelled', label: 'Cancelled' },
]

function StatusPill({ b }: { b: Pick<BillSummary, 'status' | 'billNo'> }) {
  if (b.status === 'Paid') return <span className="pill green">Paid</span>
  if (b.status === 'Cancelled') return <span className="pill red">Cancelled</span>
  return <span className="pill amber">{b.billNo ? 'Printed, not paid' : 'Open'}</span>
}

function yesterday(): string {
  const d = new Date(`${indiaToday()}T12:00:00`)
  d.setDate(d.getDate() - 1)
  return d.toISOString().slice(0, 10)
}

/** Bill history: today's totals, search and filters, and the chosen bill with reprint and cancel. */
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
  const setDates = (f: string, t: string) => {
    setFrom(f)
    setTo(t)
  }
  const isToday = from === indiaToday() && to === indiaToday()
  const isYesterday = from === yesterday() && to === yesterday()

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h1>Bill history</h1>
          <p>Find any bill, print it again, or cancel it. Cancelled bills stay here.</p>
        </div>
      </div>

      {today && (
        <div className="ledger" aria-label="Today">
          <div><span>Today's total · {plural(today.billCount, 'bill')}</span><b>{rupees(today.totalPaise)}</b></div>
          <div><span><Icon name="cash" />Cash</span><b>{rupees(byMethod('Cash'))}</b></div>
          <div><span><Icon name="upi" />UPI</span><b>{rupees(byMethod('Upi'))}</b></div>
          <div><span><Icon name="card" />Card</span><b>{rupees(byMethod('Card'))}</b></div>
          {today.unpaidPaise > 0 && <div className="warn-fig"><span>Printed, not paid</span><b>{rupees(today.unpaidPaise)}</b></div>}
          <div><span><Icon name="cancel" />Cancelled</span><b>{today.cancelledCount}</b></div>
        </div>
      )}

      <div className="filters">
        <label className="search">
          <Icon name="search" />
          <input placeholder="Bill no. (e.g. 123), table or dish…" value={q} onChange={(e) => setQ(e.target.value)} autoFocus />
          {q && <button className="icon-btn" onClick={() => setQ('')} aria-label="Clear search"><Icon name="close" /></button>}
        </label>
        <button className={`chip${isToday ? ' on' : ''}`} onClick={() => setDates(indiaToday(), indiaToday())}>Today</button>
        <button className={`chip${isYesterday ? ' on' : ''}`} onClick={() => setDates(yesterday(), yesterday())}>Yesterday</button>
        <button className={`chip${!from && !to ? ' on' : ''}`} onClick={() => setDates('', '')}>All dates</button>
        <label className="date">From <input type="date" className="input" value={from} onChange={(e) => setFrom(e.target.value)} /></label>
        <label className="date">To <input type="date" className="input" value={to} onChange={(e) => setTo(e.target.value)} /></label>
      </div>
      <div className="chipbar" style={{ marginBottom: 12, flex: 'none' }}>
        {STATUSES.map((s) => (
          <button key={s.label} className={`chip${status === s.value ? ' on' : ''}`} onClick={() => setStatus(s.value)}>{s.label}</button>
        ))}
        {page && <span className="muted" style={{ alignSelf: 'center', marginLeft: 'auto', fontSize: 14 }}>{plural(page.total, 'bill')} found</span>}
      </div>

      <div className="history">
        <div className="card">
          <table className="list-table">
            <thead>
              <tr>
                <th>Bill no.</th>
                <th>Time</th>
                <th>Table</th>
                <th className="num">Total</th>
                <th>Paid by</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {page?.items.map((b) => (
                <tr key={b.id} className={`clickable${detail?.id === b.id ? ' sel' : ''}`} onClick={() => void open(b.id)}>
                  <td><b>{b.billNo ?? '—'}</b></td>
                  <td style={{ whiteSpace: 'nowrap' }}>{dateTime(b.finalisedAt ?? b.openedAt)}</td>
                  <td>{billLabel(b)}</td>
                  <td className="num"><b>{rupees(b.totalPaise)}</b></td>
                  <td>{b.paymentMethods.map(methodLabel).join(' + ')}</td>
                  <td><StatusPill b={b} /></td>
                </tr>
              ))}
              {page && page.items.length === 0 && <tr><td colSpan={6} className="empty">No bills found. Try “All dates”.</td></tr>}
            </tbody>
          </table>
          {page && page.items.length < page.total && (
            <div className="row" style={{ justifyContent: 'center', padding: 12 }}>
              <button className="btn" onClick={() => void more()}>Show more ({page.total - page.items.length} left)</button>
            </div>
          )}
        </div>

        <aside className="receipt-col" aria-label="Bill details">
          {!detail ? (
            <>
              <div className="paper"><div className="empty"><Icon name="receipt" /><br />Tap a bill in the list to see it here.</div></div>
              <div className="paper-foot" />
            </>
          ) : (
            <>
              <div className="paper detail-body">
                <header className="r-head">
                  <div className="r-kicker">{detail.documentTitle}</div>
                  <div className="r-title">{detail.billNo ? `Bill ${detail.billNo}` : billLabel(detail)}</div>
                  <div className="r-sub"><StatusPill b={detail} /></div>
                </header>
                <div className="detail-meta">
                  <span>Table</span><div>{billLabel(detail)}</div>
                  <span>Opened</span><div>{dateTime(detail.openedAt)}</div>
                  <span>Printed</span><div>{detail.finalisedAt ? `${dateTime(detail.finalisedAt)} · ${detail.printCount}×` : 'not yet'}</div>
                  <span>Paid</span><div>{detail.payments.length ? detail.payments.map((p) => `${methodLabel(p.method)} ${rupees(p.amountPaise)}`).join(' + ') : '—'}</div>
                </div>
                {detail.status === 'Cancelled' && (
                  <div className="detail-cancelled">
                    <b>Cancelled</b> {detail.cancelledAt && dateTime(detail.cancelledAt)} · {detail.cancelReason}
                  </div>
                )}
                <div className="detail-lines">
                  {detail.lines.map((l) => (
                    <div className="detail-line" key={l.id}>
                      <div>
                        {l.itemName}{l.variantName !== 'Regular' && ` (${l.variantName})`}
                        <small>{l.qty} × {rupeesShort(l.unitPricePaise)}{l.note && ` · ${l.note}`}</small>
                      </div>
                      <div className="num">{rupees(l.amountPaise)}</div>
                    </div>
                  ))}
                  {detail.lines.length === 0 && <div className="empty">No dishes on this bill.</div>}
                </div>
                {detail.lines.length > 0 && <BillTotals bill={detail} />}
              </div>
              <div className="detail-actions">
                {detail.status !== 'Cancelled' && detail.lines.length > 0 && (
                  <button className="btn dark wide" onClick={() => void reprint()}>
                    <Icon name="print" />{detail.printCount > 0 ? 'Print again (DUPLICATE)' : 'Print'}
                  </button>
                )}
                {detail.lines.length > 0 && <button className="btn" onClick={() => onPreview(detail)}><Icon name="eye" />View bill</button>}
                {detail.status === 'Open' && <a className="btn" href={`#billing/${detail.id}`}><Icon name="open" />Open in Billing</a>}
                {detail.status !== 'Cancelled' && (
                  <button className="btn danger" onClick={() => setCancelling(true)}><Icon name="cancel" />Cancel bill</button>
                )}
              </div>
            </>
          )}
        </aside>
      </div>

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
    </div>
  )
}
