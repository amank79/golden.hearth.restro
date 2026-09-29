import { useCallback, useEffect, useMemo, useState } from 'react'
import { FoodMark, Icon } from '../../components/Icon'
import { toast } from '../../components/toast'
import { api, errorText } from '../../lib/api'
import { plural } from '../../lib/bills'
import { searchItems } from '../../lib/menuSearch'
import { percent, rupeesShort } from '../../lib/money'
import type { Menu, MenuItem, Settings } from '../../lib/types'
import { CategoryManager } from './CategoryManager'
import { ItemEditor } from './ItemEditor'

/** Menu management: dishes, prices, sizes, "not available" switch, categories (MENU-1, 2, 3, 5, 6, 7, 8). */
export function MenuScreen({ settings }: { settings: Settings }) {
  const [menu, setMenu] = useState<Menu | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [cat, setCat] = useState<number | 'all'>('all')
  const [query, setQuery] = useState('')
  const [showRemoved, setShowRemoved] = useState(false)
  const [editing, setEditing] = useState<MenuItem | 'new' | null>(null)
  const [managingCats, setManagingCats] = useState(false)

  const load = useCallback(() => {
    api.get<Menu>('/menu?includeInactive=true').then(
      (m) => {
        setMenu(m)
        setError(null)
      },
      (e) => setError(errorText(e)),
    )
  }, [])

  useEffect(load, [load])

  const replaceItem = (item: MenuItem) =>
    setMenu((m) => m && { ...m, items: m.items.some((i) => i.id === item.id) ? m.items.map((i) => (i.id === item.id ? item : i)) : [...m.items, item] })

  async function toggle(item: MenuItem, what: 'availability' | 'active') {
    try {
      const updated =
        what === 'availability'
          ? await api.put<MenuItem>(`/menu/items/${item.id}/availability`, { isAvailable: !item.isAvailable })
          : await api.put<MenuItem>(`/menu/items/${item.id}/active`, { isActive: !item.isActive })
      replaceItem(updated)
      toast(
        what === 'availability'
          ? `${item.name} is now ${updated.isAvailable ? 'available' : 'NOT available'}.`
          : updated.isActive ? `${item.name} is back on the menu.` : `${item.name} removed from the menu. Old bills are not changed.`,
      )
    } catch (e) {
      toast(errorText(e), 'error')
    }
  }

  const categories = useMemo(() => menu?.categories ?? [], [menu])
  const catName = useMemo(() => new Map(categories.map((c) => [c.id, c.name])), [categories])
  const visible = useMemo(() => {
    if (!menu) return []
    const inactiveCats = new Set(categories.filter((c) => !c.isActive).map((c) => c.id))
    const list = menu.items.filter(
      (i) => (cat === 'all' || i.categoryId === cat) && (showRemoved || (i.isActive && !inactiveCats.has(i.categoryId))),
    )
    return searchItems(list, query)
  }, [menu, categories, cat, query, showRemoved])

  if (error) return <div className="error-box">{error} <button className="btn small" onClick={load}>Try again</button></div>
  if (!menu) return <div className="empty">Loading menu…</div>

  const shownCats = categories.filter((c) => c.isActive || showRemoved)
  const liveItems = menu.items.filter((i) => i.isActive)
  const notAvailable = liveItems.filter((i) => !i.isAvailable).length
  const countIn = (id: number) => menu.items.filter((i) => i.categoryId === id && (showRemoved || i.isActive)).length

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h1>Menu</h1>
          <p>
            {plural(liveItems.length, 'dish', 'dishes')}
            {notAvailable > 0 ? ` · ${notAvailable} not available today` : ' · all available today'}
          </p>
        </div>
        <div className="actions">
          <button className="btn" onClick={() => setManagingCats(true)}><Icon name="edit" />Categories</button>
          <button className="btn primary" onClick={() => setEditing('new')}><Icon name="plus" />Add dish</button>
        </div>
      </div>

      {categories.length === 0 ? (
        <div className="card empty">
          <b style={{ color: 'var(--ink)' }}>The menu is empty.</b><br />
          First add categories (Starters, Main Course, Breads…), then add dishes.<br /><br />
          <button className="btn primary" onClick={() => setManagingCats(true)}>Add categories</button>
        </div>
      ) : (
        <div className="menu-layout">
          <nav className="catlist" aria-label="Categories">
            <div className="catlist-head">Categories</div>
            <button className={`catrow${cat === 'all' ? ' on' : ''}`} onClick={() => setCat('all')}>
              All dishes<span className="n">{showRemoved ? menu.items.length : liveItems.length}</span>
            </button>
            {shownCats.map((c) => (
              <button
                key={c.id}
                className={`catrow${cat === c.id ? ' on' : ''}${c.isActive ? '' : ' off'}`}
                onClick={() => setCat(c.id)}
              >
                {c.name}<span className="n">{countIn(c.id)}</span>
              </button>
            ))}
          </nav>

        <div className="menu-main">
          <div className="menu-tools">
            <label className="search">
              <Icon name="search" />
              <input placeholder="Search by name or short code…" value={query} onChange={(e) => setQuery(e.target.value)} />
              {query && <button className="icon-btn" onClick={() => setQuery('')} aria-label="Clear search"><Icon name="close" /></button>}
            </label>
            <label className="check">
              <input type="checkbox" checked={showRemoved} onChange={(e) => setShowRemoved(e.target.checked)} /> Show removed dishes
            </label>
          </div>

            <div className="card">
              <table className="list-table">
                <thead>
                  <tr>
                    <th>Dish</th>
                    <th>Code</th>
                    {cat === 'all' && <th>Category</th>}
                    <th>Price</th>
                    <th>GST</th>
                    <th>Available today</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {visible.map((i) => (
                    <tr key={i.id} className={i.isActive ? '' : 'inactive'}>
                      <td>
                        <span className="nmcell"><FoodMark type={i.foodType} />{i.name}</span>
                        {i.description && <small className="muted" style={{ display: 'block', marginLeft: 24 }}>{i.description}</small>}
                        {!i.isActive && <span className="pill red" style={{ marginTop: 4, marginLeft: 24 }}>Removed</span>}
                      </td>
                      <td>{i.shortCode && <span className="code">{i.shortCode}</span>}</td>
                      {cat === 'all' && <td className="muted">{catName.get(i.categoryId)}</td>}
                      <td style={{ fontVariantNumeric: 'tabular-nums' }}>
                        {i.variants.length === 1
                          ? <b>{rupeesShort(i.variants[0].pricePaise)}</b>
                          : <span className="price-sizes">{i.variants.map((v) => <span key={v.id}>{v.name} <b>{rupeesShort(v.pricePaise)}</b></span>)}</span>}
                      </td>
                      <td className="muted">{percent(i.gstRateBp ?? settings.gstRateBp)}%{i.gstRateBp != null && ' *'}</td>
                      <td style={{ whiteSpace: 'nowrap' }}>
                        <button className={`switch${i.isAvailable ? ' on' : ''}`} onClick={() => toggle(i, 'availability')} aria-label={`${i.name} available`} disabled={!i.isActive} />
                        <span className="sw-label" style={{ color: i.isAvailable ? 'var(--green)' : 'var(--red)' }}>{i.isAvailable ? 'Yes' : 'No'}</span>
                      </td>
                      <td className="num">
                        <div className="row" style={{ flexWrap: 'nowrap', justifyContent: 'flex-end' }}>
                          <button className="btn small" onClick={() => setEditing(i)}><Icon name="edit" />Edit</button>
                          <button className={`btn small${i.isActive ? ' danger' : ''}`} onClick={() => toggle(i, 'active')}>
                            {i.isActive ? 'Remove' : 'Bring back'}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {visible.length === 0 && (
                    <tr><td colSpan={7} className="empty">No dish found.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
            <p className="foot-note">
              Price changes apply to new orders only; old bills keep their prices. * = dish has its own GST rate.
            </p>
        </div>
        </div>
      )}

      {editing && (
        <ItemEditor
          item={editing === 'new' ? null : editing}
          categories={categories}
          defaultCategoryId={cat === 'all' ? null : cat}
          defaultGstRateBp={settings.gstRateBp}
          onClose={() => setEditing(null)}
          onSaved={(item) => {
            replaceItem(item)
            setEditing(null)
          }}
        />
      )}
      {managingCats && <CategoryManager categories={categories} onClose={() => setManagingCats(false)} onChanged={load} />}
    </div>
  )
}
