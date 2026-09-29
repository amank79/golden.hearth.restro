import { useEffect, useRef } from 'react'
import { BillTotals } from '../../components/BillTotals'
import { Icon } from '../../components/Icon'
import { billLabel, minutesAgo, plural } from '../../lib/bills'
import { rupees, rupeesShort } from '../../lib/money'
import type { Bill, BillLine, PaymentMethod } from '../../lib/types'

interface Props {
  bill: Bill | null
  now: number
  /** Line to highlight after it was added or changed; `n` changes so the same line can flash again. */
  flash: { lineId: number; n: number } | null
  onNewDineIn: () => void
  onNewTakeaway: () => void
  onEditLine: (line: BillLine) => void
  onQty: (line: BillLine, qty: number) => void
  onDiscount: () => void
  onPreview: () => void
  onChangeTable: () => void
  onCancel: () => void
  onPrint: () => void
  onPay: (method: PaymentMethod) => void
}

/**
 * The current bill drawn as a paper receipt: title and tools, the dishes, the tax lines, then a torn edge,
 * the navy total band and the Print / Cash / UPI / Card buttons. The band and buttons never scroll away.
 */
export function BillPanel({ bill, now, flash, onNewDineIn, onNewTakeaway, onEditLine, onQty, onDiscount, onPreview, onChangeTable, onCancel, onPrint, onPay }: Props) {
  const linesRef = useRef<HTMLDivElement>(null)

  // Keep the dish that was just added in view.
  useEffect(() => {
    if (flash) linesRef.current?.querySelector(`[data-line="${flash.lineId}"]`)?.scrollIntoView({ block: 'nearest' })
  }, [flash])

  if (!bill) {
    return (
      <section className="receipt-col" aria-label="Current bill">
        <div className="paper">
          <div className="bill-start">
            <Icon name="receipt" />
            <h3>No bill open</h3>
            <div>Start a dine-in or takeaway bill.</div>
            <div className="btns">
              <button className="btn dark big" onClick={onNewDineIn}><Icon name="table" />Dine-in <kbd>F3</kbd></button>
              <button className="btn dark big" onClick={onNewTakeaway}><Icon name="bag" />Takeaway <kbd>F4</kbd></button>
            </div>
            <div className="hint">Or just tap a dish: you will be asked for the table.</div>
          </div>
        </div>
        <div className="paper-foot" />
      </section>
    )
  }

  const editable = !bill.isFinalised
  const empty = bill.lines.length === 0
  const itemCount = bill.lines.reduce((n, l) => n + l.qty, 0)
  const hasDiscount = bill.discountKind !== 'None'

  return (
    <section className="receipt-col" aria-label="Current bill">
      <div className="paper">
        <header className="r-head split">
          <div className="r-title">{billLabel(bill)}</div>
          <div className="r-kicker">
            {bill.orderType === 'DineIn' ? 'Dine-in' : 'Takeaway'}
            <small>started {minutesAgo(bill.openedAt, now)}</small>
          </div>
        </header>
        {bill.billNo && (
          <div className="r-sub"><span className="pill blue">Printed · waiting for payment</span> Bill {bill.billNo}</div>
        )}
        <div className="r-tools">
          {editable && !empty && (
            <button className={`rtool${hasDiscount ? ' on' : ''}`} onClick={onDiscount} title="Discount with a reason">
              <Icon name="percent" />{hasDiscount ? 'Discount ✓' : 'Discount'}
            </button>
          )}
          {editable && bill.orderType === 'DineIn' && (
            <button className="rtool" onClick={onChangeTable} title="Move this bill to another table"><Icon name="swap" />Table</button>
          )}
          {!empty && <button className="rtool" onClick={onPreview} title="See the bill as it will print"><Icon name="eye" />View</button>}
          <button className="rtool danger" onClick={onCancel} title="Cancel this bill (it stays in history)"><Icon name="cancel" />Cancel</button>
        </div>

        {bill.isFinalised && (
          <div className="bill-banner">Printed bills cannot be changed. To change it, use Cancel and copy the dishes to a new bill.</div>
        )}

        <div className="r-lines" ref={linesRef}>
          {bill.lines.map((l) => {
            const flashing = flash?.lineId === l.id
            return (
              <div className={`line${flashing ? ' flash' : ''}`} key={flashing ? `${l.id}-${flash.n}` : l.id} data-line={l.id}>
                <button className="line-main" onClick={() => onEditLine(l)} disabled={!editable} title={editable ? 'Change quantity or add a note' : undefined}>
                  <span className="line-name">{l.itemName}</span>
                  <span className="line-meta">
                    {l.variantName !== 'Regular' && <em>{l.variantName}</em>}
                    {rupeesShort(l.unitPricePaise)} each
                    {l.note && <> · <span className="line-note">{l.note}</span></>}
                  </span>
                </button>
                <div className="stepper">
                  <button onClick={() => onQty(l, l.qty - 1)} disabled={!editable} aria-label={`One less ${l.itemName}`}><Icon name="minus" /></button>
                  <span>{l.qty}</span>
                  <button onClick={() => onQty(l, l.qty + 1)} disabled={!editable} aria-label={`One more ${l.itemName}`}><Icon name="plus" /></button>
                </div>
                <div className="line-amt">{rupees(l.amountPaise)}</div>
              </div>
            )
          })}
          {empty && (
            <div className="bill-empty-lines">
              Tap dishes on the left,<br />or type a name or code and press <kbd>Enter</kbd>.
            </div>
          )}
        </div>

        {!empty && <BillTotals bill={bill} compact hideGrand />}
      </div>

      <div className="tear" aria-hidden="true" />
      <div className={`r-total${empty ? ' idle' : ''}`}>
        <span>Total to pay<small>{plural(itemCount, 'item')}</small></span>
        <b>{rupees(bill.totalPaise)}</b>
      </div>
      <div className="r-actions">
        <button className="act print" onClick={onPrint} disabled={empty}>
          <Icon name="print" />{bill.isFinalised ? 'Reprint' : 'Print'} <kbd>F9</kbd>
        </button>
        <button className="act" onClick={() => onPay('Cash')} disabled={empty}><Icon name="cash" />Cash <kbd>F8</kbd></button>
        <button className="act" onClick={() => onPay('Upi')} disabled={empty}><Icon name="upi" />UPI</button>
        <button className="act" onClick={() => onPay('Card')} disabled={empty}><Icon name="card" />Card</button>
      </div>
    </section>
  )
}
