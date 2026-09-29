import { useState, type FormEvent } from 'react'
import { Field } from '../components/Field'
import { toast } from '../components/toast'
import { api, ApiError, errorText } from '../lib/api'
import { parsePercent, percent } from '../lib/money'
import type { Settings } from '../lib/types'

interface Props {
  settings: Settings
  onSaved: (s: Settings) => void
}

/** Restaurant details and tax settings printed on every bill (SET-1, SET-2). */
export function SettingsScreen({ settings, onSaved }: Props) {
  const [form, setForm] = useState({ ...settings, gstRate: percent(settings.gstRateBp) })
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [saving, setSaving] = useState(false)

  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => setForm((f) => ({ ...f, [key]: value }))

  async function save(e: FormEvent) {
    e.preventDefault()
    const gstRateBp = parsePercent(form.gstRate)
    if (gstRateBp === null) {
      setErrors({ gstRateBp: ['Type the GST rate as a number, for example 5.'] })
      return
    }
    const { gstRate: _ignored, ...rest } = form
    void _ignored
    setSaving(true)
    try {
      const saved = await api.put<Settings>('/settings', { ...rest, gstRateBp })
      setErrors({})
      setForm({ ...saved, gstRate: percent(saved.gstRateBp) })
      onSaved(saved)
      toast('Settings saved.')
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fields).length) setErrors(err.fields)
      toast(errorText(err), 'error')
    } finally {
      setSaving(false)
    }
  }

  const composition = form.taxMode === 'Composition'
  const halfRate = parsePercent(form.gstRate)

  return (
    <form className="form" onSubmit={save}>
      <div className="card" style={{ padding: 18 }}>
        <div className="section-title" style={{ marginTop: 0 }}>Restaurant details (printed on every bill)</div>
        <div className="form" style={{ maxWidth: 'none' }}>
          <Field label="Restaurant name" error={errors.name}>
            <input value={form.name} onChange={(e) => set('name', e.target.value)} maxLength={60} required />
          </Field>
          <Field label="Address" error={errors.address}>
            <textarea value={form.address} onChange={(e) => set('address', e.target.value)} maxLength={200} rows={2} />
          </Field>
          <div className="form-grid">
            <Field label="Phone" error={errors.phone}>
              <input value={form.phone} onChange={(e) => set('phone', e.target.value)} maxLength={40} inputMode="tel" />
            </Field>
            <Field label="FSSAI licence no." hint="14 digits" error={errors.fssaiNo}>
              <input value={form.fssaiNo} onChange={(e) => set('fssaiNo', e.target.value)} maxLength={14} inputMode="numeric" />
            </Field>
          </div>
          <Field label="GSTIN" hint="15 characters, for example 24ABCDE1234F1Z5" error={errors.gstin}>
            <input value={form.gstin} onChange={(e) => set('gstin', e.target.value.toUpperCase())} maxLength={15} />
          </Field>
          <Field label="Bill footer" hint="Printed at the bottom of every bill" error={errors.billFooter}>
            <textarea value={form.billFooter} onChange={(e) => set('billFooter', e.target.value)} maxLength={300} rows={2} />
          </Field>
        </div>
      </div>

      <div className="card" style={{ padding: 18 }}>
        <div className="section-title" style={{ marginTop: 0 }}>Tax</div>
        <div className="form" style={{ maxWidth: 'none' }}>
          <Field label="Tax mode" error={errors.taxMode}>
            <div className="seg" role="radiogroup">
              <button type="button" className={composition ? '' : 'on'} onClick={() => set('taxMode', 'Regular')}>
                Regular GST (Tax Invoice)
              </button>
              <button type="button" className={composition ? 'on' : ''} onClick={() => set('taxMode', 'Composition')}>
                Composition (Bill of Supply)
              </button>
            </div>
          </Field>
          {composition ? (
            <p className="note">
              Composition scheme: no GST is added to bills. Bills are titled <b>Bill of Supply</b> and carry the line
              “Composition taxable person, not eligible to collect tax on supplies”.
            </p>
          ) : (
            <Field
              label="GST rate (%)"
              hint={halfRate !== null ? `Printed as CGST ${percent(halfRate / 2)}% + SGST ${percent(halfRate / 2)}%. Dishes can have their own rate in Menu.` : 'For example 5'}
              error={errors.gstRateBp}
            >
              <input value={form.gstRate} onChange={(e) => set('gstRate', e.target.value)} inputMode="decimal" style={{ maxWidth: 160 }} />
            </Field>
          )}
          {!form.gstin && <p className="warn">GSTIN is empty. Please confirm the tax details with the CA before the first real bill.</p>}
        </div>
      </div>

      <div className="form-actions">
        <button className="btn primary big" disabled={saving}>{saving ? 'Saving…' : 'Save settings'}</button>
      </div>
    </form>
  )
}
