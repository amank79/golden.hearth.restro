import { FoodMark, Icon } from '../../components/Icon'
import { rupeesShort } from '../../lib/money'
import type { MenuItem, Variant } from '../../lib/types'

interface Props {
  item: MenuItem
  /** How many of this dish are on the current bill (shown as a badge). */
  inBill: number
  highlighted: boolean
  /** Add the dish; without a variant the size is asked for. */
  onAdd: (item: MenuItem, variant?: Variant) => void
}

/**
 * One dish on the billing grid. A single-price dish is one big button with the price and a + along the bottom.
 * A dish with two sizes has two big buttons along the bottom (HALF ₹160 | FULL ₹260), so the right size is one tap;
 * tapping the name asks for the size. Three or more sizes open the size picker.
 */
export function ItemTile({ item, inBill, highlighted, onAdd }: Props) {
  const cls = `tile${item.isAvailable ? '' : ' na'}${highlighted ? ' hl' : ''}`
  const badge = inBill > 0 ? <span className="qbadge" aria-label={`${inBill} on this bill`}>{inBill}</span> : null
  const head = (
    <>
      <span className="tile-top">
        <FoodMark type={item.foodType} />
        {item.shortCode && <span className="tile-code">{item.shortCode}</span>}
      </span>
      <span className="tile-name">{item.name}</span>
    </>
  )

  if (!item.isAvailable || item.variants.length !== 2) {
    const prices = item.variants.map((v) => v.pricePaise)
    const price = !item.isAvailable
      ? 'Not available today'
      : prices.length === 1
        ? rupeesShort(prices[0])
        : `${item.variants.length} sizes · ${rupeesShort(Math.min(...prices))}+`
    return (
      <button className={cls} onClick={() => onAdd(item, item.variants.length === 1 ? item.variants[0] : undefined)}>
        {badge}
        <span className="tile-main">{head}</span>
        <span className="tile-foot">
          <span className="tile-price">{price}</span>
          {item.isAvailable && <span className="tile-plus" aria-hidden="true"><Icon name="plus" /></span>}
        </span>
      </button>
    )
  }

  return (
    <div className={cls}>
      {badge}
      <button className="tile-main" onClick={() => onAdd(item)} aria-label={`${item.name}, choose size`}>
        {head}
      </button>
      <div className="tile-vars">
        {item.variants.map((v) => (
          <button key={v.id} className="tile-var" onClick={() => onAdd(item, v)} aria-label={`${item.name} ${v.name} ${rupeesShort(v.pricePaise)}`}>
            <span>{v.name}</span>
            <b>{rupeesShort(v.pricePaise)}</b>
          </button>
        ))}
      </div>
    </div>
  )
}
