import { useState, type FormEvent } from 'react'
import { Field } from '../../components/Field'
import { Modal } from '../../components/Modal'
import { toast } from '../../components/toast'
import { api, ApiError, errorText } from '../../lib/api'
import { paiseToInput, parsePercent, parseRupees, percent } from '../../lib/money'
import type { Category, FoodType, MenuItem, MenuItemInput } from '../../lib/types'

interface Props {
  item: MenuItem | null // null = new dish
  categories: Category[]
  defaultCategoryId: number | null
  defaultGstRateBp: number
  onClose: () => void
  onSaved: (item: MenuItem) => void
}

interface SizeRow {
  id: number | null
  name: string
  price: string
}

const FOOD_TYPES: { value: FoodType; label: string }[] = [
  { value: 'Veg', label: 'Veg' },
  { value: 'NonVeg', label: 'Non-veg' },
  { value: 'Egg', label: 'Egg' },
]

/** Add or edit a dish: name, short code, category, veg/non-veg/egg, one price or several sizes, GST rate. */
export function ItemEditor({ item, categories, defaultCategoryId, defaultGstRateBp, onClose, onSaved }: Props) {
  const [name, setName] = useState(item?.name ?? '')
  const [shortCode, setShortCode] = useState(item?.shortCode ?? '')
  const [categoryId, setCategoryId] = useState<number | ''>(item?.categoryId ?? defaultCategoryId ?? categories[0]?.id ?? '')
  const [foodType, setFoodType] = useState<FoodType>(item?.foodType ?? 'Veg')
  const [description, setDescription] = useState(item?.description ?? '')
  const [isAvailable, setIsAvailable] = useState(item?.isAvailable ?? true)
  const [ownRate, setOwnRate] = useState(item?.gstRateBp != null)
  const [rate, setRate] = useState(item?.gstRateBp != null ? percent(item.gstRateBp) : percent(defaultGstRateBp))
  const [sizes, setSizes] = useState<SizeRow[]>(
    item?.variants.length
      ? item.variants.map((v) => ({ id: v.id, name: v.name === 'Regular' && item.variants.length === 1 ? '' : v.name, price: paiseToInput(v.pricePaise) }))
      : [{ id: null, name: '', price: '' }],
  )
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [saving, setSaving] = useState(false)

  const single = sizes.length === 1
  const setSize = (i: number, patch: Partial<SizeRow>) => setSizes((rows) => rows.map((r, j) => (j === i ? { ...r, ...patch } : r)))

  function switchToHalfFull() {
    // Keep the existing single price as the Full price.
    const first = sizes[0]
    setSizes([
      { id: null, name: 'Half', price: '' },
      { id: first?.id ?? null, name: 'Full', price: first?.price ?? '' },
    ])
  }

  async function save(e: FormEvent) {
    e.preventDefault()
    const local: Record<string, string[]> = {}
    const variants = sizes.map((s) => ({ id: s.id, name: s.name.trim() || null, pricePaise: parseRupees(s.price) }))
    if (variants.some((v) => v.pricePaise === null)) local.variants = ['Type each price in rupees, for example 180 or 180.50.']
    const gstRateBp = ownRate ? parsePercent(rate) : null
    if (ownRate && gstRateBp === null) local.gstRateBp = ['Type the GST rate as a number, for example 5.']
    if (categoryId === '') local.categoryId = ['Choose a category. Add one first with “Categories”.']
    if (Object.keys(local).length) {
      setErrors(local)
      return
    }

    const body: MenuItemInput = {
      categoryId: categoryId as number,
      name,
      shortCode: shortCode.trim() || null,
      description: description.trim() || null,
      foodType,
      gstRateBp,
      isAvailable,
      sortOrder: null,
      variants: variants.map((v) => ({ ...v, pricePaise: v.pricePaise! })),
    }
    setSaving(true)
    try {
      const saved = item ? await api.put<MenuItem>(`/menu/items/${item.id}`, body) : await api.post<MenuItem>('/menu/items', body)
      toast(item ? `${saved.name} saved.` : `${saved.name} added to the menu.`)
      onSaved(saved)
    } catch (err) {
      if (err instanceof ApiError) setErrors(err.fields)
      toast(errorText(err), 'error')
    } finally {
      setSaving(false)
    }
  }

  const activeCategories = categories.filter((c) => c.isActive || c.id === item?.categoryId)

  return (
    <Modal title={item ? `Edit ${item.name}` : 'Add dish'} onClose={onClose} wide>
      <form className="form" style={{ maxWidth: 'none' }} onSubmit={save}>
        <div className="form-grid">
          <Field label="Dish name" error={errors.name}>
            <input value={name} onChange={(e) => setName(e.target.value)} maxLength={100} autoFocus required />
          </Field>
          <Field label="Short code" hint="For quick search at the counter, like PBM" error={errors.shortCode}>
            <input value={shortCode} onChange={(e) => setShortCode(e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, ''))} maxLength={10} />
          </Field>
          <Field label="Category" error={errors.categoryId}>
            <select value={categoryId} onChange={(e) => setCategoryId(e.target.value ? Number(e.target.value) : '')}>
              {activeCategories.length === 0 && <option value="">(no categories yet)</option>}
              {activeCategories.map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
          </Field>
          <Field label="Type" error={errors.foodType}>
            <div className="seg">
              {FOOD_TYPES.map((t) => (
                <button key={t.value} type="button" className={foodType === t.value ? 'on' : ''} onClick={() => setFoodType(t.value)}>
                  {t.label}
                </button>
              ))}
            </div>
          </Field>
        </div>

        <Field label={single ? 'Price (₹)' : 'Sizes and prices (₹)'} error={errors.variants}>
          <div style={{ display: 'grid', gap: 8 }}>
            {sizes.map((s, i) => (
              <div key={i} className="row" style={{ flexWrap: 'nowrap' }}>
                {!single && (
                  <input className="input" placeholder="Size, e.g. Half" value={s.name} onChange={(e) => setSize(i, { name: e.target.value })} maxLength={30} style={{ flex: 1 }} />
                )}
                <input className="input" placeholder="Price" value={s.price} onChange={(e) => setSize(i, { price: e.target.value })} inputMode="decimal" style={{ flex: 1 }} />
                {!single && (
                  <button type="button" className="btn small" onClick={() => setSizes((rows) => rows.filter((_, j) => j !== i))} aria-label="Remove size">
                    Remove
                  </button>
                )}
              </div>
            ))}
            <div className="row">
              {single && <button type="button" className="btn small" onClick={switchToHalfFull}>Use Half / Full</button>}
              <button type="button" className="btn small" onClick={() => setSizes((rows) => [...rows, { id: null, name: '', price: '' }])}>
                + Add size
              </button>
            </div>
          </div>
        </Field>

        <div className="form-grid">
          <Field label="GST rate" error={errors.gstRateBp} hint={ownRate ? 'Only for this dish' : `Uses the restaurant rate (${percent(defaultGstRateBp)}%)`}>
            <div className="row" style={{ flexWrap: 'nowrap' }}>
              <label className="check">
                <input type="checkbox" checked={ownRate} onChange={(e) => setOwnRate(e.target.checked)} /> Own rate
              </label>
              {ownRate && <input className="input" value={rate} onChange={(e) => setRate(e.target.value)} inputMode="decimal" style={{ width: 90 }} aria-label="GST rate percent" />}
              {ownRate && '%'}
            </div>
          </Field>
          <Field label="Available today">
            <label className="check">
              <input type="checkbox" checked={isAvailable} onChange={(e) => setIsAvailable(e.target.checked)} /> Can be billed now
            </label>
          </Field>
        </div>

        <Field label="Short description (optional)" error={errors.description}>
          <input value={description} onChange={(e) => setDescription(e.target.value)} maxLength={300} />
        </Field>

        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? 'Saving…' : 'Save dish'}</button>
        </div>
      </form>
    </Modal>
  )
}
