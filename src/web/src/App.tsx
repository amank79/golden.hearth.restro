import { useCallback, useEffect, useState } from 'react'
import { Icon, type IconName } from './components/Icon'
import { PrintPreview } from './components/PrintPreview'
import { ShortcutsHelp } from './components/ShortcutsHelp'
import { Toaster } from './components/Toaster'
import { toast } from './components/toast'
import { api, errorText } from './lib/api'
import { brandParts } from './lib/brand'
import { dateTime, day, indiaToday, plural, time } from './lib/bills'
import { rupees } from './lib/money'
import type { BackupStatus, Bill, BillSummary, PrintOutput, Settings, TodaySummary } from './lib/types'
import { BillingScreen } from './screens/billing/BillingScreen'
import { BillsScreen } from './screens/history/BillsScreen'
import { MenuScreen } from './screens/menu/MenuScreen'
import { SettingsScreen } from './screens/SettingsScreen'

type ScreenId = 'billing' | 'bills' | 'menu' | 'settings'

const SCREENS: { id: ScreenId; label: string; icon: IconName }[] = [
  { id: 'billing', label: 'Billing', icon: 'billing' },
  { id: 'bills', label: 'Bill history', icon: 'bills' },
  { id: 'menu', label: 'Menu', icon: 'menu' },
  { id: 'settings', label: 'Settings', icon: 'settings' },
]

function currentScreen(): ScreenId {
  const id = window.location.hash.slice(1).split('/')[0]
  return (SCREENS.find((s) => s.id === id)?.id ?? SCREENS[0].id) as ScreenId
}

function useClock() {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    const t = window.setInterval(() => setNow(new Date()), 15000)
    return () => window.clearInterval(t)
  }, [])
  return { time: time(now.toISOString()), day: day(now.toISOString()) }
}


/** Backup state for the status bar: green when recent, amber when overdue, red when the last one failed. */
function BackupPill({ status }: { status: BackupStatus | null }) {
  if (!status) return null
  if (status.lastResult && !status.lastResult.success)
    return <a className="status bad" href="#settings" title={status.lastResult.messages.join('\n')}><Icon name="alert" /><span>Backup failed</span></a>
  if (!status.newestBackupAt || status.overdue)
    return <a className="status warn" href="#settings" title="Open Settings to back up now"><Icon name="alert" /><span>{status.newestBackupAt ? 'Backup overdue' : 'No backup yet'}</span></a>
  const at = status.newestBackupAt
  const label = at.slice(0, 10) === indiaToday() ? time(at) : dateTime(at)
  return <a className="status ok" href="#settings" title={`Last backup ${dateTime(at)}`}><Icon name="shield" /><span>Backed up {label}</span></a>
}

export default function App() {
  const [screen, setScreen] = useState<ScreenId>(currentScreen)
  const [settings, setSettings] = useState<Settings | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [connected, setConnected] = useState(true)
  const [today, setToday] = useState<TodaySummary | null>(null)
  const [openCount, setOpenCount] = useState(0)
  const [backup, setBackup] = useState<BackupStatus | null>(null)
  const [help, setHelp] = useState(false)
  const clock = useClock()

  const loadToday = useCallback(() => {
    api.get<TodaySummary>('/bills/today').then(
      (t) => {
        setToday(t)
        setConnected(true)
      },
      () => setConnected(false),
    )
    api.get<BillSummary[]>('/bills/open').then((b) => setOpenCount(b.length), () => {})
  }, [])

  const loadBackup = useCallback(() => {
    api.get<BackupStatus>('/backup/status').then(setBackup, () => {})
  }, [])

  useEffect(() => {
    loadToday()
    loadBackup()
    const t = window.setInterval(loadToday, 30000)
    const b = window.setInterval(loadBackup, 5 * 60000)
    window.addEventListener('pos:bills-changed', loadToday)
    window.addEventListener('pos:backup-changed', loadBackup)
    return () => {
      window.clearInterval(t)
      window.clearInterval(b)
      window.removeEventListener('pos:bills-changed', loadToday)
      window.removeEventListener('pos:backup-changed', loadBackup)
    }
  }, [loadToday, loadBackup])

  const [preview, setPreview] = useState<{ output: PrintOutput; printed: boolean } | null>(null)

  // Print a bill: the first print gives it its number; later prints are marked DUPLICATE.
  const printBill = useCallback(async (bill: Bill): Promise<Bill | null> => {
    try {
      const output = await api.post<PrintOutput>(`/bills/${bill.id}/print`)
      setPreview({ output, printed: true })
      loadToday()
      return output.bill
    } catch (e) {
      toast(errorText(e), 'error')
      return null
    }
  }, [loadToday])

  // Show what the bill will look like, without printing or numbering it.
  const previewBill = useCallback(async (bill: Bill) => {
    try {
      setPreview({ output: await api.get<PrintOutput>(`/bills/${bill.id}/print-preview`), printed: false })
    } catch (e) {
      toast(errorText(e), 'error')
    }
  }, [])

  const loadSettings = useCallback(() => {
    api.get<Settings>('/settings').then(setSettings, (e) => setLoadError(errorText(e)))
  }, [])

  useEffect(() => {
    loadSettings()
    const onHash = () => setScreen(currentScreen())
    window.addEventListener('hashchange', onHash)
    return () => window.removeEventListener('hashchange', onHash)
  }, [loadSettings])

  // F1 (or ? outside a text box) shows the keyboard shortcuts on every screen.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const inField = e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement
      if (e.key === 'F1' || (e.key === '?' && !inField && !document.querySelector('.overlay'))) {
        e.preventDefault()
        setHelp(true)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const gstinMissing = settings !== null && settings.taxMode === 'Regular' && !settings.gstin.trim()

  const brand = brandParts(settings?.name)

  return (
    <div className="app">
      <header className="topnav">
        <a className="brand" href="#billing" title={settings?.name}>
          <span className="brand-mark"><Icon name="hearth" /></span>
          <span className="brand-text">
            <b>{brand.main}</b>
            {brand.sub && <small>{brand.sub}</small>}
          </span>
        </a>
        <nav className="tabs" aria-label="Screens">
          {SCREENS.map((s) => (
            <a key={s.id} className={`tab${s.id === screen ? ' active' : ''}`} href={`#${s.id}`} aria-current={s.id === screen ? 'page' : undefined}>
              <Icon name={s.icon} />
              {s.label}
              {s.id === 'billing' && openCount > 0 && <span className="tab-badge" title={plural(openCount, 'open bill')}>{openCount}</span>}
            </a>
          ))}
        </nav>
        <div className="topnav-right">
          {!connected && (
            <span className="status bad" title="The billing program is not answering. Restart the laptop or open the Restaurant POS shortcut again.">
              <Icon name="alert" /><span>Not connected</span>
            </span>
          )}
          {gstinMissing && (
            <a className="status warn" href="#settings" title="Bills are printed as Tax Invoice without a GSTIN. Add it in Settings.">
              <Icon name="alert" /><span>GSTIN missing</span>
            </a>
          )}
          <BackupPill status={backup} />
          {today && (
            <a className="today" href="#bills" title="Open bill history">
              <b>{rupees(today.totalPaise)}</b>
              <small>Today · {plural(today.billCount, 'bill')}</small>
            </a>
          )}
          <span className="clock"><b>{clock.time}</b><small>{clock.day}</small></span>
          <button className="keys-btn" onClick={() => setHelp(true)} title="Keyboard shortcuts (F1)" aria-label="Keyboard shortcuts (F1)">
            <Icon name="keyboard" />
          </button>
        </div>
      </header>

      <main className={`view${screen === 'billing' ? ' full' : ''}`}>
        {loadError ? (
          <div className="error-box">
            {loadError} <button className="btn small" onClick={() => { setLoadError(null); loadSettings() }}>Try again</button>
          </div>
        ) : !settings ? (
          <div className="empty">Loading…</div>
        ) : (
          <>
            {screen === 'billing' && <BillingScreen onPrint={printBill} onPreview={previewBill} />}
            {screen === 'bills' && <BillsScreen onPrint={printBill} onPreview={previewBill} />}
            {screen === 'menu' && <MenuScreen settings={settings} />}
            {screen === 'settings' && <SettingsScreen settings={settings} onSaved={setSettings} backup={backup} />}
          </>
        )}
      </main>
      {preview && <PrintPreview output={preview.output} printed={preview.printed} onClose={() => setPreview(null)} />}
      {help && <ShortcutsHelp onClose={() => setHelp(false)} />}
      <Toaster />
    </div>
  )
}
