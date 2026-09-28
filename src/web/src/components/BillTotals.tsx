import { percent, rupees } from '../lib/money'
import type { Bill } from '../lib/types'

interface Props {
  bill: Bill
  onDiscount?: () => void
  /** Billing panel: one GST row per rate and no reason row, to leave room for the dishes. */
  compact?: boolean
}

/** Items total, discount, taxable value, CGST/SGST per rate, round off and grand total. */
export function BillTotals({ bill, onDiscount, compact }: Props) {
  const discountLabel =
    bill.discountKind === 'Percent' ? ` (${percent(bill.discountValue)}%)` : bill.discountKind === 'Amount' ? '' : ''
  return (
    <div className="totals">
      <div><span>Items total</span><span>{rupees(bill.subtotalPaise)}</span></div>
      <div>
        <span>
          Discount{discountLabel}
          {onDiscount && (
            <button className="link-btn" onClick={onDiscount}>{bill.discountKind === 'None' ? '+ Add' : 'Change'}</button>
          )}
        </span>
        <span>{bill.discountPaise ? `− ${rupees(bill.discountPaise)}` : '—'}</span>
      </div>
      {bill.discountReason && !compact && <div style={{ fontSize: 12.5 }}><span>Reason: {bill.discountReason}</span></div>}
      {bill.discountPaise > 0 && <div><span>Taxable value</span><span>{rupees(bill.taxablePaise)}</span></div>}
      {bill.taxMode === 'Regular' && compact
        ? bill.taxGroups
            .filter((g) => g.gstRateBp > 0)
            .map((g) => (
              <div key={g.gstRateBp} title={`CGST ${percent(g.halfRateBp)}% ${rupees(g.cgstPaise)} + SGST ${percent(g.halfRateBp)}% ${rupees(g.sgstPaise)}`}>
                <span>GST {percent(g.gstRateBp)}% (CGST + SGST {percent(g.halfRateBp)}% each)</span>
                <span>{rupees(g.cgstPaise + g.sgstPaise)}</span>
              </div>
            ))
        : bill.taxMode === 'Regular'
        ? bill.taxGroups
            .filter((g) => g.gstRateBp > 0)
            .flatMap((g) => [
              <div key={`c${g.gstRateBp}`}><span>CGST {percent(g.halfRateBp)}%</span><span>{rupees(g.cgstPaise)}</span></div>,
              <div key={`s${g.gstRateBp}`}><span>SGST {percent(g.halfRateBp)}%</span><span>{rupees(g.sgstPaise)}</span></div>,
            ])
        : <div><span>No GST (composition)</span><span /></div>}
      {bill.roundOffPaise !== 0 && (
        <div><span>Round off</span><span>{bill.roundOffPaise > 0 ? '+ ' : '− '}{rupees(Math.abs(bill.roundOffPaise))}</span></div>
      )}
      <div className="grand"><span>Total to pay</span><span>{rupees(bill.totalPaise)}</span></div>
    </div>
  )
}
