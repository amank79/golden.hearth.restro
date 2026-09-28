import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react'
import { BillTotals } from '../../components/BillTotals'
import { FoodMark, Icon } from '../../components/Icon'
import { toast } from '../../components/toast'
import { api, errorText } from '../../lib/api'
import { billLabel, minutesAgo, notifyBillsChanged } from '../../lib/bills'
import { dishName, searchItems } from '../../lib/menuSearch'
import { rupees, rupeesShort } from '../../lib/money'
import type { Bill, BillLine, BillSummary, CancelResult, DiscountKind, Menu, MenuItem, PaymentMethod, Variant } from '../../lib/types'
import { CancelDialog } from './CancelDialog'
import { DiscountDialog } from './DiscountDialog'
import { LineEditor } from './LineEditor'
import { PaymentDialog } from './PaymentDialog'
import { TablePicker } from './TablePicker'
import { VariantPicker } from './VariantPicker'

type Dialog =
  | { kind: 'variant'; item: MenuItem; qty: number }
  | { kind: 'table'; mode: 'new' | 'change' }
  | { kind: 'line'; line: BillLine }
  | { kind: 'discount' }
  | { kind: 'pay'; method: PaymentMethod }
  | { kind: 'cancel' }

interface Props {
  /** Prints the bill (the first print gives it its number) and shows it; returns the updated bill. */
  onPrint: (bill: Bill) => Promise<Bill | null>
  /** Shows the bill as it will print, without printing it. */
  onPreview: (bill: Bill) => void
}

/** "3*bn" or "3 x bn" → quantity 3, search "bn". */
function parseQuery(text: string): { qty: number; q: string } {
  const m = /^\s*(\d{1,3})\s*[*x×]\s*(.*)$/i.exec(text)
  return m ? { qty: Math.max(1, Math.min(999, Number(m[1]))), q: m[2] } : { qty: 1, q: text }
}

function billIdFromHash(): number | null {
  const [screen, id] = window.location.hash.slice(1).split('/')
  return screen === 'billing' && id ? Number(id) || null : null
}

/** Counter billing: open bills, menu grid with search and categories, current bill with totals and payment. */
export function BillingScreen({ onPrint, onPreview }: Props) {
  const [menu, setMenu] = useState<Menu | null>(null)
  const [openBills, setOpenBills] = useState<BillSummary[]>([])
  const [bill, setBill] = useState<Bill | null>(null)
  const [cat, setCat] = useState<number | 'all'>('all')
  const [query, setQuery] = useState('')
  const [highlight, setHighlight] = useState(0)
  const [dialog, setDialog] = useState<Dialog | null>(null)
  const [now, setNow] = useState(() => Date.now())
  const searchRef = useRef<HTMLInputElement>(null)
  const queue = useRef<Promise<unknown>>(Promise.resolve())

  // Changes run one after another, so quick taps are never lost or applied out of order.
  const run = useCallback(<T,>(action: () => Promise<T>): Promise<T | undefined> => {
    const next = queue.current.then(action).catch((e: unknown) => {
      toast(errorText(e), 'error')
      return undefined
    })
    queue.current = next
    return next
  }, [])

  const loadOpen = useCallback(() => api.get<BillSummary[]>('/bills/open').then(setOpenBills, () => {}), [])
  const loadMenu = useCallback(() => api.get<Menu>('/menu').then(setMenu, (e) => toast(errorText(e), 'error')), [])

  const showBill = useCallback((b: Bill | null) => {
    setBill(b)
    const hash = b ? `billing/${b.id}` : 'billing'
    if (window.location.hash.slice(1) !== hash) window.history.replaceState(null, '', `#${hash}`)
  }, [])

  const selectBill = useCallback(
    (id: number) =>
      run(async () => {
        const b = await api.get<Bill>(`/bills/${id}`)
        showBill(b.status === 'Open' ? b : null)
      }),
    [run, showBill],
  )

  // Apply a change to the current bill and refresh the tabs.
  const change = useCallback(
    (action: () => Promise<Bill>) =>
      run(async () => {
        const b = await action()
        showBill(b.status === 'Open' ? b : null)
        void loadOpen()
        notifyBillsChanged()
        return b
      }),
    [run, showBill, loadOpen],
  )

  useEffect(() => {
    void loadMenu()
    void loadOpen()
    const id = billIdFromHash()
    if (id) void selectBill(id)
    // Keep "not available" switches and other screens' changes fresh.
    const t = window.setInterval(() => {
      setNow(Date.now())
      void loadMenu()
      void loadOpen()
    }, 30000)
    return () => window.clearInterval(t)
  }, [loadMenu, loadOpen, selectBill])

  const newBill = (orderType: 'DineIn' | 'Takeaway', table: string | null) =>
    change(() => api.post<Bill>('/bills', { orderType, tableLabel: table })).then((b) => {
      if (b) {
        toast(`${billLabel(b)} started.`)
        searchRef.current?.focus()
      }
    })

  const addLine = (variant: Variant, qty: number, item: MenuItem) => {
    if (!bill) return
    void change(() => api.post<Bill>(`/bills/${bill.id}/lines`, { itemVariantId: variant.id, qty, note: null })).then((b) => {
      if (b) toast(`${qty} × ${dishName(item.name, variant.name)} added.`)
    })
  }

  const tapItem = (item: MenuItem, qty = 1) => {
    if (!item.isAvailable) return toast(`${item.name} is marked not available today (see Menu).`, 'error')
    if (!bill) return toast('First start a bill: + Dine-in (F3) or + Takeaway (F4).', 'error')
    if (bill.isFinalised) return toast('This bill is already printed. Cancel it and re-bill to change it.', 'error')
    if (item.variants.length === 1) addLine(item.variants[0], qty, item)
    else setDialog({ kind: 'variant', item, qty })
    setQuery('')
    setHighlight(0)
  }

  const setQty = (line: BillLine, qty: number) =>
    bill &&
    change(() =>
      qty <= 0
        ? api.del<Bill>(`/bills/${bill.id}/lines/${line.id}`)
        : api.put<Bill>(`/bills/${bill.id}/lines/${line.id}`, { qty, note: line.note }),
    )

  const print = async () => {
    if (!bill) return
    if (bill.lines.length === 0) return toast('Add at least one dish before printing.', 'error')
    const printed = await run(() => onPrint(bill))
    if (printed) {
      showBill(printed.status === 'Open' ? printed : null)
      void loadOpen()
      notifyBillsChanged()
    }
  }

  const pay = (method: PaymentMethod) => {
    if (!bill) return
    if (bill.lines.length === 0) return toast('Add dishes before taking payment.', 'error')
    setDialog({ kind: 'pay', method })
  }

  // Menu to show: search looks in every category; otherwise the chosen category.
  const { qty: searchQty, q } = parseQuery(query)
  const items = useMemo(() => {
    if (!menu) return []
    if (q.trim()) return searchItems(menu.items, q)
    return cat === 'all' ? menu.items : menu.items.filter((i) => i.categoryId === cat)
  }, [menu, cat, q])
  const top = q.trim() ? items[Math.min(highlight, items.length - 1)] : undefined

  const inBill = useMemo(() => {
    const m = new Map<number, number>()
    bill?.lines.forEach((l) => m.set(l.menuItemId, (m.get(l.menuItemId) ?? 0) + l.qty))
    return m
  }, [bill])

  // Counter shortcuts. Dialogs handle their own keys.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (dialog || document.querySelector('.overlay')) return // any dialog open (also the print preview)
      const inField = e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement
      if (e.key === 'F2' || (e.key === '/' && !inField)) {
        e.preventDefault()
        searchRef.current?.focus()
        searchRef.current?.select()
      } else if (e.key === 'F3') {
        e.preventDefault()
        setDialog({ kind: 'table', mode: 'new' })
      } else if (e.key === 'F4') {
        e.preventDefault()
        void newBill('Takeaway', null)
      } else if (e.key === 'F8') {
        e.preventDefault()
        pay('Cash')
      } else if (e.key === 'F9') {
        e.preventDefault()
        void print()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  })

  const onSearchKey = (e: ReactKeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' && top) {
      e.preventDefault()
      tapItem(top, searchQty)
    } else if (e.key === 'ArrowDown') {
      e.preventDefault()
      setHighlight((h) => Math.min(h + 1, Math.max(0, items.length - 1)))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setHighlight((h) => Math.max(0, h - 1))
    } else if (e.key === 'Escape') {
      setQuery('')
      setHighlight(0)
    }
  }

  const closeDialog = () => {
    setDialog(null)
    window.setTimeout(() => searchRef.current?.focus(), 0)
  }

  const editable = bill !== null && !bill.isFinalised

  return (
    <div className="pos-view" style={{ height: '100%' }}>
      <div className="open-bills" aria-label="Open bills">
        <button className="bill-tab new" onClick={() => setDialog({ kind: 'table', mode: 'new' })}>
          + Dine-in <kbd>F3</kbd>
        </button>
        <button className="bill-tab new" onClick={() => void newBill('Takeaway', null)}>
          + Takeaway <kbd>F4</kbd>
        </button>
        {openBills.map((b) => (
          <button
            key={b.id}
            className={`bill-tab${bill?.id === b.id ? ' on' : ''}${b.billNo ? ' printed' : ''}`}
            onClick={() => void selectBill(b.id)}
          >
            <b>{billLabel(b)}</b>
            <small>{b.billNo ? 'Printed · ' : ''}{rupeesShort(b.totalPaise)} · {minutesAgo(b.openedAt, now)}</small>
          </button>
        ))}
      </div>

      <div className="pos">
        <div className="cats">
          <button className={`cat-btn${cat === 'all' ? ' active' : ''}`} onClick={() => setCat('all')}>All</button>
          {menu?.categories.map((c) => (
            <button key={c.id} className={`cat-btn${cat === c.id ? ' active' : ''}`} onClick={() => { setCat(c.id); setQuery('') }}>
              {c.name}
            </button>
          ))}
        </div>

        <div className="items-col">
          <label className="search">
            <Icon name="search" />
            <input
              ref={searchRef}
              placeholder="Search dish or code… (e.g. pbm, 3*naan)"
              value={query}
              onChange={(e) => { setQuery(e.target.value); setHighlight(0) }}
              onKeyDown={onSearchKey}
              autoFocus
              aria-label="Search dish"
            />
            <kbd>F2</kbd>
          </label>
          {q.trim() && (
            <div className="search-hint">
              {top ? <>Enter adds {searchQty > 1 ? `${searchQty} × ` : ''}<b>{top.name}</b> · ↑↓ to choose · Esc to clear</> : 'No dish found.'}
            </div>
          )}
          <div className="items-grid">
            {items.map((m) => (
              <button key={m.id} className={`item-btn${m.isAvailable ? '' : ' na'}${m === top ? ' hl' : ''}`} onClick={() => tapItem(m, searchQty)}>
                {inBill.get(m.id) ? <span className="qbadge">{inBill.get(m.id)}</span> : null}
                <span className="top"><FoodMark type={m.foodType} />{m.shortCode && <span className="sc">{m.shortCode}</span>}</span>
                <span className="nm">{m.name}</span>
                {m.variants.length > 1 && <span className="var">{m.variants.map((v) => v.name).join(' / ')}</span>}
                <span className="pr">
                  {m.isAvailable ? m.variants.map((v) => rupeesShort(v.pricePaise)).join(' / ') : 'Not available today'}
                </span>
              </button>
            ))}
            {menu && items.length === 0 && !q.trim() && (
              <div className="empty">No dishes here yet. Add them in <a href="#menu">Menu</a>.</div>
            )}
          </div>
        </div>

        <div className="order card">
          {bill ? (
            <>
              <div className="order-head">
                <div className="t">
                  {billLabel(bill)}
                  <span className={`pill${bill.isFinalised ? ' blue' : ''}`}>
                    {bill.isFinalised ? `Printed · ${bill.billNo}` : bill.orderType === 'DineIn' ? 'Dine-in' : 'Takeaway'}
                  </span>
                </div>
                <div className="sub">
                  Started {minutesAgo(bill.openedAt, now)}
                  {bill.lines.length > 0 && (
                    <> · <button className="link-btn" onClick={() => onPreview(bill)}>Preview bill</button></>
                  )}
                  {editable && bill.orderType === 'DineIn' && (
                    <> · <button className="link-btn" onClick={() => setDialog({ kind: 'table', mode: 'change' })}>Change table</button></>
                  )}
                </div>
              </div>

              <div className="order-lines">
                {bill.isFinalised && (
                  <div className="note" style={{ marginTop: 8 }}>
                    This bill is printed, so it cannot be changed. To change it, cancel it and copy it to a new bill.
                  </div>
                )}
                {bill.lines.map((l) => (
                  <div className="ol" key={l.id}>
                    <button className="n" onClick={() => editable && setDialog({ kind: 'line', line: l })} disabled={!editable}>
                      {l.itemName}
                      <small>
                        {l.variantName !== 'Regular' && `${l.variantName} · `}{rupeesShort(l.unitPricePaise)} each
                        {l.note && <> · <span className="line-note">{l.note}</span></>}
                      </small>
                    </button>
                    <div className="qty">
                      <button onClick={() => void setQty(l, l.qty - 1)} disabled={!editable} aria-label={`One less ${l.itemName}`}>−</button>
                      <span>{l.qty}</span>
                      <button onClick={() => void setQty(l, l.qty + 1)} disabled={!editable} aria-label={`One more ${l.itemName}`}>+</button>
                    </div>
                    <div className="lt">{rupees(l.amountPaise)}</div>
                  </div>
                ))}
                {bill.lines.length === 0 && <div className="empty">Tap dishes on the left, or type a name or code and press Enter.</div>}
              </div>

              <BillTotals bill={bill} compact onDiscount={editable && bill.lines.length > 0 ? () => setDialog({ kind: 'discount' }) : undefined} />

              <div className="order-actions">
                <button className="btn dark" onClick={() => void print()} disabled={bill.lines.length === 0}>
                  <Icon name="print" />{bill.isFinalised ? 'Print again' : 'Print bill'} <kbd>F9</kbd>
                </button>
                <button className="btn danger" onClick={() => setDialog({ kind: 'cancel' })}>Cancel bill</button>
                <div className="pay-row">
                  <button className="btn green big" onClick={() => pay('Cash')} disabled={bill.lines.length === 0}>Cash <kbd>F8</kbd></button>
                  <button className="btn green big" onClick={() => pay('Upi')} disabled={bill.lines.length === 0}>UPI</button>
                  <button className="btn green big" onClick={() => pay('Card')} disabled={bill.lines.length === 0}>Card</button>
                </div>
              </div>
            </>
          ) : (
            <div className="empty">
              <b style={{ color: 'var(--ink)', fontSize: 17 }}>No bill selected</b>
              <br />
              Pick an open bill above, or start a new one.
              <div className="row" style={{ justifyContent: 'center', marginTop: 14 }}>
                <button className="btn primary big" onClick={() => setDialog({ kind: 'table', mode: 'new' })}>+ Dine-in</button>
                <button className="btn primary big" onClick={() => void newBill('Takeaway', null)}>+ Takeaway</button>
              </div>
            </div>
          )}
        </div>
      </div>

      {dialog?.kind === 'variant' && (
        <VariantPicker
          item={dialog.item}
          qty={dialog.qty}
          onPick={(v) => {
            addLine(v, dialog.qty, dialog.item)
            closeDialog()
          }}
          onClose={closeDialog}
        />
      )}
      {dialog?.kind === 'table' && (
        <TablePicker
          title={dialog.mode === 'new' ? 'New dine-in bill' : 'Change table'}
          openBills={openBills}
          currentBillId={dialog.mode === 'change' ? bill?.id : undefined}
          onClose={closeDialog}
          onOpenExisting={(id) => {
            closeDialog()
            void selectBill(id)
          }}
          onPick={(table) => {
            closeDialog()
            if (dialog.mode === 'new') void newBill('DineIn', table)
            else if (bill) void change(() => api.put<Bill>(`/bills/${bill.id}/table`, { orderType: 'DineIn', tableLabel: table }))
          }}
        />
      )}
      {dialog?.kind === 'line' && bill && (
        <LineEditor
          line={dialog.line}
          onClose={closeDialog}
          onRemove={() => {
            closeDialog()
            void setQty(dialog.line, 0)
          }}
          onSave={(qty, note) => {
            closeDialog()
            void change(() => api.put<Bill>(`/bills/${bill.id}/lines/${dialog.line.id}`, { qty, note }))
          }}
        />
      )}
      {dialog?.kind === 'discount' && bill && (
        <DiscountDialog
          bill={bill}
          onClose={closeDialog}
          onSave={(kind: DiscountKind, value, reason) => {
            closeDialog()
            void change(() => api.put<Bill>(`/bills/${bill.id}/discount`, { kind, value, reason }))
          }}
        />
      )}
      {dialog?.kind === 'pay' && bill && (
        <PaymentDialog
          bill={bill}
          initialMethod={dialog.method}
          onClose={closeDialog}
          onConfirm={(payments, changePaise) => {
            closeDialog()
            void change(() => api.post<Bill>(`/bills/${bill.id}/payments`, { payments })).then((b) => {
              if (b) {
                toast(`${billLabel(b)} paid: ${rupees(b.totalPaise)}.${changePaise > 0 ? ` Give back ${rupees(changePaise)} change.` : ''} Bill ${b.billNo}.`)
                // Paid without printing first: print it now so the customer always gets a bill.
                if (b.printCount === 0) void onPrint(b)
              }
            })
          }}
        />
      )}
      {dialog?.kind === 'cancel' && bill && (
        <CancelDialog
          bill={bill}
          onClose={closeDialog}
          onConfirm={(reason, copyToNewBill) => {
            closeDialog()
            void run(async () => {
              const r = await api.post<CancelResult>(`/bills/${bill.id}/cancel`, { reason, copyToNewBill })
              showBill(r.newBill)
              void loadOpen()
              notifyBillsChanged()
              toast(r.newBill ? `Bill cancelled. Dishes copied to a new bill for ${billLabel(r.newBill)}.` : 'Bill cancelled.')
            })
          }}
        />
      )}
    </div>
  )
}
