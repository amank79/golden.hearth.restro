import { useState } from 'react'
import { Modal } from '../../components/Modal'
import { toast } from '../../components/toast'
import { api, errorText } from '../../lib/api'
import type { Category } from '../../lib/types'

interface Props {
  categories: Category[]
  onClose: () => void
  onChanged: () => void
}

/** Add, rename, reorder and remove categories (MENU-1). Removing only hides a category; nothing is deleted. */
export function CategoryManager({ categories, onClose, onChanged }: Props) {
  const [newName, setNewName] = useState('')
  const [editing, setEditing] = useState<{ id: number; name: string } | null>(null)
  const [busy, setBusy] = useState(false)

  const active = categories.filter((c) => c.isActive)
  const removed = categories.filter((c) => !c.isActive)

  async function run(action: () => Promise<unknown>, done?: string) {
    setBusy(true)
    try {
      await action()
      if (done) toast(done)
      onChanged()
    } catch (e) {
      toast(errorText(e), 'error')
    } finally {
      setBusy(false)
    }
  }

  const add = () =>
    newName.trim() &&
    run(async () => {
      await api.post('/menu/categories', { name: newName, sortOrder: null })
      setNewName('')
    }, `${newName.trim()} added.`)

  const rename = () =>
    editing &&
    run(async () => {
      const c = categories.find((x) => x.id === editing.id)!
      await api.put(`/menu/categories/${c.id}`, { name: editing.name, sortOrder: c.sortOrder })
      setEditing(null)
    })

  // Swap display order with the neighbour. Sort orders are renumbered 1..n so they stay distinct.
  const move = (index: number, dir: -1 | 1) =>
    run(async () => {
      const order = [...active]
      const [c] = order.splice(index, 1)
      order.splice(index + dir, 0, c)
      for (let i = 0; i < order.length; i++) {
        if (order[i].sortOrder !== i + 1) await api.put(`/menu/categories/${order[i].id}`, { name: order[i].name, sortOrder: i + 1 })
      }
    })

  const setActive = (c: Category, isActive: boolean) =>
    run(() => api.put(`/menu/categories/${c.id}/active`, { isActive }), isActive ? `${c.name} is back.` : `${c.name} removed. Its dishes are hidden from billing.`)

  return (
    <Modal title="Categories" subtitle="The order here is the order on the billing screen." onClose={onClose} wide>
      <div className="card" style={{ overflow: 'hidden' }}>
        <table className="list-table">
          <tbody>
            {active.map((c, i) => (
              <tr key={c.id}>
                <td style={{ width: '100%' }}>
                  {editing?.id === c.id ? (
                    <form className="row" style={{ flexWrap: 'nowrap' }} onSubmit={(e) => { e.preventDefault(); void rename() }}>
                      <input className="input" value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} maxLength={60} autoFocus />
                      <button className="btn small primary" disabled={busy}>Save</button>
                      <button type="button" className="btn small" onClick={() => setEditing(null)}>Cancel</button>
                    </form>
                  ) : (
                    <b>{c.name}</b>
                  )}
                </td>
                <td className="num">
                  <div className="row" style={{ flexWrap: 'nowrap' }}>
                    <button className="btn small" disabled={busy || i === 0} onClick={() => move(i, -1)} aria-label="Move up">▲</button>
                    <button className="btn small" disabled={busy || i === active.length - 1} onClick={() => move(i, 1)} aria-label="Move down">▼</button>
                    <button className="btn small" disabled={busy} onClick={() => setEditing({ id: c.id, name: c.name })}>Rename</button>
                    <button className="btn small danger" disabled={busy} onClick={() => setActive(c, false)}>Remove</button>
                  </div>
                </td>
              </tr>
            ))}
            {active.length === 0 && (
              <tr><td className="empty">No categories yet. Add the first one below.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      <form className="row" style={{ marginTop: 12, flexWrap: 'nowrap' }} onSubmit={(e) => { e.preventDefault(); void add() }}>
        <input className="input" placeholder="New category, e.g. Soups" value={newName} onChange={(e) => setNewName(e.target.value)} maxLength={60} />
        <button className="btn primary" disabled={busy || !newName.trim()}>Add</button>
      </form>

      {removed.length > 0 && (
        <>
          <div className="section-title">Removed categories</div>
          <div className="chipbar">
            {removed.map((c) => (
              <button key={c.id} className="chip" disabled={busy} onClick={() => setActive(c, true)} title="Bring back">
                {c.name} · Bring back
              </button>
            ))}
          </div>
        </>
      )}

      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Done</button>
      </div>
    </Modal>
  )
}
