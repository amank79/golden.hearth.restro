import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react'
import { Icon } from '../../components/Icon'
import { toast } from '../../components/toast'
import { api, errorText } from '../../lib/api'
import { billLabel, minutesAgo, notifyBillsChanged } from '../../lib/bills'
import { searchItems } from '../../lib/menuSearch'
import { rupees, rupeesShort } from '../../lib/money'
import type { Bill, BillLine, BillSummary, CancelResult, DiscountKind, Menu, MenuItem, PaymentMethod, Variant } from '../../lib/types'
import { BillPanel } from './BillPanel'
import { CancelDialog } from './CancelDialog'
import { DiscountDialog } from './DiscountDialog'
import { ItemTile } from './ItemTile'
import { LineEditor } from './LineEditor'
import { PaymentDialog } from './PaymentDialog'
import { TablePicker } from './TablePicker'
import { VariantPicker } from './VariantPicker'

/** A dish tapped before any bill was open; it is added once the bill is started. */
interface Pending {
  item: MenuItem
  variant?: Variant
  qty: number
}

type Dialog =
  | { kind: 'variant'; item: MenuItem; qty: number }
  | { kind: 'table'; mode: 'new' | 'change'; pending?: Pending }
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

/** Counter billing: open bills along the top, categories, dish grid with search, and the current bill. */
export function BillingScreen({ onPrint, onPreview }: Props) {
  const [menu, setMenu] = useState<Menu | null>(null)
  const [openBills, setOpenBills] = useState<BillSummary[]>([])
  const [bill, setBill] = useState<Bill | null>(null)
  const [cat, setCat] = useState<number | 'all'>('all')
  const [query, setQuery] = useState('')
  const [highlight, setHighlight] = useState(0)
  const [dialog, setDialog] = useState<Dialog | null>(null)
  const [flash, setFlash] = useState<{ lineId: number; n: number } | null>(null)
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
        return b.status === 'Open' ? b : undefined
      }),
    [run, showBill],
  )

  // Apply a change to the current bill and refresh the open bills.
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
    // Keep "not available" switches, other screens' changes and the "min ago" times fresh.
    const t = window.setInterval(() => {
      setNow(Date.now())
      void loadMenu()
      void loadOpen()
    }, 30000)
    return () => window.clearInterval(t)
  }, [loadMenu, loadOpen, selectBill])

  const focusSearch = () => window.setTimeout(() => searchRef.current?.focus(), 0)

  const addLine = (billId: number, variant: Variant, qty: number) =>
    change(() => api.post<Bill>(`/bills/${billId}/lines`, { itemVariantId: variant.id, qty, note: null })).then((b) => {
      // Same dish and size without a note is added to the existing line, so flash that line.
      const line = b?.lines.filter((l) => l.itemVariantId === variant.id && !l.note).at(-1)
      if (line) setFlash((f) => ({ lineId: line.id, n: (f?.n ?? 0) + 1 }))
    })

  /** Adds a dish to this bill, asking for the size if it has several and none was tapped. */
  const addTo = (target: Bill, p: Pending) => {
    if (p.variant) void addLine(target.id, p.variant, p.qty)
    else if (p.item.variants.length === 1) void addLine(target.id, p.item.variants[0], p.qty)
    else setDialog({ kind: 'variant', item: p.item, qty: p.qty })
  }

  const newBill = (orderType: 'DineIn' | 'Takeaway', table: string | null, pending?: Pending) =>
    change(() => api.post<Bill>('/bills', { orderType, tableLabel: table })).then((b) => {
      if (!b) return
      if (pending) addTo(b, pending)
      else toast(`${billLabel(b)} started. Add dishes.`)
      focusSearch()
    })

  const tapItem = (item: MenuItem, variant?: Variant, qty = 1) => {
    setQuery('')
    setHighlight(0)
    if (!item.isAvailable) return toast(`${item.name} is marked not available today (see Menu).`, 'error')
    // No bill yet: ask dine-in or takeaway, then add the dish.
    if (!bill) return setDialog({ kind: 'table', mode: 'new', pending: { item, variant, qty } })
    if (bill.isFinalised) return toast('This bill is already printed. Use Cancel and copy it to a new bill to change it.', 'error')
    addTo(bill, { item, variant, qty })
  }

  const setQty = (line: BillLine, qty: number) =>
    bill &&
    change(() =>
      qty <= 0
        ? api.del<Bill>(`/bills/${bill.id}/lines/${line.id}`)
        : api.put<Bill>(`/bills/${bill.id}/lines/${line.id}`, { qty, note: line.note }),
    ).then((b) => {
      if (b && qty > 0) setFlash((f) => ({ lineId: line.id, n: (f?.n ?? 0) + 1 }))
    })

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

  // Dishes to show: search looks in every category; otherwise the chosen category.
  const { qty: searchQty, q } = parseQuery(query)
  const searching = q.trim() !== ''
  const items = useMemo(() => {
    if (!menu) return []
    if (searching) return searchItems(menu.items, q)
    return cat === 'all' ? menu.items : menu.items.filter((i) => i.categoryId === cat)
  }, [menu, cat, q, searching])
  const top = searching ? items[Math.min(highlight, items.length - 1)] : undefined

  const counts = useMemo(() => {
    const m = new Map<number, number>()
    menu?.items.forEach((i) => m.set(i.categoryId, (m.get(i.categoryId) ?? 0) + 1))
    return m
  }, [menu])

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
      tapItem(top, top.variants.length === 1 ? top.variants[0] : undefined, searchQty)
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
    focusSearch()
  }

  return (
    <div className="billing">
      {/* Open bills as tickets across the whole width; the two big buttons start a new one. */}
      <div className="tickets" aria-label="Open bills">
        <button className="new-bill" onClick={() => setDialog({ kind: 'table', mode: 'new' })}>
          <Icon name="table" />Dine-in <kbd>F3</kbd>
        </button>
        <button className="new-bill" onClick={() => void newBill('Takeaway', null)}>
          <Icon name="bag" />Takeaway <kbd>F4</kbd>
        </button>
        <div className="ticket-list">
          {openBills.map((b) => (
            <button
              key={b.id}
              className={`ticket${bill?.id === b.id ? ' on' : ''}${b.billNo ? ' printed' : ''}`}
              onClick={() => void selectBill(b.id)}
              title={b.billNo ? 'Printed, waiting for payment' : 'Open bill'}
            >
              <b>{billLabel(b)}</b>
              <small>
                {b.billNo && <span className="tag">Printed · </span>}
                {rupeesShort(b.totalPaise)} · {minutesAgo(b.openedAt, now)}
              </small>
            </button>
          ))}
          {openBills.length === 0 && <div className="tickets-empty">No open bills. Start one, or just tap a dish.</div>}
        </div>
        {openBills.length > 0 && <div className="tickets-count">{openBills.length} open</div>}
      </div>

      <div className="counter">
        <nav className="catlist" aria-label="Categories">
          <div className="catlist-head">Menu</div>
          <button className={`catrow${cat === 'all' && !searching ? ' on' : ''}`} onClick={() => { setCat('all'); setQuery('') }}>
            All dishes<span className="n">{menu?.items.length ?? ''}</span>
          </button>
          {menu?.categories.filter((c) => counts.has(c.id)).map((c) => (
            <button key={c.id} className={`catrow${cat === c.id && !searching ? ' on' : ''}`} onClick={() => { setCat(c.id); setQuery('') }}>
              {c.name}<span className="n">{counts.get(c.id) ?? 0}</span>
            </button>
          ))}
        </nav>

        <section className="dishes" aria-label="Dishes">
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
            {query ? (
              <button className="icon-btn" onClick={() => { setQuery(''); searchRef.current?.focus() }} aria-label="Clear search"><Icon name="close" /></button>
            ) : (
              <kbd>F2</kbd>
            )}
          </label>
          {searching && (
            <div className="search-hint">
              {top
                ? <><kbd>Enter</kbd> adds {searchQty > 1 ? `${searchQty} × ` : ''}<b>{top.name}</b> · <kbd>↑</kbd><kbd>↓</kbd> choose · <kbd>Esc</kbd> clear</>
                : 'No dish found. Try the short code or the first letters of each word (e.g. pbm).'}
            </div>
          )}
          <div className="grid">
            {items.map((m) => (
              <ItemTile
                key={m.id}
                item={m}
                inBill={inBill.get(m.id) ?? 0}
                highlighted={m === top}
                onAdd={(item, variant) => tapItem(item, variant, searchQty)}
              />
            ))}
            {menu && items.length === 0 && !searching && (
              <div className="empty" style={{ gridColumn: '1 / -1' }}>No dishes here yet. Add them in <a href="#menu">Menu</a>.</div>
            )}
          </div>
        </section>

        <BillPanel
          bill={bill}
          now={now}
          flash={flash}
          onNewDineIn={() => setDialog({ kind: 'table', mode: 'new' })}
          onNewTakeaway={() => void newBill('Takeaway', null)}
          onEditLine={(line) => setDialog({ kind: 'line', line })}
          onQty={(line, qty) => void setQty(line, qty)}
          onDiscount={() => setDialog({ kind: 'discount' })}
          onPreview={() => bill && onPreview(bill)}
          onChangeTable={() => setDialog({ kind: 'table', mode: 'change' })}
          onCancel={() => setDialog({ kind: 'cancel' })}
          onPrint={() => void print()}
          onPay={pay}
        />
      </div>

      {dialog?.kind === 'variant' && (
        <VariantPicker
          item={dialog.item}
          qty={dialog.qty}
          onPick={(v) => {
            closeDialog()
            if (bill) void addLine(bill.id, v, dialog.qty)
          }}
          onClose={closeDialog}
        />
      )}
      {dialog?.kind === 'table' && (
        <TablePicker
          title={dialog.mode === 'change' ? 'Move to another table' : dialog.pending ? `New bill for ${dialog.pending.item.name}` : 'New dine-in bill'}
          subtitle={dialog.pending ? 'Is it takeaway or for a table? The dish is added right after.' : undefined}
          openBills={openBills}
          currentBillId={dialog.mode === 'change' ? bill?.id : undefined}
          onClose={closeDialog}
          onTakeaway={dialog.pending ? () => { closeDialog(); void newBill('Takeaway', null, dialog.pending) } : undefined}
          onOpenExisting={(id) => {
            closeDialog()
            const pending = dialog.pending
            void selectBill(id).then((b) => {
              if (!b || !pending) return
              if (b.isFinalised) toast('That table’s bill is already printed, so the dish was not added.', 'error')
              else addTo(b, pending)
            })
          }}
          onPick={(table) => {
            closeDialog()
            if (dialog.mode === 'new') void newBill('DineIn', table, dialog.pending)
            else if (bill) void change(() => api.put<Bill>(`/bills/${bill.id}/table`, { orderType: 'DineIn', tableLabel: table })).then((b) => b && toast(`Moved to ${billLabel(b)}.`))
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
            void change(() => api.put<Bill>(`/bills/${bill.id}/lines/${dialog.line.id}`, { qty, note })).then((b) => {
              if (b) setFlash((f) => ({ lineId: dialog.line.id, n: (f?.n ?? 0) + 1 }))
            })
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
