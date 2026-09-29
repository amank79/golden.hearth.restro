import { useEffect, useState, type FormEvent } from 'react'
import { Field } from '../components/Field'
import { Icon } from '../components/Icon'
import { toast } from '../components/toast'
import { api, ApiError, errorText } from '../lib/api'
import { dateTime } from '../lib/bills'
import { parsePercent, percent } from '../lib/money'
import { applyTheme, savedTheme, THEMES, type Theme } from '../lib/theme'
import type { BackupResult, BackupStatus, Settings } from '../lib/types'

interface Props {
  settings: Settings
  onSaved: (s: Settings) => void
  backup: BackupStatus | null
}

/** Backup state and a "Back up now" button. Backups also run by themselves every day and when the app closes. */
function BackupPanel({ backup }: { backup: BackupStatus | null }) {
  const [running, setRunning] = useState(false)

  const runNow = async () => {
    setRunning(true)
    try {
      const r = await api.post<BackupResult>('/backup/run')
      toast(`Backup saved: ${r.fileName}.`)
    } catch (e) {
      toast(`Backup failed: ${errorText(e)}`, 'error')
    } finally {
      setRunning(false)
      window.dispatchEvent(new Event('pos:backup-changed'))
    }
  }

  const failed = backup?.lastResult && !backup.lastResult.success
  return (
    <div className="card panel">
      <h2><Icon name="shield" />Backup</h2>
      <p className="muted">A safe copy of all bills and the menu is saved every day and when the program closes.</p>
      {backup && (
        <div className="kv" style={{ marginBottom: 14 }}>
          <span>Last backup</span>
          <div>
            {backup.newestBackupAt ? dateTime(backup.newestBackupAt) : 'none yet'}{' '}
            {failed ? <span className="pill red">Last try failed</span> : backup.overdue || !backup.newestBackupAt ? <span className="pill amber">Overdue</span> : <span className="pill green">OK</span>}
          </div>
          <span>Saved in</span>
          <code>{backup.folder}</code>
          {backup.extraFolders.map((f) => (
            <div key={f.path} style={{ display: 'contents' }}>
              <span>Extra copy</span>
              <div><code>{f.path}</code> {f.connected ? <span className="pill green">Connected</span> : <span className="pill amber">Not plugged in</span>}</div>
            </div>
          ))}
          {failed && (
            <>
              <span>Problem</span>
              <div style={{ color: 'var(--red)' }}>{backup.lastResult!.messages.join(' ')}</div>
            </>
          )}
        </div>
      )}
      <button type="button" className="btn dark" onClick={() => void runNow()} disabled={running}>
        <Icon name="shield" />{running ? 'Backing up…' : 'Back up now'}
      </button>
    </div>
  )
}

/** Colour palette for this laptop. Saved straight away; not part of the restaurant settings. */
function AppearancePanel() {
  const [theme, setTheme] = useState<Theme>(savedTheme)
  const choose = (t: Theme) => {
    applyTheme(t)
    setTheme(t)
  }
  return (
    <div className="card panel">
      <h2><Icon name="eye" />Appearance</h2>
      <p className="muted">Pick the colours for this laptop. Changes straight away.</p>
      <div className="theme-opts" role="radiogroup" aria-label="Colours">
        {THEMES.map((t) => (
          <button key={t.id} type="button" role="radio" aria-checked={theme === t.id} className={`theme-opt${theme === t.id ? ' on' : ''}`} onClick={() => choose(t.id)}>
            <span className={`theme-swatch ${t.id}`} />
            <b>{t.name}</b>
            <small>{t.note}</small>
          </button>
        ))}
      </div>
    </div>
  )
}

/** Restaurant details and tax settings printed on every bill (SET-1, SET-2), plus backup and version. */
export function SettingsScreen({ settings, onSaved, backup }: Props) {
  const [form, setForm] = useState({ ...settings, gstRate: percent(settings.gstRateBp) })
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [saving, setSaving] = useState(false)
  const [version, setVersion] = useState<string | null>(null)

  useEffect(() => {
    api.get<{ version: string }>('/health').then((h) => setVersion(h.version.split('+')[0]), () => {})
  }, [])

  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => setForm((f) => ({ ...f, [key]: value }))
  const dirty =
    form.name !== settings.name || form.address !== settings.address || form.phone !== settings.phone || form.gstin !== settings.gstin ||
    form.fssaiNo !== settings.fssaiNo || form.taxMode !== settings.taxMode || form.billFooter !== settings.billFooter ||
    form.gstRate !== percent(settings.gstRateBp)

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
    <form onSubmit={save}>
      <div className="page-head">
        <div>
          <h1>Settings</h1>
          <p>Restaurant details and tax are printed on every bill.</p>
        </div>
      </div>

      <div className="settings">
        <div className="card panel">
          <h2><Icon name="receipt" />Restaurant details</h2>
          <p className="muted">Printed at the top and bottom of every bill.</p>
          <div className="form">
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
            <Field label="Bill footer" hint="Printed at the bottom of every bill" error={errors.billFooter}>
              <textarea value={form.billFooter} onChange={(e) => set('billFooter', e.target.value)} maxLength={300} rows={2} />
            </Field>
          </div>
        </div>

        <div className="settings-col">
          <div className="card panel">
            <h2><Icon name="bills" />GST</h2>
            <p className="muted">Confirm these with the CA before the first real bill.</p>
            <div className="form">
              <Field label="Tax mode" error={errors.taxMode}>
                <div className="seg" role="radiogroup">
                  <button type="button" className={composition ? '' : 'on'} onClick={() => set('taxMode', 'Regular')} aria-pressed={!composition}>
                    Regular GST
                  </button>
                  <button type="button" className={composition ? 'on' : ''} onClick={() => set('taxMode', 'Composition')} aria-pressed={composition}>
                    Composition
                  </button>
                </div>
              </Field>
              <Field label="GSTIN" hint="15 characters, for example 24ABCDE1234F1Z5" error={errors.gstin}>
                <input value={form.gstin} onChange={(e) => set('gstin', e.target.value.toUpperCase())} maxLength={15} />
              </Field>
              {composition ? (
                <p className="note" style={{ margin: 0 }}>
                  No GST is added to bills. Bills are titled <b>Bill of Supply</b> and carry the line
                  “Composition taxable person, not eligible to collect tax on supplies”.
                </p>
              ) : (
                <Field
                  label="GST rate (%)"
                  hint={halfRate !== null ? `Printed as CGST ${percent(halfRate / 2)}% + SGST ${percent(halfRate / 2)}%. A dish can have its own rate in Menu.` : 'For example 5'}
                  error={errors.gstRateBp}
                >
                  <input value={form.gstRate} onChange={(e) => set('gstRate', e.target.value)} inputMode="decimal" style={{ maxWidth: 160 }} />
                </Field>
              )}
              {!composition && !form.gstin.trim() && (
                <p className="warn" style={{ margin: 0 }}>GSTIN is empty, but bills are printed as <b>Tax Invoice</b>. Add the GSTIN, or switch to Composition if that is how the restaurant is registered.</p>
              )}
            </div>
          </div>

          <BackupPanel backup={backup} />
          <AppearancePanel />

          <div className="card panel">
            <h2><Icon name="settings" />About</h2>
            <div className="kv">
              <span>Version</span><div>{version ?? '…'}</div>
              <span>Help</span><div>Press <kbd>F1</kbd> for keyboard shortcuts.</div>
            </div>
          </div>
        </div>
      </div>

      <div className="save-bar">
        <button className="btn primary big" disabled={saving || !dirty}>{saving ? 'Saving…' : 'Save settings'}</button>
        <span className="muted">{dirty ? 'You have changes that are not saved yet.' : 'All changes saved.'}</span>
      </div>
    </form>
  )
}
