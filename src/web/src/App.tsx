import { useCallback, useEffect, useState } from 'react'
import { Icon, type IconName } from './components/Icon'
import { Toaster } from './components/Toaster'
import { toast } from './components/toast'
import { api, errorText } from './lib/api'
import { time } from './lib/bills'
import { rupees } from './lib/money'
import type { Bill, Settings, TodaySummary } from './lib/types'
import { BillingScreen } from './screens/billing/BillingScreen'
import { BillsScreen } from './screens/history/BillsScreen'
import { MenuScreen } from './screens/menu/MenuScreen'
import { SettingsScreen } from './screens/SettingsScreen'

type ScreenId = 'billing' | 'bills' | 'menu' | 'settings'

const SCREENS: { id: ScreenId; label: string; icon: IconName; sub: string }[] = [
  { id: 'billing', label: 'Billing', icon: 'billing', sub: 'Add dishes, print the bill, take payment' },
  { id: 'bills', label: 'Bill history', icon: 'bills', sub: "Find, reprint or cancel bills · today's total" },
  { id: 'menu', label: 'Menu', icon: 'menu', sub: 'Dishes, prices and what is available today' },
  { id: 'settings', label: 'Settings', icon: 'settings', sub: 'Restaurant details, GST and bill footer' },
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
  return time(now.toISOString())
}

export default function App() {
  const [screen, setScreen] = useState<ScreenId>(currentScreen)
  const [settings, setSettings] = useState<Settings | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const clock = useClock()

  const [today, setToday] = useState<TodaySummary | null>(null)

  const loadToday = useCallback(() => {
    api.get<TodaySummary>('/bills/today').then(setToday, () => {})
  }, [])

  useEffect(() => {
    loadToday()
    const t = window.setInterval(loadToday, 60000)
    window.addEventListener('pos:bills-changed', loadToday)
    return () => {
      window.clearInterval(t)
      window.removeEventListener('pos:bills-changed', loadToday)
    }
  }, [loadToday])

  // Printing: finalises the bill (gives it a number). The print preview comes with the bill print layout.
  const printBill = useCallback(async (bill: Bill): Promise<Bill | null> => {
    try {
      const b = await api.post<Bill>(`/bills/${bill.id}/finalise`)
      toast(`Bill ${b.billNo} is ready.`)
      loadToday()
      return b
    } catch (e) {
      toast(errorText(e), 'error')
      return null
    }
  }, [loadToday])

  const loadSettings = useCallback(() => {
    api.get<Settings>('/settings').then(setSettings, (e) => setLoadError(errorText(e)))
  }, [])

  useEffect(() => {
    loadSettings()
    const onHash = () => setScreen(currentScreen())
    window.addEventListener('hashchange', onHash)
    return () => window.removeEventListener('hashchange', onHash)
  }, [loadSettings])

  const meta = SCREENS.find((s) => s.id === screen)!

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-logo">{(settings?.name || 'R').charAt(0).toUpperCase()}</div>
          <div style={{ minWidth: 0 }}>
            <b>{settings?.name ?? 'Restaurant POS'}</b>
            <small>Billing &amp; Menu</small>
          </div>
        </div>
        {SCREENS.map((s) => (
          <button key={s.id} className={`nav-btn${s.id === screen ? ' active' : ''}`} onClick={() => (window.location.hash = s.id)}>
            <Icon name={s.icon} />
            {s.label}
          </button>
        ))}
        {today && (
          <div className="side-foot">
            Today's total
            <b>{rupees(today.totalPaise)}</b>
            {today.billCount} bills{today.openCount > 0 && ` · ${today.openCount} open`}
          </div>
        )}
      </aside>

      <main>
        <header className="topbar">
          <div>
            <h1>{meta.label}</h1>
            <p>{meta.sub}</p>
          </div>
          <div className="right">
            <span className="pill ok" title="Runs on this laptop">Works without internet</span>
            <span className="clock">{clock}</span>
          </div>
        </header>
        <div className="view" style={screen === 'billing' ? { padding: 0 } : undefined}>
          {loadError ? (
            <div className="error-box">
              {loadError} <button className="btn small" onClick={() => { setLoadError(null); loadSettings() }}>Try again</button>
            </div>
          ) : !settings ? (
            <div className="empty">Loading…</div>
          ) : (
            <>
              {screen === 'billing' && <BillingScreen onPrint={printBill} />}
              {screen === 'bills' && <BillsScreen onPrint={printBill} />}
              {screen === 'menu' && <MenuScreen settings={settings} />}
              {screen === 'settings' && <SettingsScreen settings={settings} onSaved={setSettings} />}
            </>
          )}
        </div>
      </main>
      <Toaster />
    </div>
  )
}
